using System;
using System.Data.SQLite;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;
using OpenCvSharp;
using OpenCvSharp.Extensions;
namespace Assessment_management_and_performance_evaluation
{
    // =========================================================================
    // Event argument types (kept here for single-file convenience)
    // =========================================================================
    public class ProctoringStatusEventArgs : EventArgs
    {
        public Bitmap Frame          { get; set; }
        public string StatusMessage  { get; set; }
        /// <summary>"Normal" | "Warning" | "Critical"</summary>
        public string StatusLevel    { get; set; }
        public int    WarningCount   { get; set; }
        /// <summary>0–100 composite suspicion score from BehaviorAnalyzer.</summary>
        public int    SuspicionScore { get; set; }
        /// <summary>Full behavior description (may list multiple findings).</summary>
        public string BehaviorDetail { get; set; }
    }

    public class ProctoringViolationEventArgs : EventArgs
    {
        public string        ViolationType { get; set; }
        public int           WarningCount  { get; set; }
        public Bitmap        Snapshot      { get; set; }
        public BehaviorResult BehaviorResult { get; set; }
    }

    public class ProctoringTerminatedEventArgs : EventArgs
    {
        public string Reason { get; set; }
    }

    // =========================================================================
    // ProctoringEngine
    //
    // Orchestrates the exam-session lifecycle:
    //   • Opens the device camera via OpenCvSharp VideoCapture
    //   • Runs BehaviorAnalyzer on every frame (~2 fps)
    //   • Publishes annotated frames + scores to LiveFrameStore (admin polling)
    //   • Persists violations + snapshots to SQLite
    //   • Fires events that student_dashboard.cs listens to
    // =========================================================================
    public class ProctoringEngine
    {
        // ------------------------------------------------------------------
        // DB
        // ------------------------------------------------------------------
        private static readonly string ConnectionString =
            "Data Source=assessment.db;Version=3;";

        // ------------------------------------------------------------------
        // CV resources
        // ------------------------------------------------------------------
        private VideoCapture   _capture;
        private BehaviorAnalyzer _analyzer;

        // ------------------------------------------------------------------
        // Timer
        // ------------------------------------------------------------------
        private System.Windows.Forms.Timer _processTimer;
        private bool _isProcessing;

        // ------------------------------------------------------------------
        // Session state
        // ------------------------------------------------------------------
        public int  StudentID    { get; private set; }
        public int  AssessmentID { get; private set; }
        public int  SessionID    { get; private set; }
        public int  WarningCount { get; private set; }
        public int  MaxWarnings  { get; set; } = 3;
        public bool IsRunning    { get; private set; }

        /// <summary>Cached student display name for LiveFrameStore tiles.</summary>
        private string _studentName = "Student";

        // ------------------------------------------------------------------
        // Violation debounce – require sustained detection before issuing a warning
        // ------------------------------------------------------------------
        private int _consecutiveViolationFrames;
        private const int VIOLATION_THRESHOLD_FRAMES = 6; // ~3 s at 2 fps

        // ------------------------------------------------------------------
        // Events (subscribed by student_dashboard)
        // ------------------------------------------------------------------
        public event EventHandler<ProctoringStatusEventArgs>    StatusUpdated;
        public event EventHandler<ProctoringViolationEventArgs> ViolationOccurred;
        public event EventHandler<ProctoringTerminatedEventArgs> SessionTerminated;

        // ==================================================================
        // Constructor
        // ==================================================================
        public ProctoringEngine()
        {
            EnsureDatabaseTables();
            _analyzer = new BehaviorAnalyzer();
        }

