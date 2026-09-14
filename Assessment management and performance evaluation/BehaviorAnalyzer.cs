using System;
using System.Collections.Generic;
using System.IO;
using OpenCvSharp;

namespace Assessment_management_and_performance_evaluation
{
    // -------------------------------------------------------------------------
    // Severity levels for detected behaviors
    // -------------------------------------------------------------------------
    public enum BehaviorSeverity
    {
        Normal = 0,
        Low = 1,
        Medium = 2,
        High = 3,
        Critical = 4
    }

    // -------------------------------------------------------------------------
    // Result returned by BehaviorAnalyzer for a single analyzed frame
    // -------------------------------------------------------------------------
    public class BehaviorResult
    {
        /// <summary>Primary detected behavior label, or null when the frame is clean.</summary>
        public string BehaviorType { get; set; }

        /// <summary>Human-readable description shown in the admin UI.</summary>
        public string Description { get; set; }

        /// <summary>0.0 – 1.0 confidence the behavior was genuinely detected.</summary>
        public double Confidence { get; set; }

        /// <summary>Overall behavior risk severity for this frame.</summary>
        public BehaviorSeverity Severity { get; set; }

        /// <summary>Composite "suspicion score" 0–100 aggregated across all detectors.</summary>
        public int SuspicionScore { get; set; }

        /// <summary>All individual detector findings; there may be more than one per frame.</summary>
        public List<BehaviorFinding> Findings { get; set; } = new List<BehaviorFinding>();

        /// <summary>True when no suspicious behavior was found.</summary>
        public bool IsClean => string.IsNullOrEmpty(BehaviorType);
    }

    /// <summary>One discrete finding from a single detector sub-module.</summary>
    public class BehaviorFinding
    {
        public string DetectorName { get; set; }
        public string Label { get; set; }
        public double Confidence { get; set; }
        public BehaviorSeverity Severity { get; set; }
    }

    // =========================================================================
    // BehaviorAnalyzer
    // ML model class that runs multiple OpenCvSharp-based detectors on every
    // frame and aggregates them into a single BehaviorResult.
    //
    // Detectors implemented:
    //   1. Face Presence   – frontal + profile Haar cascades
    //   2. Multi-Person    – multiple frontal faces → unauthorized person
    //   3. Gaze Direction  – eye cascade + face-center offset heuristic
    //   4. Head Pose       – face bounding-box aspect-ratio & position
    //   5. Mouth / Talking – lower-face region motion delta (optical flow approx.)
    //   6. Object / Phone  – HOG people-less contour + rectangular dark object
    //   7. Posture         – face-size relative to frame (leaning/moving away)
    //   8. Lighting        – mean brightness check (covering camera / dark room)
    // =========================================================================
    public class BehaviorAnalyzer : IDisposable
    {
        // ------------------------------------------------------------------
        // Cascade classifiers (loaded once, reused every frame)
        // ------------------------------------------------------------------
        private readonly CascadeClassifier _faceCascade;
        private readonly CascadeClassifier _eyeCascade;
        private readonly CascadeClassifier _profileCascade;
        private readonly CascadeClassifier _mouthCascade;   // optional — gracefully absent

        // ------------------------------------------------------------------
        // Optical-flow state for mouth / talking detection
        // ------------------------------------------------------------------
        private Mat _prevMouthROI;          // grayscale mouth ROI from last frame
        private double _mouthMotionScore;   // accumulated inter-frame motion

        // ------------------------------------------------------------------
        // Smoothing buffers – rolling window avoids single-frame spikes
        // ------------------------------------------------------------------
        private readonly Queue<double> _suspicionHistory = new Queue<double>();
        private const int HISTORY_WINDOW = 8;

        // ------------------------------------------------------------------
        // Configuration thresholds (can be tuned without recompile)
        // ------------------------------------------------------------------
        private const double FACE_HORIZONTAL_OFFSET_RATIO = 0.32; // >32 % → looking away
        private const double FACE_MIN_SIZE_RATIO = 0.08;          // face < 8 % frame → too far
        private const double FACE_MAX_SIZE_RATIO = 0.70;          // face > 70 % frame → leaning in
        private const double MOUTH_MOTION_THRESHOLD = 18.0;       // pixel-diff mean for talking
        private const double BRIGHTNESS_LOW_THRESHOLD = 30.0;     // too dark
        private const double BRIGHTNESS_HIGH_THRESHOLD = 240.0;   // camera flash / covered
        private const double OBJECT_CONTOUR_MIN_AREA = 1800.0;    // min px² for suspicious object
        private const double HOG_RECT_ASPECT_MIN = 1.4;           // phone-like rectangle ratio

