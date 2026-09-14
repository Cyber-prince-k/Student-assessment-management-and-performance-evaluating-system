using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Media;
using System.Windows.Forms;

namespace Assessment_management_and_performance_evaluation
{
    public partial class Admin_dashboard : Form
    {
        // ------------------------------------------------------------------
        // Core state
        // ------------------------------------------------------------------
        private readonly int _loggedInUserId;
        private readonly Administrator _admin = new Administrator();

        // ------------------------------------------------------------------
        // Proctoring tab — db-backed session / log grids
        // ------------------------------------------------------------------
        private TabPage        _tabProctoring;
        private DataGridView   _dgvSessions;
        private DataGridView   _dgvLogs;
        private PictureBox     _picSnapshotEvidence;
        private Label          _lblDetailHeader;
        private Label          _lblBehaviorDetail;
        private ProgressBar    _pbSuspicion;
        private Label          _lblSuspicionPct;

        /// <summary>Timer that refreshes the DB-backed session/log grids.</summary>
        private System.Windows.Forms.Timer _dbRefreshTimer;

        // ------------------------------------------------------------------
        // Live-feed tab — reads directly from LiveFrameStore (no DB round-trip)
        // ------------------------------------------------------------------
        private TabPage    _tabLiveFeed;
        private Panel      _panelTiles;
        private Label      _lblLiveHeader;
        private Label      _lblActiveCount;
        private Label      _lblGlobalStatus;

        /// <summary>Timer that redraws the live tile grid from LiveFrameStore.</summary>
        private System.Windows.Forms.Timer _liveRefreshTimer;

        /// <summary>Tile controls keyed by SessionID.</summary>
        private readonly Dictionary<int, StudentTile> _tiles = new Dictionary<int, StudentTile>();

        /// <summary>Track last known violation count per session to detect new alerts.</summary>
        private readonly Dictionary<int, int> _lastWarningCount = new Dictionary<int, int>();

        // ------------------------------------------------------------------
        // Alert sound (generated in-memory — no external file dependency)
        // ------------------------------------------------------------------
        private SoundPlayer _alertPlayer;

        // ==================================================================
        // Constructor
        // ==================================================================
        public Admin_dashboard(int userId)
        {
            InitializeComponent();
            _loggedInUserId = userId;

            ProctoringEngine.EnsureDatabaseTables();

            BuildAlertSound();
            InitializeProctoringTab();
            InitializeLiveFeedTab();
        }

        // ==================================================================
        // SECTION 1 — Proctoring Tab (DB-backed session history + snapshots)
        // ==================================================================