        // ==================================================================
        // Database setup  (called from Admin_dashboard constructor too)
        // ==================================================================
        public static void EnsureDatabaseTables()
        {
            try
            {
                using (var conn = new SQLiteConnection(ConnectionString))
                {
                    conn.Open();

                    // ---- ExamSessions ----------------------------------------
                    const string createSessions = @"
                        CREATE TABLE IF NOT EXISTS ExamSessions (
                            SessionID         INTEGER PRIMARY KEY AUTOINCREMENT,
                            StudentID         INTEGER NOT NULL,
                            AssessmentID      INTEGER NOT NULL,
                            StartTime         DATETIME DEFAULT CURRENT_TIMESTAMP,
                            EndTime           DATETIME,
                            Status            TEXT NOT NULL DEFAULT 'In Progress',
                            WarningCount      INTEGER DEFAULT 0,
                            TerminationReason TEXT
                        );";

                    // ---- ProctoringLogs --------------------------------------
                    const string createLogs = @"
                        CREATE TABLE IF NOT EXISTS ProctoringLogs (
                            LogID          INTEGER PRIMARY KEY AUTOINCREMENT,
                            SessionID      INTEGER NOT NULL,
                            StudentID      INTEGER NOT NULL,
                            AssessmentID   INTEGER NOT NULL,
                            ViolationType  TEXT    NOT NULL,
                            Timestamp      DATETIME DEFAULT CURRENT_TIMESTAMP,
                            Severity       TEXT    NOT NULL,
                            Confidence     REAL    DEFAULT 0,
                            SuspicionScore INTEGER DEFAULT 0,
                            BehaviorDetail TEXT,
                            SnapshotImage  BLOB
                        );";

                    // ---- Migrations: add new columns if table already exists --
                    const string addConfidence     = @"ALTER TABLE ProctoringLogs ADD COLUMN Confidence     REAL    DEFAULT 0;";
                    const string addSuspicion      = @"ALTER TABLE ProctoringLogs ADD COLUMN SuspicionScore INTEGER DEFAULT 0;";
                    const string addBehaviorDetail = @"ALTER TABLE ProctoringLogs ADD COLUMN BehaviorDetail TEXT;";

                    ExecNonQuery(conn, createSessions);
                    ExecNonQuery(conn, createLogs);

                    // Gracefully add columns (SQLite throws if column already exists — we catch that)
                    TryAlterTable(conn, addConfidence);
                    TryAlterTable(conn, addSuspicion);
                    TryAlterTable(conn, addBehaviorDetail);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ProctoringEngine] EnsureDatabaseTables: {ex.Message}");
            }
        }

        private static void ExecNonQuery(SQLiteConnection conn, string sql)
        {
            using (var cmd = new SQLiteCommand(sql, conn))
                cmd.ExecuteNonQuery();
        }

        private static void TryAlterTable(SQLiteConnection conn, string sql)
        {
            try { ExecNonQuery(conn, sql); }
            catch { /* column already exists — harmless */ }
        }

        // ==================================================================
        // Session lifecycle
        // ==================================================================

        /// <summary>Opens the camera, creates an ExamSessions row, starts the frame timer.</summary>
        public void StartSession(int studentId, int assessmentId)
        {
            if (IsRunning) return;

            StudentID    = studentId;
            AssessmentID = assessmentId;
            WarningCount = 0;
            _consecutiveViolationFrames = 0;

            // Resolve student display name for admin tiles
            _studentName = ResolveStudentName(studentId);

            // Create session row
            SessionID = CreateSessionRow(studentId, assessmentId);

            // Open camera
            OpenCamera();

            // Start frame processing timer
            _processTimer = new System.Windows.Forms.Timer { Interval = 500 }; // 2 fps
            _processTimer.Tick += ProcessTimer_Tick;
            _processTimer.Start();

            IsRunning = true;
        }

        private int CreateSessionRow(int studentId, int assessmentId)
        {
            try
            {
                using (var conn = new SQLiteConnection(ConnectionString))
                {
                    conn.Open();
                    // SQLite does not support multi-statement ExecuteScalar via the standard driver,
                    // so we use two separate commands.
                    const string insert = @"
                        INSERT INTO ExamSessions (StudentID, AssessmentID, Status, WarningCount)
                        VALUES (@sid, @aid, 'In Progress', 0);";
                    using (var cmd = new SQLiteCommand(insert, conn))
                    {
                        cmd.Parameters.AddWithValue("@sid", studentId);
                        cmd.Parameters.AddWithValue("@aid", assessmentId);
                        cmd.ExecuteNonQuery();
                    }
                    using (var cmd = new SQLiteCommand("SELECT last_insert_rowid();", conn))
                        return Convert.ToInt32(cmd.ExecuteScalar());
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ProctoringEngine] CreateSessionRow: {ex.Message}");
                return -1;
            }
        }

        private void OpenCamera()
        {
            try
            {
                _capture = new VideoCapture(0, VideoCaptureAPIs.ANY);
                if (!_capture.IsOpened())
                {
                    _capture.Dispose();
                    _capture = null;
                    Console.WriteLine("[ProctoringEngine] Camera unavailable — running in simulated mode.");
                }
            }
            catch
            {
                _capture = null;
            }
        }

        private string ResolveStudentName(int studentId)
        {
            try
            {
                using (var conn = new SQLiteConnection(ConnectionString))
                {
                    conn.Open();
                    const string q = "SELECT FirstName || ' ' || LastName FROM Students WHERE UserID = @id LIMIT 1;";
                    using (var cmd = new SQLiteCommand(q, conn))
                    {
                        cmd.Parameters.AddWithValue("@id", studentId);
                        var result = cmd.ExecuteScalar();
                        if (result != null && result != DBNull.Value)
                            return result.ToString();
                    }
                }
            }
            catch { /* non-critical */ }
            return $"Student #{studentId}";
        }