        public BehaviorAnalyzer()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;

            _faceCascade    = LoadCascade(Path.Combine(baseDir, "Models", "haarcascade_frontalface_default.xml"));
            _eyeCascade     = LoadCascade(Path.Combine(baseDir, "Models", "haarcascade_eye.xml"));
            _profileCascade = LoadCascade(Path.Combine(baseDir, "Models", "haarcascade_profileface.xml"));
            // mouth cascade is optional – if missing, talking detection falls back gracefully
            _mouthCascade   = LoadCascade(Path.Combine(baseDir, "Models", "haarcascade_smile.xml"));
        }

        private static CascadeClassifier LoadCascade(string path)
        {
            try
            {
                if (File.Exists(path))
                    return new CascadeClassifier(path);
            }
            catch { /* graceful – detector will be skipped */ }
            return null;
        }

        // ==================================================================
        // PUBLIC API
        // ==================================================================

        /// <summary>
        /// Analyzes a single BGR Mat frame and returns a consolidated
        /// BehaviorResult.  The frame is annotated in-place with colored
        /// overlays so it is ready for display.
        /// </summary>
        public BehaviorResult Analyze(Mat frame)
        {
            if (frame == null || frame.Empty())
                return BuildClean();

            var findings = new List<BehaviorFinding>();

            using (Mat gray = new Mat())
            {
                Cv2.CvtColor(frame, gray, ColorConversionCodes.BGR2GRAY);
                Cv2.EqualizeHist(gray, gray);

                // ---- Run all detectors ----------------------------------
                RunFaceDetector(frame, gray, findings);
                RunLightingCheck(gray, findings);
                RunPostureCheck(frame, gray, findings);
                RunObjectDetector(frame, gray, findings);
                RunMouthTalkingDetector(frame, gray, findings);
            }

            return BuildResult(frame, findings);
        }

        // ==================================================================
        // DETECTOR 1 + 2 + 3 + 4 — Face, Multi-Person, Gaze, Head Pose
        // ==================================================================
        private void RunFaceDetector(Mat frame, Mat gray, List<BehaviorFinding> findings)
        {
            if (_faceCascade == null) return;

            Rect[] faces = _faceCascade.DetectMultiScale(
                gray,
                scaleFactor: 1.1,
                minNeighbors: 5,
                flags: HaarDetectionTypes.ScaleImage,
                minSize: new OpenCvSharp.Size(70, 70));

            if (faces.Length == 0)
            {
                // Try profile cascade as fallback
                Rect[] profiles = _profileCascade?.DetectMultiScale(
                    gray, scaleFactor: 1.1, minNeighbors: 4,
                    minSize: new OpenCvSharp.Size(60, 60)) ?? new Rect[0];

                if (profiles.Length > 0)
                {
                    DrawAnnotation(frame, profiles[0], Scalar.Yellow, "HEAD TURNED");
                    findings.Add(new BehaviorFinding
                    {
                        DetectorName = "FaceDetector",
                        Label = "Head Turned / Looking Away",
                        Confidence = 0.82,
                        Severity = BehaviorSeverity.Medium
                    });
                }
                else
                {
                    Cv2.PutText(frame, "! NO FACE DETECTED",
                        new OpenCvSharp.Point(12, 38),
                        HersheyFonts.HersheySimplex, 0.75, Scalar.Red, 2);
                    findings.Add(new BehaviorFinding
                    {
                        DetectorName = "FaceDetector",
                        Label = "No Face Detected",
                        Confidence = 0.95,
                        Severity = BehaviorSeverity.High
                    });
                }
                return;
            }

            if (faces.Length > 1)
            {
                foreach (var f in faces)
                    DrawAnnotation(frame, f, Scalar.Red, "UNAUTHORIZED");
                findings.Add(new BehaviorFinding
                {
                    DetectorName = "MultiPersonDetector",
                    Label = "Multiple Persons Detected",
                    Confidence = Math.Min(0.97, 0.80 + faces.Length * 0.05),
                    Severity = BehaviorSeverity.Critical
                });
                return; // gaze/posture not meaningful when multiple people present
            }

            // Exactly one face — run gaze & posture sub-detectors
            Rect face = faces[0];
            RunGazeDetector(frame, gray, face, findings);
            RunHeadPoseDetector(frame, face, findings);

            // Draw face box (color depends on findings added so far)
            bool hasIssue = findings.Count > 0;
            Scalar faceColor = hasIssue ? Scalar.Orange : Scalar.LightGreen;
            string faceLabel = hasIssue ? "MONITORING" : "VERIFIED";
            DrawAnnotation(frame, face, faceColor, faceLabel);
        }

