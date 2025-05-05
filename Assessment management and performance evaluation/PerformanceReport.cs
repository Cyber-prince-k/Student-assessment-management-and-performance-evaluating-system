using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Text;

namespace Assessment_management_and_performance_evaluation
{
    internal class PerformanceReport
    {
        public int ReportID { get; set; }
        public List<string> WeakAreas { get; set; }
        public int Grades { get; set; }
        public int StudentID { get; set; }
        public string Subject { get; set; }

        public PerformanceReport(int studentId, string subject)
        {
            StudentID = studentId;
            Subject = subject;
            WeakAreas = new List<string>();
        }

        public void GenerateReport()
        {
            try
            {
                using (var conn = new SQLiteConnection("Data Source=assessment.db;Version=3;"))
                {
                    conn.Open();

                    // Get weak areas
                    GetWeakAreas(conn);

                    // Calculate overall grade
                    CalculateOverallGrade(conn);
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Error generating performance report: " + ex.Message);
            }
        }

        private void GetWeakAreas(SQLiteConnection conn)
        {
            const string query = @"
                SELECT 
                    q.QuestionText,
                    COUNT(CASE WHEN a.IsCorrect = 0 THEN 1 END) as WrongCount,
                    COUNT(*) as TotalAttempts
                FROM Answers a
                JOIN Questions q ON a.QuestionID = q.QuestionID
                WHERE a.StudentID = @StudentID
                AND q.questiontype IN ('Structured', 'Essay')
                GROUP BY q.QuestionID, q.QuestionText
                HAVING WrongCount > 0";

            using (var cmd = new SQLiteCommand(query, conn))
            {
                cmd.Parameters.AddWithValue("@StudentID", StudentID);

                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        string questionText = reader["QuestionText"].ToString();
                        int wrongCount = Convert.ToInt32(reader["WrongCount"]);
                        int totalAttempts = Convert.ToInt32(reader["TotalAttempts"]);
                        double failureRate = (wrongCount * 100.0) / totalAttempts;

                        if (failureRate > 50)
                        {
                            WeakAreas.Add($"{questionText} (Failure Rate: {failureRate:F1}%)");
                        }
                    }
                }
            }
        }

        private void CalculateOverallGrade(SQLiteConnection conn)
        {
            const string query = @"
                SELECT AVG(ag.MarksGiven * 100.0 / q.qmarks) as OverallGrade
                FROM AssessmentGrades ag
                JOIN Questions q ON ag.QuestionID = q.QuestionID
                WHERE ag.StudentID = @StudentID";

            using (var cmd = new SQLiteCommand(query, conn))
            {
                cmd.Parameters.AddWithValue("@StudentID", StudentID);
                var result = cmd.ExecuteScalar();
                Grades = result != DBNull.Value ? Convert.ToInt32(result) : 0;
            }
        }

        public string GetReportAsText()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"PERFORMANCE REPORT");
            sb.AppendLine($"==================");
            sb.AppendLine($"Student ID: {StudentID}");
            sb.AppendLine($"Subject: {Subject}");
            sb.AppendLine($"Overall Grade: {Grades}%");
            sb.AppendLine();
            sb.AppendLine("WEAK AREAS IDENTIFIED:");
            sb.AppendLine("----------------------");

            if (WeakAreas.Count > 0)
            {
                foreach (var area in WeakAreas)
                {
                    sb.AppendLine($"- {area}");
                }
            }
            else
            {
                sb.AppendLine("No significant weak areas identified");
            }

            return sb.ToString();
        }

    }
}