        // ==================================================================
        // Per-frame processing
        // ==================================================================
        private void ProcessTimer_Tick(object sender, EventArgs e)
        {
            if (_isProcessing || !IsRunning) return;
            _isProcessing = true;

            try
            {
                Bitmap        outputBitmap   = null;
                BehaviorResult behaviorResult = null;

                if (_capture != null && _capture.IsOpened())
                {
                    using (var frame = new Mat())
                    {
                        if (_capture.Read(frame) && !frame.Empty())
                        {
                            // Run ML behavior analysis — annotates frame in-place
                            behaviorResult = _analyzer.Analyze(frame);
                            outputBitmap   = BitmapConverter.ToBitmap(frame);
                        }
                    }
                }
                else
                {
                    // Simulated mode — placeholder bitmap
                    outputBitmap   = CreatePlaceholderFrame();
                    behaviorResult = new BehaviorResult
                    {
                        BehaviorType   = null,
                        Description    = "Monitoring Active (simulated)",
                        Confidence     = 1.0,
                        Severity       = BehaviorSeverity.Normal,
                        SuspicionScore = 0
                    };
                }

                // ---- Push to LiveFrameStore so admin dashboard can poll ----
                string liveLevel;
                if (behaviorResult == null || behaviorResult.Severity == BehaviorSeverity.Normal || behaviorResult.Severity == BehaviorSeverity.Low)
                    liveLevel = "Normal";
                else if (behaviorResult.Severity == BehaviorSeverity.Critical)
                    liveLevel = "Critical";
                else
                    liveLevel = "Warning";

                LiveFrameStore.Instance.Update(
                    SessionID,
                    outputBitmap,
                    behaviorResult,
                    StudentID,
                    _studentName,
                    WarningCount);

                // ---- Violation handling with debounce ----------------------
                bool hasViolation = behaviorResult != null && !behaviorResult.IsClean;

                if (hasViolation)
                {
                    _consecutiveViolationFrames++;

                    if (_consecutiveViolationFrames >= VIOLATION_THRESHOLD_FRAMES)
                    {
                        _consecutiveViolationFrames = 0;
                        WarningCount++;

                        PersistViolationLog(behaviorResult, outputBitmap);

                        ViolationOccurred?.Invoke(this, new ProctoringViolationEventArgs
                        {
                            ViolationType  = behaviorResult.BehaviorType,
                            WarningCount   = WarningCount,
                            Snapshot       = outputBitmap,
                            BehaviorResult = behaviorResult
                        });

                        if (WarningCount >= MaxWarnings)
                            TerminateSession("Exceeded maximum allowed suspicious-behavior warnings.");
                    }
                }
                else
                {
                    if (_consecutiveViolationFrames > 0)
                        _consecutiveViolationFrames--;
                }

                // ---- Fire status event for student_dashboard UI ------------
                string statusLevel;
                if (behaviorResult == null || behaviorResult.Severity == BehaviorSeverity.Normal || behaviorResult.Severity == BehaviorSeverity.Low)
                    statusLevel = "Normal";
                else if (behaviorResult.Severity == BehaviorSeverity.Critical)
                    statusLevel = "Critical";
                else
                    statusLevel = "Warning";

                string statusMsg = hasViolation
                    ? $"ALERT: {behaviorResult.BehaviorType?.ToUpper()}"
                    : "NORMAL — MONITORING ACTIVE";

                StatusUpdated?.Invoke(this, new ProctoringStatusEventArgs
                {
                    Frame          = outputBitmap,
                    StatusMessage  = statusMsg,
                    StatusLevel    = statusLevel,
                    WarningCount   = WarningCount,
                    SuspicionScore = behaviorResult?.SuspicionScore ?? 0,
                    BehaviorDetail = behaviorResult?.Description
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ProctoringEngine] Frame error: {ex.Message}");
            }
            finally
            {
                _isProcessing = false;
            }
        }

        // ==================================================================
        // Persistence
        // ==================================================================
        private void PersistViolationLog(BehaviorResult result, Bitmap snapshot)
        {
            try
            {
                byte[] imageBytes = null;
                if (snapshot != null)
                {
                    using (var ms = new MemoryStream())
                    {
                        snapshot.Save(ms, ImageFormat.Jpeg);
                        imageBytes = ms.ToArray();
                    }
                }

                using (var conn = new SQLiteConnection(ConnectionString))
                {
                    conn.Open();

                    // Update session warning count
                    const string updateSession = @"
                        UPDATE ExamSessions
                        SET WarningCount = @wc
                        WHERE SessionID  = @sid;";
                    using (var cmd = new SQLiteCommand(updateSession, conn))
                    {
                        cmd.Parameters.AddWithValue("@wc",  WarningCount);
                        cmd.Parameters.AddWithValue("@sid", SessionID);
                        cmd.ExecuteNonQuery();
                    }

                    // Insert log row with ML metadata
                    const string insertLog = @"
                        INSERT INTO ProctoringLogs
                            (SessionID, StudentID, AssessmentID,
                             ViolationType, Severity,
                             Confidence, SuspicionScore, BehaviorDetail,
                             SnapshotImage)
                        VALUES
                            (@sid, @stid, @aid,
                             @vtype, @sev,
                             @conf, @score, @detail,
                             @img);";
                    using (var cmd = new SQLiteCommand(insertLog, conn))
                    {
                        cmd.Parameters.AddWithValue("@sid",    SessionID);
                        cmd.Parameters.AddWithValue("@stid",   StudentID);
                        cmd.Parameters.AddWithValue("@aid",    AssessmentID);
                        cmd.Parameters.AddWithValue("@vtype",  result.BehaviorType ?? "Unknown");
                        cmd.Parameters.AddWithValue("@sev",    result.Severity.ToString());
                        cmd.Parameters.AddWithValue("@conf",   result.Confidence);
                        cmd.Parameters.AddWithValue("@score",  result.SuspicionScore);
                        cmd.Parameters.AddWithValue("@detail", result.Description ?? "");
                        cmd.Parameters.AddWithValue("@img",    (object)imageBytes ?? DBNull.Value);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ProctoringEngine] PersistViolationLog: {ex.Message}");
            }
        }

        // ==================================================================
        // Session termination / completion
        // ==================================================================
        public void TerminateSession(string reason)
        {
            if (!IsRunning) return;
            StopResources();
            UpdateSessionStatus("Terminated", reason);
            LiveFrameStore.Instance.MarkInactive(SessionID);
            SessionTerminated?.Invoke(this, new ProctoringTerminatedEventArgs { Reason = reason });
        }

        public void CompleteSession()
        {
            if (!IsRunning) return;
            StopResources();
            UpdateSessionStatus("Completed", null);
            LiveFrameStore.Instance.MarkInactive(SessionID);
        }

        private void StopResources()
        {
            IsRunning = false;

            _processTimer?.Stop();
            _processTimer?.Dispose();
            _processTimer = null;

            if (_capture != null)
            {
                if (_capture.IsOpened()) _capture.Release();
                _capture.Dispose();
                _capture = null;
            }

            _analyzer?.Dispose();
            _analyzer = null;
        }

        private void UpdateSessionStatus(string status, string reason)
        {
            try
            {
                using (var conn = new SQLiteConnection(ConnectionString))
                {
                    conn.Open();
                    const string q = @"
                        UPDATE ExamSessions
                        SET Status            = @status,
                            EndTime           = CURRENT_TIMESTAMP,
                            TerminationReason = @reason
                        WHERE SessionID = @sid;";
                    using (var cmd = new SQLiteCommand(q, conn))
                    {
                        cmd.Parameters.AddWithValue("@status", status);
                        cmd.Parameters.AddWithValue("@reason", (object)reason ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@sid",    SessionID);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ProctoringEngine] UpdateSessionStatus: {ex.Message}");
            }
        }

        // ==================================================================
        // Placeholder frame (camera unavailable / simulated mode)
        // ==================================================================
        private Bitmap CreatePlaceholderFrame()
        {
            var bmp = new Bitmap(320, 240);
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.FromArgb(25, 25, 35));
                using (var pen  = new Pen(Color.LimeGreen, 2))
                    g.DrawRectangle(pen, 8, 8, 304, 224);
                using (var font = new Font("Segoe UI", 9, FontStyle.Bold))
                {
                    g.DrawString("PROCTORING — SIMULATED MODE", font, Brushes.LightGreen, 20, 28);
                    g.DrawString($"Student : {_studentName}",          font, Brushes.White,      20, 58);
                    g.DrawString($"Warnings: {WarningCount}/{MaxWarnings}", font, Brushes.Yellow, 20, 84);
                    g.DrawString("ML Analysis: Active",                font, Brushes.Cyan,       20, 110);
                    g.DrawString(DateTime.Now.ToString("HH:mm:ss"),    font, Brushes.Gray,        20, 196);
                }
            }
            return bmp;
        }
    }
}