        // ==================================================================
        // DETECTOR 3 — Gaze Direction (eye cascade + center-offset heuristic)
        // ==================================================================
        private void RunGazeDetector(Mat frame, Mat gray, Rect face, List<BehaviorFinding> findings)
        {
            if (_eyeCascade == null) return;

            // Only look in top-half of face ROI (eyes are in upper portion)
            Rect upperFace = new Rect(face.X, face.Y, face.Width, face.Height / 2);
            using (Mat faceROI = new Mat(gray, upperFace))
            {
                Rect[] eyes = _eyeCascade.DetectMultiScale(
                    faceROI,
                    scaleFactor: 1.1,
                    minNeighbors: 3,
                    flags: HaarDetectionTypes.ScaleImage,
                    minSize: new OpenCvSharp.Size(18, 18));

                if (eyes.Length == 0)
                {
                    DrawAnnotation(frame, face, Scalar.Gold, "GAZE AWAY");
                    findings.Add(new BehaviorFinding
                    {
                        DetectorName = "GazeDetector",
                        Label = "Gaze Away / Eyes Not Visible",
                        Confidence = 0.74,
                        Severity = BehaviorSeverity.Medium
                    });
                    return;
                }

                // Estimate gaze using relative positions of detected eyes
                if (eyes.Length >= 2)
                {
                    int frameCenterX = frame.Width / 2;
                    int faceCenterX  = face.X + face.Width / 2;
                    int xOffset      = Math.Abs(faceCenterX - frameCenterX);

                    if (xOffset > frame.Width * FACE_HORIZONTAL_OFFSET_RATIO)
                    {
                        DrawAnnotation(frame, face, Scalar.Yellow, "LOOKING AWAY");
                        findings.Add(new BehaviorFinding
                        {
                            DetectorName = "GazeDetector",
                            Label = "Looking Away from Screen",
                            Confidence = Math.Min(0.95, 0.60 + xOffset / (double)frame.Width),
                            Severity = BehaviorSeverity.Medium
                        });
                    }
                    else
                    {
                        // Eyes detected and centered — draw green eye dots
                        foreach (var eye in eyes)
                        {
                            var center = new OpenCvSharp.Point(
                                face.X + eye.X + eye.Width / 2,
                                face.Y + face.Height / 2 + eye.Y + eye.Height / 2);
                            Cv2.Circle(frame, center, 4, Scalar.Cyan, 2);
                        }
                    }
                }
            }
        }

        // ==================================================================
        // DETECTOR 4 — Head Pose (face bounding box position and size)
        // ==================================================================
        private void RunHeadPoseDetector(Mat frame, Rect face, List<BehaviorFinding> findings)
        {
            double faceAreaRatio = (double)(face.Width * face.Height) / (frame.Width * frame.Height);

            if (faceAreaRatio < FACE_MIN_SIZE_RATIO)
            {
                findings.Add(new BehaviorFinding
                {
                    DetectorName = "HeadPoseDetector",
                    Label = "Student Too Far / Moved Away",
                    Confidence = 0.70,
                    Severity = BehaviorSeverity.Low
                });
            }
            else if (faceAreaRatio > FACE_MAX_SIZE_RATIO)
            {
                findings.Add(new BehaviorFinding
                {
                    DetectorName = "HeadPoseDetector",
                    Label = "Student Leaning In (Possible Cheating Aid)",
                    Confidence = 0.65,
                    Severity = BehaviorSeverity.Low
                });
            }
        }