        private void InitializeProctoringTab()
        {
            try
            {
                _tabProctoring = new TabPage
                {
                    Text      = "Proctoring & Behavior Monitor",
                    BackColor = Color.FromArgb(240, 243, 246)
                };

                // ---- Header -----------------------------------------------
                var lblHeader = MakeLabel(
                    "Live Student Behavior Monitoring & Exam Proctoring",
                    new Font("Segoe UI", 12, FontStyle.Bold),
                    Color.FromArgb(30, 40, 60),
                    new Point(15, 12));
                _tabProctoring.Controls.Add(lblHeader);

                // ---- Session Grid -----------------------------------------
                _tabProctoring.Controls.Add(
                    MakeLabel("Active / Recent Exam Sessions:",
                              new Font("Segoe UI", 9, FontStyle.Bold),
                              Color.FromArgb(50, 50, 80), new Point(15, 45)));

                _dgvSessions = BuildDataGridView(new Point(15, 68), new Size(530, 200));
                _dgvSessions.SelectionChanged += DgvSessions_SelectionChanged;
                _tabProctoring.Controls.Add(_dgvSessions);

                // ---- Snapshot Evidence + Detail ---------------------------
                _tabProctoring.Controls.Add(
                    MakeLabel("Live Vision Snapshot Evidence:",
                              new Font("Segoe UI", 9, FontStyle.Bold),
                              Color.FromArgb(50, 50, 80), new Point(560, 45)));

                _picSnapshotEvidence = new PictureBox
                {
                    Location    = new Point(560, 68),
                    Size        = new Size(340, 200),
                    SizeMode    = PictureBoxSizeMode.Zoom,
                    BorderStyle = BorderStyle.FixedSingle,
                    BackColor   = Color.Black
                };
                _tabProctoring.Controls.Add(_picSnapshotEvidence);

                // Suspicion score bar under snapshot
                _tabProctoring.Controls.Add(
                    MakeLabel("ML Suspicion Score:",
                              new Font("Segoe UI", 8, FontStyle.Bold),
                              Color.FromArgb(50, 50, 80), new Point(560, 272)));

                _pbSuspicion = new ProgressBar
                {
                    Location = new Point(560, 292),
                    Size     = new Size(270, 18),
                    Minimum  = 0,
                    Maximum  = 100,
                    Value    = 0,
                    Style    = ProgressBarStyle.Continuous
                };
                _tabProctoring.Controls.Add(_pbSuspicion);

                _lblSuspicionPct = MakeLabel("0 %",
                    new Font("Segoe UI", 8, FontStyle.Bold),
                    Color.FromArgb(30, 40, 60), new Point(836, 292));
                _tabProctoring.Controls.Add(_lblSuspicionPct);

                // Behavior description
                _lblDetailHeader = MakeLabel("Behavior Detail:",
                    new Font("Segoe UI", 8, FontStyle.Bold),
                    Color.FromArgb(50, 50, 80), new Point(560, 316));
                _tabProctoring.Controls.Add(_lblDetailHeader);

                _lblBehaviorDetail = new Label
                {
                    Location  = new Point(560, 334),
                    Size      = new Size(340, 50),
                    Font      = new Font("Segoe UI", 8),
                    ForeColor = Color.FromArgb(180, 60, 20),
                    Text      = "—",
                    AutoSize  = false
                };
                _tabProctoring.Controls.Add(_lblBehaviorDetail);

                // ---- Violation Log Grid -----------------------------------
                _tabProctoring.Controls.Add(
                    MakeLabel("Violation History & AI Behavior Alerts:",
                              new Font("Segoe UI", 9, FontStyle.Bold),
                              Color.FromArgb(50, 50, 80), new Point(15, 280)));

                _dgvLogs = BuildDataGridView(new Point(15, 303), new Size(530, 200));
                _dgvLogs.SelectionChanged += DgvLogs_SelectionChanged;
                _tabProctoring.Controls.Add(_dgvLogs);

                // ---- Buttons ----------------------------------------------
                var btnRefresh = BuildButton("🔄  Refresh", new Point(560, 390),
                    Color.FromArgb(41, 128, 185));
                btnRefresh.Click += (s, e) => LoadProctoringSessions();
                _tabProctoring.Controls.Add(btnRefresh);

                var btnTerminate = BuildButton("🚫  Terminate", new Point(560, 434),
                    Color.FromArgb(192, 57, 43));
                btnTerminate.Click += BtnForceTerminate_Click;
                _tabProctoring.Controls.Add(btnTerminate);

                var btnExport = BuildButton("💾  Export CSV", new Point(560, 478),
                    Color.FromArgb(39, 174, 96));
                btnExport.Click += BtnExportCsv_Click;
                _tabProctoring.Controls.Add(btnExport);

                // Add tab
                guna2TabControl1.TabPages.Add(_tabProctoring);

                // DB auto-refresh timer (every 3 s)
                _dbRefreshTimer = new System.Windows.Forms.Timer { Interval = 3000 };
                _dbRefreshTimer.Tick += (s, e) => LoadProctoringSessions();
                _dbRefreshTimer.Start();

                LoadProctoringSessions();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Admin] InitializeProctoringTab: {ex.Message}");
            }
        }

        // ---- DB query helpers -------------------------------------------

