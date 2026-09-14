using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace Assessment_management_and_performance_evaluation
{
    // =========================================================================
    // LiveFrameEntry
    // One entry held in the store per active exam session.
    // =========================================================================
    public class LiveFrameEntry
    {
        /// <summary>Exam session this entry belongs to.</summary>
        public int SessionID { get; set; }

        /// <summary>Database student identifier.</summary>
        public int StudentID { get; set; }

        /// <summary>Display name for the admin feed tiles.</summary>
        public string StudentName { get; set; }

        /// <summary>Latest annotated camera frame (thread-safe clone).</summary>
        public Bitmap LatestFrame { get; set; }

        /// <summary>UTC time the frame was last updated.</summary>
        public DateTime LastUpdated { get; set; }

        /// <summary>Suspicion score 0–100 from the last BehaviorResult.</summary>
        public int SuspicionScore { get; set; }

        /// <summary>Primary detected behavior label.</summary>
        public string StatusMessage { get; set; }

        /// <summary>"Normal" | "Warning" | "Critical"</summary>
        public string StatusLevel { get; set; }

        /// <summary>How many warnings the student has accumulated this session.</summary>
        public int WarningCount { get; set; }

        /// <summary>Whether the session is still running.</summary>
        public bool IsActive { get; set; } = true;
    }

    // =========================================================================
    // LiveFrameStore
    //
    // Thread-safe singleton.  The ProctoringEngine (student side) pushes frames
    // on every timer tick.  The Admin_dashboard polls this store to render
    // live tiles — no database round-trip required for the video feed.
    //
    // Usage:
    //   Push  → LiveFrameStore.Instance.Update(sessionId, frame, result, studentId, name)
    //   Poll  → LiveFrameStore.Instance.GetAll()
    //         → LiveFrameStore.Instance.GetEntry(sessionId)
    //   Close → LiveFrameStore.Instance.Remove(sessionId)
    // =========================================================================
    public sealed class LiveFrameStore
    {
        // ------------------------------------------------------------------
        // Singleton
        // ------------------------------------------------------------------
        private static readonly Lazy<LiveFrameStore> _instance =
            new Lazy<LiveFrameStore>(() => new LiveFrameStore());

        public static LiveFrameStore Instance => _instance.Value;

        private LiveFrameStore() { }

        // ------------------------------------------------------------------
        // Storage  (sessionId → entry)
        // ------------------------------------------------------------------
        private readonly ConcurrentDictionary<int, LiveFrameEntry> _sessions =
            new ConcurrentDictionary<int, LiveFrameEntry>();

        // ------------------------------------------------------------------
        // PUBLIC API
        // ------------------------------------------------------------------

        /// <summary>
        /// Push a new frame + behavior metadata for a running session.
        /// The Bitmap is cloned before storing so the caller can freely
        /// dispose their copy.
        /// </summary>
        public void Update(
            int        sessionId,
            Bitmap     frame,
            BehaviorResult result,
            int        studentId,
            string     studentName,
            int        warningCount)
        {
            // Determine status level string from severity
            string level;
            if (result == null || result.Severity == BehaviorSeverity.Normal || result.Severity == BehaviorSeverity.Low)
                level = "Normal";
            else if (result.Severity == BehaviorSeverity.Critical)
                level = "Critical";
            else
                level = "Warning";

            _sessions.AddOrUpdate(
                sessionId,
                // ADD  — create new entry
                _ =>
                {
                    var e = new LiveFrameEntry
                    {
                        SessionID    = sessionId,
                        StudentID    = studentId,
                        StudentName  = studentName ?? $"Student #{studentId}",
                        LatestFrame  = SafeClone(frame),
                        LastUpdated  = DateTime.UtcNow,
                        SuspicionScore = result?.SuspicionScore ?? 0,
                        StatusMessage  = result?.BehaviorType ?? "Monitoring",
                        StatusLevel    = level,
                        WarningCount   = warningCount,
                        IsActive       = true
                    };
                    return e;
                },
                // UPDATE — mutate existing entry
                (_, existing) =>
                {
                    // Dispose old frame to avoid GDI+ handle leak
                    var old = existing.LatestFrame;
                    existing.LatestFrame   = SafeClone(frame);
                    existing.LastUpdated   = DateTime.UtcNow;
                    existing.SuspicionScore = result?.SuspicionScore ?? existing.SuspicionScore;
                    existing.StatusMessage  = result?.BehaviorType ?? existing.StatusMessage;
                    existing.StatusLevel    = level;
                    existing.WarningCount   = warningCount;
                    existing.IsActive       = true;
                    old?.Dispose();
                    return existing;
                });
        }

        /// <summary>
        /// Mark a session as ended (keeps its last frame for post-exam review).
        /// </summary>
        public void MarkInactive(int sessionId)
        {
            if (_sessions.TryGetValue(sessionId, out var entry))
                entry.IsActive = false;
        }

        /// <summary>
        /// Fully remove a session from the store (call this when you want to
        /// free the frame memory too).
        /// </summary>
        public void Remove(int sessionId)
        {
            if (_sessions.TryRemove(sessionId, out var entry))
                entry.LatestFrame?.Dispose();
        }

        /// <summary>
        /// Returns a snapshot list of all entries (active + inactive).
        /// The caller receives shallow references; do NOT dispose the Bitmaps.
        /// </summary>
        public IReadOnlyList<LiveFrameEntry> GetAll()
        {
            return new List<LiveFrameEntry>(_sessions.Values);
        }

        /// <summary>Returns only active (currently running) sessions.</summary>
        public IReadOnlyList<LiveFrameEntry> GetActive()
        {
            var result = new List<LiveFrameEntry>();
            foreach (var entry in _sessions.Values)
                if (entry.IsActive) result.Add(entry);
            return result;
        }

        /// <summary>Get the entry for a specific session, or null.</summary>
        public LiveFrameEntry GetEntry(int sessionId)
        {
            _sessions.TryGetValue(sessionId, out var entry);
            return entry;
        }

        /// <summary>Total number of tracked sessions (active + ended).</summary>
        public int Count => _sessions.Count;

        // ------------------------------------------------------------------
        // HELPERS
        // ------------------------------------------------------------------

        /// <summary>
        /// Creates a thread-safe Bitmap clone.  Returns a dark placeholder
        /// if the source is null.
        /// </summary>
        private static Bitmap SafeClone(Bitmap source)
        {
            if (source == null)
                return CreateNoSignalBitmap();

            try
            {
                // Convert to MemoryStream and back — avoids GDI+ cross-thread issues
                using (var ms = new MemoryStream())
                {
                    source.Save(ms, ImageFormat.Bmp);
                    ms.Position = 0;
                    return new Bitmap(ms);
                }
            }
            catch
            {
                return CreateNoSignalBitmap();
            }
        }

        private static Bitmap CreateNoSignalBitmap()
        {
            var bmp = new Bitmap(320, 240);
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.FromArgb(20, 20, 30));
                using (var f = new Font("Segoe UI", 11, FontStyle.Bold))
                    g.DrawString("NO SIGNAL", f, Brushes.Gray, 110, 105);
            }
            return bmp;
        }
    }
}