        // ==================================================================
        // DETECTOR 5 — Posture (face vertical position in frame)
        // ==================================================================
        private void RunPostureCheck(Mat frame, Mat gray, List<BehaviorFinding> findings)
        {
            if (_faceCascade == null) return;

            Rect[] faces = _faceCascade.DetectMultiScale(
                gray, scaleFactor: 1.2, minNeighbors: 4,
                minSize: new OpenCvSharp.Size(50, 50));

            if (faces.Length != 1) return; // handled by face detector

            Rect face = faces[0];
            int faceCenterY = face.Y + face.Height / 2;

            // If face is in the bottom 30 % of frame, student may be slouching / putting head down
            if (faceCenterY > frame.Height * 0.70)
            {
                findings.Add(new BehaviorFinding
                {
                    DetectorName = "PostureDetector",
                    Label = "Head Down / Unusual Posture",
                    Confidence = 0.62,
                    Severity = BehaviorSeverity.Low
                });
                Cv2.PutText(frame, "HEAD DOWN", new OpenCvSharp.Point(face.X, face.Y - 8),
                    HersheyFonts.HersheySimplex, 0.55, Scalar.Orange, 1);
            }
        }

        // ==================================================================
        // DETECTOR 6 — Suspicious Object / Phone Detection
        // Approach: find rectangular dark contours that match a phone aspect
        // ratio in the lower-half of the frame (where hands typically are).
        // ==================================================================
        private void RunObjectDetector(Mat frame, Mat gray, List<BehaviorFinding> findings)
        {
            // Work on bottom 55 % of frame — hands / desk area
            int roiY     = (int)(frame.Height * 0.45);
            int roiH     = frame.Height - roiY;
            Rect roiRect = new Rect(0, roiY, frame.Width, roiH);

            using (Mat roi = new Mat(gray, roiRect))
            using (Mat blurred = new Mat())
            using (Mat edges = new Mat())
            {
                Cv2.GaussianBlur(roi, blurred, new OpenCvSharp.Size(5, 5), 0);
                Cv2.Canny(blurred, edges, 50, 150);

                OpenCvSharp.Point[][] contours;
                HierarchyIndex[] hierarchy;
                Cv2.FindContours(edges, out contours, out hierarchy,
                    RetrievalModes.External, ContourApproximationModes.ApproxSimple);

                foreach (var contour in contours)
                {
                    double area = Cv2.ContourArea(contour);
                    if (area < OBJECT_CONTOUR_MIN_AREA) continue;

                    RotatedRect rr = Cv2.MinAreaRect(contour);
                    float w = Math.Max(rr.Size.Width, rr.Size.Height);
                    float h = Math.Min(rr.Size.Width, rr.Size.Height);
                    if (h < 1) continue;

                    double aspectRatio = w / h;

                    if (aspectRatio >= HOG_RECT_ASPECT_MIN && aspectRatio <= 5.5)
                    {
                        // Draw indicator on main frame (offset by roiY)
                        var pts = rr.Points();
                        for (int i = 0; i < 4; i++)
                        {
                            Cv2.Line(frame,
                                new OpenCvSharp.Point((int)pts[i].X, roiY + (int)pts[i].Y),
                                new OpenCvSharp.Point((int)pts[(i + 1) % 4].X, roiY + (int)pts[(i + 1) % 4].Y),
                                Scalar.Magenta, 2);
                        }
                        Cv2.PutText(frame, "OBJECT DETECTED",
                            new OpenCvSharp.Point(10, roiY + 22),
                            HersheyFonts.HersheySimplex, 0.6, Scalar.Magenta, 2);

                        double confidence = Math.Min(0.88, 0.50 + area / 15000.0);
                        findings.Add(new BehaviorFinding
                        {
                            DetectorName = "ObjectDetector",
                            Label = "Suspicious Object / Phone Detected",
                            Confidence = confidence,
                            Severity = BehaviorSeverity.High
                        });
                        break; // report once per frame
                    }
                }
            }
        }