        private void LoadProctoringSessions()
        {
            try
            {
                using (var conn = new System.Data.SQLite.SQLiteConnection(
                    "Data Source=assessment.db;Version=3;"))
                {
                    conn.Open();

                    const string q = @"
                        SELECT s.SessionID,
                               (u.FirstName || ' ' || u.LastName) AS StudentName,
                               a.Title   AS AssessmentTitle,
                               s.StartTime,
                               s.Status,
                               s.WarningCount,
                               s.TerminationReason
                        FROM   ExamSessions s
                        JOIN   Students    u ON s.StudentID    = u.UserID
                        JOIN   Assessments a ON s.AssessmentID = a.AssessmentID
                        ORDER  BY s.SessionID DESC;";

                    using (var da = new System.Data.SQLite.SQLiteDataAdapter(q, conn))
                    {
                        var dt = new DataTable();
                        da.Fill(dt);

                        // Colour rows by status — must run on UI thread
                        if (this.InvokeRequired)
                            this.BeginInvoke((MethodInvoker)(() => BindSessionGrid(dt)));
                        else
                            BindSessionGrid(dt);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Admin] LoadProctoringSessions: {ex.Message}");
            }
        }

        private void BindSessionGrid(DataTable dt)
        {
            _dgvSessions.DataSource = dt;
            foreach (DataGridViewRow row in _dgvSessions.Rows)
            {
                string status = row.Cells["Status"]?.Value?.ToString() ?? "";
                if (status == "Terminated")
                    row.DefaultCellStyle.BackColor = Color.FromArgb(255, 220, 220);
                else if (status == "In Progress")
                    row.DefaultCellStyle.BackColor = Color.FromArgb(220, 255, 220);
            }
        }

        private void DgvSessions_SelectionChanged(object sender, EventArgs e)
        {
            if (_dgvSessions.CurrentRow == null) return;
            try
            {
                int sid = Convert.ToInt32(_dgvSessions.CurrentRow.Cells["SessionID"].Value);
                LoadSessionLogs(sid);
            }
            catch { }
        }

        private void LoadSessionLogs(int sessionId)
        {
            try
            {
                using (var conn = new System.Data.SQLite.SQLiteConnection(
                    "Data Source=assessment.db;Version=3;"))
                {
                    conn.Open();

                    // Load log rows (include new ML columns gracefully)
                    const string logQ = @"
                        SELECT LogID,
                               Timestamp,
                               ViolationType,
                               Severity,
                               CAST(ROUND(COALESCE(Confidence,0)*100) AS INTEGER) AS Confidence_Pct,
                               COALESCE(SuspicionScore,0) AS SuspicionScore,
                               COALESCE(BehaviorDetail,'') AS BehaviorDetail
                        FROM   ProctoringLogs
                        WHERE  SessionID = @sid
                        ORDER  BY LogID DESC;";

                    using (var cmd = new System.Data.SQLite.SQLiteCommand(logQ, conn))
                    {
                        cmd.Parameters.AddWithValue("@sid", sessionId);
                        using (var da = new System.Data.SQLite.SQLiteDataAdapter(cmd))
                        {
                            var dt = new DataTable();
                            da.Fill(dt);
                            _dgvLogs.DataSource = dt;

                            // Update suspicion bar from the latest log row
                            if (dt.Rows.Count > 0)
                            {
                                int score = Convert.ToInt32(dt.Rows[0]["SuspicionScore"]);
                                string detail = dt.Rows[0]["BehaviorDetail"].ToString();
                                UpdateSuspicionBar(score, detail);
                            }
                            else
                            {
                                UpdateSuspicionBar(0, "—");
                            }
                        }
                    }

                    // Latest snapshot
                    const string imgQ = @"
                        SELECT SnapshotImage
                        FROM   ProctoringLogs
                        WHERE  SessionID = @sid
                          AND  SnapshotImage IS NOT NULL
                        ORDER  BY LogID DESC
                        LIMIT  1;";
                    using (var cmd = new System.Data.SQLite.SQLiteCommand(imgQ, conn))
                    {
                        cmd.Parameters.AddWithValue("@sid", sessionId);
                        var result = cmd.ExecuteScalar();
                        if (result != null && result != DBNull.Value)
                            DisplaySnapshot((byte[])result);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Admin] LoadSessionLogs: {ex.Message}");
            }
        }

        private void DgvLogs_SelectionChanged(object sender, EventArgs e)
        {
            if (_dgvLogs.CurrentRow == null) return;
            try
            {
                int logId = Convert.ToInt32(_dgvLogs.CurrentRow.Cells["LogID"].Value);

                // Update suspicion bar from selected row
                int score  = Convert.ToInt32(_dgvLogs.CurrentRow.Cells["SuspicionScore"].Value);
                string det = _dgvLogs.CurrentRow.Cells["BehaviorDetail"].Value?.ToString() ?? "";
                UpdateSuspicionBar(score, det);

                using (var conn = new System.Data.SQLite.SQLiteConnection(
                    "Data Source=assessment.db;Version=3;"))
                {
                    conn.Open();
                    const string q = "SELECT SnapshotImage FROM ProctoringLogs WHERE LogID = @id;";
                    using (var cmd = new System.Data.SQLite.SQLiteCommand(q, conn))
                    {
                        cmd.Parameters.AddWithValue("@id", logId);
                        var result = cmd.ExecuteScalar();
                        if (result != null && result != DBNull.Value)
                            DisplaySnapshot((byte[])result);
                    }
                }
            }
            catch { }
        }

        private void DisplaySnapshot(byte[] bytes)
        {
            try
            {
                using (var ms = new MemoryStream(bytes))
                {
                    var oldImg = _picSnapshotEvidence.Image;
                    _picSnapshotEvidence.Image = Image.FromStream(ms);
                    oldImg?.Dispose();
                }
            }
            catch { }
        }

        private void UpdateSuspicionBar(int score, string detail)
        {
            _pbSuspicion.Value    = Math.Max(0, Math.Min(100, score));
            _lblSuspicionPct.Text = $"{score} %";
            _lblBehaviorDetail.Text = string.IsNullOrWhiteSpace(detail) ? "—" : detail;

            // Colour the bar based on risk level
            if (score >= 75)
            {
                _pbSuspicion.ForeColor = Color.Crimson;
                _lblSuspicionPct.ForeColor = Color.Crimson;
            }
            else if (score >= 40)
            {
                _pbSuspicion.ForeColor = Color.DarkOrange;
                _lblSuspicionPct.ForeColor = Color.DarkOrange;
            }
            else
            {
                _pbSuspicion.ForeColor = Color.SeaGreen;
                _lblSuspicionPct.ForeColor = Color.SeaGreen;
            }
        }

        private void BtnForceTerminate_Click(object sender, EventArgs e)
        {
            if (_dgvSessions.CurrentRow == null)
            {
                MessageBox.Show("Select an active session first.",
                    "No Selection", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            try
            {
                int    sid  = Convert.ToInt32(_dgvSessions.CurrentRow.Cells["SessionID"].Value);
                string name = _dgvSessions.CurrentRow.Cells["StudentName"].Value?.ToString();

                if (MessageBox.Show(
                    $"Force-terminate the exam session for '{name}'?",
                    "Confirm Termination",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                    return;

                using (var conn = new System.Data.SQLite.SQLiteConnection(
                    "Data Source=assessment.db;Version=3;"))
                {
                    conn.Open();
                    const string q = @"
                        UPDATE ExamSessions
                        SET    Status            = 'Terminated',
                               EndTime           = CURRENT_TIMESTAMP,
                               TerminationReason = 'Force Terminated by Admin'
                        WHERE  SessionID = @sid;";
                    using (var cmd = new System.Data.SQLite.SQLiteCommand(q, conn))
                    {
                        cmd.Parameters.AddWithValue("@sid", sid);
                        cmd.ExecuteNonQuery();
                    }
                }

                // Also mark inactive in the live store
                LiveFrameStore.Instance.MarkInactive(sid);
                MessageBox.Show($"Session for '{name}' terminated.",
                    "Done", MessageBoxButtons.OK, MessageBoxIcon.Information);
                LoadProctoringSessions();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}");
            }
        }

        private void BtnExportCsv_Click(object sender, EventArgs e)
        {
            try
            {
                using (var dlg = new SaveFileDialog
                {
                    Filter   = "CSV files|*.csv",
                    FileName = $"proctoring_export_{DateTime.Now:yyyyMMdd_HHmm}.csv"
                })
                {
                    if (dlg.ShowDialog() != DialogResult.OK) return;

                    var dt = _dgvLogs.DataSource as DataTable;
                    if (dt == null) { MessageBox.Show("Select a session first."); return; }

                    using (var sw = new System.IO.StreamWriter(dlg.FileName))
                    {
                        // Header
                        var cols = new List<string>();
                        foreach (DataColumn c in dt.Columns) cols.Add(c.ColumnName);
                        sw.WriteLine(string.Join(",", cols));

                        // Rows
                        foreach (DataRow row in dt.Rows)
                        {
                            var cells = new List<string>();
                            foreach (var v in row.ItemArray)
                                cells.Add($"\"{v?.ToString()?.Replace("\"", "\"\"")}\"");
                            sw.WriteLine(string.Join(",", cells));
                        }
                    }

                    MessageBox.Show($"Exported to:\n{dlg.FileName}",
                        "Export Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Export failed: {ex.Message}");
            }
        }

        // ==================================================================
        // SECTION 2 — Live Feed Tab (reads from LiveFrameStore, no DB)
        // ==================================================================

        private void InitializeLiveFeedTab()
        {
            try
            {
                _tabLiveFeed = new TabPage
                {
                    Text      = "📹  Live Camera Feeds",
                    BackColor = Color.FromArgb(22, 28, 36)
                };

                // ---- Top bar --------------------------------------------
                _lblLiveHeader = new Label
                {
                    Text      = "LIVE AI PROCTORING FEEDS",
                    Font      = new Font("Segoe UI", 13, FontStyle.Bold),
                    ForeColor = Color.FromArgb(0, 220, 160),
                    Location  = new Point(15, 10),
                    AutoSize  = true
                };
                _tabLiveFeed.Controls.Add(_lblLiveHeader);

                _lblActiveCount = new Label
                {
                    Text      = "Active sessions: 0",
                    Font      = new Font("Segoe UI", 9),
                    ForeColor = Color.Silver,
                    Location  = new Point(15, 38),
                    AutoSize  = true
                };
                _tabLiveFeed.Controls.Add(_lblActiveCount);

                _lblGlobalStatus = new Label
                {
                    Text      = "Waiting for student sessions...",
                    Font      = new Font("Segoe UI", 9, FontStyle.Italic),
                    ForeColor = Color.Gray,
                    Location  = new Point(300, 38),
                    AutoSize  = true
                };
                _tabLiveFeed.Controls.Add(_lblGlobalStatus);

                // ---- Scrollable tile panel ------------------------------
                _panelTiles = new Panel
                {
                    Location        = new Point(0, 62),
                    Size            = new Size(980, 490),
                    Anchor          = AnchorStyles.Top | AnchorStyles.Left
                                     | AnchorStyles.Right | AnchorStyles.Bottom,
                    BackColor       = Color.FromArgb(22, 28, 36),
                    AutoScroll      = true
                };
                _tabLiveFeed.Controls.Add(_panelTiles);

                guna2TabControl1.TabPages.Add(_tabLiveFeed);

                // Live feed refresh timer — 500 ms matches ProctoringEngine tick
                _liveRefreshTimer = new System.Windows.Forms.Timer { Interval = 500 };
                _liveRefreshTimer.Tick += LiveRefreshTimer_Tick;
                _liveRefreshTimer.Start();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Admin] InitializeLiveFeedTab: {ex.Message}");
            }
        }

        // ------------------------------------------------------------------
        // Live feed refresh — reads LiveFrameStore, creates / updates tiles
        // ------------------------------------------------------------------
        private void LiveRefreshTimer_Tick(object sender, EventArgs e)
        {
            try
            {
                var sessions = LiveFrameStore.Instance.GetAll();

                int activeCount = 0;
                int alertCount  = 0;

                foreach (var entry in sessions)
                {
                    if (entry.IsActive) activeCount++;
                    if (entry.StatusLevel == "Warning" || entry.StatusLevel == "Critical")
                        alertCount++;

                    // Detect new violations → play sound
                    if (_lastWarningCount.TryGetValue(entry.SessionID, out int prev))
                    {
                        if (entry.WarningCount > prev && entry.WarningCount > 0)
                            PlayAlert();
                    }
                    _lastWarningCount[entry.SessionID] = entry.WarningCount;

                    // Create or update tile
                    if (!_tiles.TryGetValue(entry.SessionID, out var tile))
                    {
                        tile = new StudentTile(entry.SessionID);
                        _tiles[entry.SessionID] = tile;
                        _panelTiles.Controls.Add(tile);
                        LayoutTiles();
                    }

                    tile.UpdateTile(entry);
                }

                // Remove tiles whose sessions have been removed from the store
                var toRemove = new List<int>();
                foreach (var kv in _tiles)
                    if (LiveFrameStore.Instance.GetEntry(kv.Key) == null)
                        toRemove.Add(kv.Key);
                foreach (int sid in toRemove)
                {
                    _panelTiles.Controls.Remove(_tiles[sid]);
                    _tiles[sid].Dispose();
                    _tiles.Remove(sid);
                }

                // Update summary labels
                _lblActiveCount.Text = $"Active sessions: {activeCount}";
                _lblGlobalStatus.Text = alertCount > 0
                    ? $"⚠  {alertCount} session(s) with suspicious behavior"
                    : activeCount > 0
                        ? "✔  All monitored students appear normal"
                        : "Waiting for student sessions...";
                _lblGlobalStatus.ForeColor = alertCount > 0 ? Color.OrangeRed : Color.LightGreen;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Admin] LiveRefreshTimer_Tick: {ex.Message}");
            }
        }

        /// <summary>
        /// Arranges tile controls in a responsive grid inside _panelTiles.
        /// Each tile is TILE_W × TILE_H with TILE_GAP spacing.
        /// </summary>
        private void LayoutTiles()
        {
            const int TILE_W   = 290;
            const int TILE_H   = 270;
            const int TILE_GAP = 12;

            int cols   = Math.Max(1, (_panelTiles.Width - TILE_GAP) / (TILE_W + TILE_GAP));
            int idx    = 0;

            foreach (StudentTile tile in _panelTiles.Controls)
            {
                int col = idx % cols;
                int row = idx / cols;
                tile.Location = new Point(
                    TILE_GAP + col * (TILE_W + TILE_GAP),
                    TILE_GAP + row * (TILE_H + TILE_GAP));
                tile.Size = new Size(TILE_W, TILE_H);
                idx++;
            }
        }

        // ==================================================================
        // SECTION 3 — Alert Sound (generated in-memory, no wav file needed)
        // ==================================================================

        private void BuildAlertSound()
        {
            try
            {
                // Build a minimal WAV byte array (short 440 Hz beep, 0.15 s)
                int    sampleRate  = 8000;
                int    durationMs  = 150;
                int    samples     = sampleRate * durationMs / 1000;
                int    dataSize    = samples * 2; // 16-bit mono
                var    wav         = new System.IO.MemoryStream();

                // RIFF header
                void WriteStr(string s) { foreach (char c in s) wav.WriteByte((byte)c); }
                void WriteInt(int v)    { wav.Write(BitConverter.GetBytes(v), 0, 4); }
                void WriteShort(short v){ wav.Write(BitConverter.GetBytes(v), 0, 2); }

                WriteStr("RIFF");
                WriteInt(36 + dataSize);
                WriteStr("WAVE");
                WriteStr("fmt ");
                WriteInt(16);          // chunk size
                WriteShort(1);         // PCM
                WriteShort(1);         // mono
                WriteInt(sampleRate);
                WriteInt(sampleRate * 2); // byte rate
                WriteShort(2);         // block align
                WriteShort(16);        // bits per sample
                WriteStr("data");
                WriteInt(dataSize);

                for (int i = 0; i < samples; i++)
                {
                    double t     = (double)i / sampleRate;
                    double freq  = 880.0;
                    double amp   = 0.5 * Math.Sin(2 * Math.PI * freq * t)
                                 * Math.Exp(-t * 12); // decay envelope
                    short  s16   = (short)(amp * short.MaxValue);
                    WriteShort(s16);
                }

                wav.Position = 0;
                _alertPlayer = new SoundPlayer(wav);
                _alertPlayer.Load();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Admin] BuildAlertSound: {ex.Message}");
                _alertPlayer = null;
            }
        }

        private void PlayAlert()
        {
            try { _alertPlayer?.Play(); }
            catch { }
        }

        // ==================================================================
        // SECTION 4 — Teacher Registration (existing functionality)
        // ==================================================================

        private void guna2Button2_Click(object sender, EventArgs e)
        {
            try
            {
                string firstName       = txtFirstName.Text.Trim();
                string lastName        = txtLastName.Text.Trim();
                string email           = txtEmail.Text.Trim();
                string subjects        = txtSubjects.Text.Trim();
                string classes         = txtClasses.Text.Trim();
                string subjectRoot     = cmbSubjectRoot.SelectedItem?.ToString();

                bool ok = _admin.RegisterTeacher(
                    firstName, lastName, email, subjects, classes, subjectRoot);

                if (ok)
                {
                    txtFirstName.Clear();
                    txtLastName.Clear();
                    txtEmail.Clear();
                    txtSubjects.Clear();
                    txtClasses.Clear();
                    cmbSubjectRoot.SelectedIndex = -1;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Unexpected error: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ==================================================================
        // Existing Designer-wired event stubs (kept to avoid compile errors)
        // ==================================================================
        private void create_account_Load(object sender, EventArgs e) { }
        private void guna2TextBox3_TextChanged(object sender, EventArgs e) { }
        private void guna2TextBox1_TextChanged(object sender, EventArgs e) { }
        private void guna2TextBox2_TextChanged(object sender, EventArgs e) { }
        private void guna2TextBox7_TextChanged(object sender, EventArgs e) { }
        private void label6_Click(object sender, EventArgs e) { }
        private void guna2TextBox8_TextChanged(object sender, EventArgs e) { }
        private void guna2TextBox5_TextChanged(object sender, EventArgs e) { }
        private void label2_Click(object sender, EventArgs e) { }
        private void label3_Click(object sender, EventArgs e) { }
        private void label4_Click(object sender, EventArgs e) { }
        private void profile_Click(object sender, EventArgs e) { }
        private void guna2Button1_Click(object sender, EventArgs e) { }

        // ==================================================================
        // Form cleanup
        // ==================================================================
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);
            _dbRefreshTimer?.Stop();
            _dbRefreshTimer?.Dispose();
            _liveRefreshTimer?.Stop();
            _liveRefreshTimer?.Dispose();
            _alertPlayer?.Dispose();
        }

        // ==================================================================
        // UI FACTORY HELPERS
        // ==================================================================

        private static Label MakeLabel(string text, Font font, Color fore, Point loc)
        {
            return new Label
            {
                Text      = text,
                Font      = font,
                ForeColor = fore,
                Location  = loc,
                AutoSize  = true
            };
        }

        private static DataGridView BuildDataGridView(Point loc, Size size)
        {
            return new DataGridView
            {
                Location              = loc,
                Size                  = size,
                SelectionMode         = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect           = false,
                ReadOnly              = true,
                AllowUserToAddRows    = false,
                AutoSizeColumnsMode   = DataGridViewAutoSizeColumnsMode.Fill,
                BackgroundColor       = Color.White,
                BorderStyle           = BorderStyle.Fixed3D,
                RowHeadersVisible     = false,
                ColumnHeadersHeightSizeMode =
                    DataGridViewColumnHeadersHeightSizeMode.AutoSize
            };
        }

        private static Button BuildButton(string text, Point loc, Color bg)
        {
            return new Button
            {
                Text      = text,
                Font      = new Font("Segoe UI", 9, FontStyle.Bold),
                Location  = loc,
                Size      = new Size(160, 36),
                BackColor = bg,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
        }
    }

    // =========================================================================
    // StudentTile
    //
    // A self-contained WinForms Panel that displays one student's live camera
    // feed, name, status badge, suspicion score bar, and warning count.
    // Updated every 500 ms by the admin's live-refresh timer.
    // =========================================================================
    internal class StudentTile : Panel
    {
        private readonly int   _sessionId;
        private PictureBox     _pic;
        private Label          _lblName;
        private Label          _lblStatus;
        private Label          _lblWarnings;
        private Label          _lblScore;
        private Panel          _scoreBar;
        private Panel          _scoreBarFill;

        // Track last severity so we can skip redraws when nothing changed
        private string _lastStatusLevel = "";
        private int    _lastScore       = -1;

        public StudentTile(int sessionId)
        {
            _sessionId  = sessionId;
            BorderStyle = BorderStyle.FixedSingle;
            BackColor   = Color.FromArgb(32, 40, 52);

            BuildControls();
        }

        private void BuildControls()
        {
            // Camera feed image
            _pic = new PictureBox
            {
                Location  = new Point(4, 4),
                Size      = new Size(282, 170),
                SizeMode  = PictureBoxSizeMode.Zoom,
                BackColor = Color.Black
            };
            Controls.Add(_pic);

            // Student name
            _lblName = new Label
            {
                Location  = new Point(4, 179),
                Size      = new Size(200, 18),
                Font      = new Font("Segoe UI", 8, FontStyle.Bold),
                ForeColor = Color.White,
                Text      = "Loading..."
            };
            Controls.Add(_lblName);

            // Status badge
            _lblStatus = new Label
            {
                Location  = new Point(4, 198),
                Size      = new Size(282, 16),
                Font      = new Font("Segoe UI", 7, FontStyle.Italic),
                ForeColor = Color.LightGreen,
                Text      = "Monitoring..."
            };
            Controls.Add(_lblStatus);

            // Score bar background
            _scoreBar = new Panel
            {
                Location  = new Point(4, 218),
                Size      = new Size(230, 8),
                BackColor = Color.FromArgb(50, 50, 60)
            };
            Controls.Add(_scoreBar);

            // Score bar fill (resized dynamically)
            _scoreBarFill = new Panel
            {
                Location  = new Point(0, 0),
                Size      = new Size(0, 8),
                BackColor = Color.SeaGreen
            };
            _scoreBar.Controls.Add(_scoreBarFill);

            // Score percentage
            _lblScore = new Label
            {
                Location  = new Point(238, 215),
                Size      = new Size(46, 14),
                Font      = new Font("Segoe UI", 7, FontStyle.Bold),
                ForeColor = Color.Silver,
                Text      = "0 %"
            };
            Controls.Add(_lblScore);

            // Warning count badge
            _lblWarnings = new Label
            {
                Location  = new Point(240, 178),
                Size      = new Size(46, 18),
                Font      = new Font("Segoe UI", 7, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.FromArgb(60, 70, 90),
                Text      = "⚠ 0",
                TextAlign = ContentAlignment.MiddleCenter
            };
            Controls.Add(_lblWarnings);
        }

        /// <summary>
        /// Called every 500 ms from the admin's live-refresh timer.
        /// Copies the Bitmap reference from the entry (do NOT dispose it).
        /// </summary>
        public void UpdateTile(LiveFrameEntry entry)
        {
            if (IsDisposed) return;

            // Update camera frame
            if (entry.LatestFrame != null)
            {
                var old = _pic.Image;
                try
                {
                    // Clone via MemoryStream for cross-thread GDI+ safety
                    using (var ms = new MemoryStream())
                    {
                        entry.LatestFrame.Save(ms, System.Drawing.Imaging.ImageFormat.Bmp);
                        ms.Position = 0;
                        _pic.Image = new Bitmap(ms);
                    }
                }
                catch { /* frame momentarily unavailable — keep old */ }
                old?.Dispose();
            }

            // Name
            _lblName.Text = entry.StudentName ?? $"Session {entry.SessionID}";

            // Status & colours
            string status = entry.IsActive
                ? (entry.StatusLevel == "Normal" ? "✔ Normal" : $"⚠ {entry.StatusMessage}")
                : "● Session Ended";

            _lblStatus.Text      = status;
            _lblStatus.ForeColor = entry.IsActive
                ? (entry.StatusLevel == "Normal" ? Color.LightGreen : Color.OrangeRed)
                : Color.Gray;

            // Border colour
            bool hasAlert = entry.StatusLevel == "Warning" || entry.StatusLevel == "Critical";
            BackColor = hasAlert
                ? Color.FromArgb(60, 28, 28)
                : Color.FromArgb(32, 40, 52);

            // Warning badge
            _lblWarnings.Text      = $"⚠ {entry.WarningCount}";
            _lblWarnings.BackColor = entry.WarningCount > 0
                ? Color.FromArgb(180, 40, 40)
                : Color.FromArgb(60, 70, 90);

            // Score bar
            int score = Math.Max(0, Math.Min(100, entry.SuspicionScore));
            int fillW = (int)(_scoreBar.Width * score / 100.0);
            _scoreBarFill.Size      = new Size(Math.Max(0, fillW), 8);
            _scoreBarFill.BackColor = score >= 75 ? Color.Crimson
                                     : score >= 40 ? Color.DarkOrange
                                     :               Color.SeaGreen;
            _lblScore.Text      = $"{score} %";
            _lblScore.ForeColor = score >= 75 ? Color.Crimson
                                 : score >= 40 ? Color.DarkOrange
                                 :               Color.Silver;

            _lastStatusLevel = entry.StatusLevel;
            _lastScore       = score;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                var old = _pic?.Image;
                if (old != null) { _pic.Image = null; old.Dispose(); }
            }
            base.Dispose(disposing);
        }
    }
}