        // ==================================================================
        // DETECTOR 7 — Mouth / Talking Detection
        // Uses mouth cascade if available, otherwise falls back to frame-diff
        // on the lower-face ROI to measure lip movement.
        // ==================================================================
        private void RunMouthTalkingDetector(Mat frame, Mat gray, List<BehaviorFinding> findings)
        {
            if (_faceCascade == null) return;

            Rect[] faces = _faceCascade.DetectMultiScale(
                gray, scaleFactor: 1.2, minNeighbors: 5,
                minSize: new OpenCvSharp.Size(70, 70));

            if (faces.Length != 1) return;

            Rect face = faces[0];

            // Lower face ROI (mouth region: bottom third of face)
            int mouthY = face.Y + (face.Height * 2 / 3);
            int mouthH = face.Height / 3;
            if (mouthY + mouthH > gray.Height) return;

            Rect mouthRegion = new Rect(face.X, mouthY, face.Width, mouthH);

            // --- Try Haar mouth cascade first ---
            if (_mouthCascade != null)
            {
                using (Mat mROI = new Mat(gray, mouthRegion))
                {
                    Rect[] mouths = _mouthCascade.DetectMultiScale(
                        mROI, scaleFactor: 1.7, minNeighbors: 11,
                        minSize: new OpenCvSharp.Size(25, 15));

                    if (mouths.Length > 0)
                    {
                        Cv2.PutText(frame, "TALKING DETECTED",
                            new OpenCvSharp.Point(face.X, face.Y - 12),
                            HersheyFonts.HersheySimplex, 0.58, Scalar.Tomato, 2);
                        findings.Add(new BehaviorFinding
                        {
                            DetectorName = "MouthDetector",
                            Label = "Student Talking / Communicating",
                            Confidence = 0.72,
                            Severity = BehaviorSeverity.Medium
                        });
                        return;
                    }
                }
            }

            // --- Fallback: frame-diff motion in mouth ROI ---
            using (Mat currentMouthROI = new Mat(gray, mouthRegion))
            {
                if (_prevMouthROI != null && !_prevMouthROI.Empty() &&
                    _prevMouthROI.Size() == currentMouthROI.Size())
                {
                    using (Mat diff = new Mat())
                    {
                        Cv2.Absdiff(_prevMouthROI, currentMouthROI, diff);
                        Scalar mean = Cv2.Mean(diff);
                        _mouthMotionScore = _mouthMotionScore * 0.6 + mean.Val0 * 0.4; // EMA smoothing

                        if (_mouthMotionScore > MOUTH_MOTION_THRESHOLD)
                        {
                            Cv2.PutText(frame, "LIP MOVEMENT",
                                new OpenCvSharp.Point(face.X, face.Y - 12),
                                HersheyFonts.HersheySimplex, 0.55, Scalar.Orange, 1);
                            double confidence = Math.Min(0.80, _mouthMotionScore / 60.0);
                            findings.Add(new BehaviorFinding
                            {
                                DetectorName = "MouthDetector",
                                Label = "Lip Movement Detected (Possible Talking)",
                                Confidence = confidence,
                                Severity = BehaviorSeverity.Medium
                            });
                        }
                    }
                }

                // Store current ROI for next frame
                _prevMouthROI?.Dispose();
                _prevMouthROI = currentMouthROI.Clone();
            }
        }

        // ==================================================================
        // DETECTOR 8 — Lighting / Camera Obstruction
        // ==================================================================
        private void RunLightingCheck(Mat gray, List<BehaviorFinding> findings)
        {
            Scalar mean = Cv2.Mean(gray);
            double brightness = mean.Val0;

            if (brightness < BRIGHTNESS_LOW_THRESHOLD)
            {
                findings.Add(new BehaviorFinding
                {
                    DetectorName = "LightingDetector",
                    Label = "Camera Covered / Very Dark Environment",
                    Confidence = Math.Min(0.95, 1.0 - brightness / BRIGHTNESS_LOW_THRESHOLD),
                    Severity = BehaviorSeverity.High
                });
            }
            else if (brightness > BRIGHTNESS_HIGH_THRESHOLD)
            {
                findings.Add(new BehaviorFinding
                {
                    DetectorName = "LightingDetector",
                    Label = "Abnormal Bright Light / Flash",
                    Confidence = 0.60,
                    Severity = BehaviorSeverity.Low
                });
            }
        }

        // ==================================================================
        // RESULT AGGREGATION
        // ==================================================================
        private BehaviorResult BuildResult(Mat frame, List<BehaviorFinding> findings)
        {
            if (findings.Count == 0)
            {
                // Draw a clean status banner
                Cv2.PutText(frame, "PROCTORING: NORMAL",
                    new OpenCvSharp.Point(8, frame.Height - 10),
                    HersheyFonts.HersheySimplex, 0.5, Scalar.LightGreen, 1);
                return BuildClean();
            }

            // Pick highest-severity finding as primary
            BehaviorFinding primary = findings[0];
            foreach (var f in findings)
                if (f.Severity > primary.Severity) primary = f;

            // Compute suspicion score: weighted sum of (confidence × severity_weight)
            double rawScore = 0;
            foreach (var f in findings)
            {
                double weight = (double)f.Severity; // 1-4
                rawScore += f.Confidence * weight * 25.0;
            }
            rawScore = Math.Min(100, rawScore);

            // Smooth over rolling window
            _suspicionHistory.Enqueue(rawScore);
            if (_suspicionHistory.Count > HISTORY_WINDOW)
                _suspicionHistory.Dequeue();
            double smoothed = 0;
            foreach (double v in _suspicionHistory) smoothed += v;
            smoothed /= _suspicionHistory.Count;

            int finalScore = (int)Math.Round(smoothed);

            // Overlay score bar on frame
            DrawScoreBar(frame, finalScore, primary.Severity);

            return new BehaviorResult
            {
                BehaviorType   = primary.Label,
                Description    = BuildDescription(findings),
                Confidence     = primary.Confidence,
                Severity       = primary.Severity,
                SuspicionScore = finalScore,
                Findings       = findings
            };
        }

        private static BehaviorResult BuildClean() =>
            new BehaviorResult
            {
                BehaviorType   = null,
                Description    = "Normal behavior",
                Confidence     = 1.0,
                Severity       = BehaviorSeverity.Normal,
                SuspicionScore = 0
            };

        private static string BuildDescription(List<BehaviorFinding> findings)
        {
            var parts = new List<string>();
            foreach (var f in findings)
                parts.Add($"{f.Label} ({(int)(f.Confidence * 100)}%)");
            return string.Join("; ", parts);
        }

        // ==================================================================
        // VISUAL OVERLAY HELPERS
        // ==================================================================
        private static void DrawAnnotation(Mat frame, Rect rect, Scalar color, string label)
        {
            Cv2.Rectangle(frame, rect, color, 2);
            OpenCvSharp.Point textPt = new OpenCvSharp.Point(rect.X, Math.Max(18, rect.Y - 8));
            Cv2.PutText(frame, label, textPt, HersheyFonts.HersheySimplex, 0.55, color, 2);
        }

        /// <summary>Draws a color-coded suspicion score bar at the bottom of the frame.</summary>
        private static void DrawScoreBar(Mat frame, int score, BehaviorSeverity severity)
        {
            int barWidth = (int)(frame.Width * score / 100.0);
            int barY     = frame.Height - 8;

            // Background
            Cv2.Rectangle(frame,
                new Rect(0, barY, frame.Width, 8),
                Scalar.FromRgb(40, 40, 40), -1);

            // Filled bar
            Scalar barColor;
            if (severity == BehaviorSeverity.Critical)
                barColor = Scalar.Red;
            else if (severity == BehaviorSeverity.High)
                barColor = Scalar.OrangeRed;
            else if (severity == BehaviorSeverity.Medium)
                barColor = Scalar.Orange;
            else if (severity == BehaviorSeverity.Low)
                barColor = Scalar.Yellow;
            else
                barColor = Scalar.LightGreen;
            if (barWidth > 0)
                Cv2.Rectangle(frame, new Rect(0, barY, barWidth, 8), barColor, -1);

            // Score text
            Cv2.PutText(frame, $"Risk: {score}%",
                new OpenCvSharp.Point(4, frame.Height - 12),
                HersheyFonts.HersheySimplex, 0.42, Scalar.White, 1);
        }

        // ==================================================================
        // IDisposable
        // ==================================================================
        private bool _disposed;
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _faceCascade?.Dispose();
            _eyeCascade?.Dispose();
            _profileCascade?.Dispose();
            _mouthCascade?.Dispose();
            _prevMouthROI?.Dispose();
        }
    }
}
