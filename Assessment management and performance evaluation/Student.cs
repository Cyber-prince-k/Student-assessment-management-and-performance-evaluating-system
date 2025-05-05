using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data.SQLite;
using System.Windows.Forms;
using System.Drawing;
using iTextSharp.text.pdf;
using iTextSharp.text;
using System.IO;
using System.Net.Mail;
using System.Net;


namespace Assessment_management_and_performance_evaluation
{
    internal class Student
    {
        private string classLevel;
        private string stream;
        private int studentId; 

        public int StudentID { get => studentId; set => studentId = value; } 
        public string ClassLevel { get => classLevel; set => classLevel = value; }
        public string Stream { get => stream; set => stream = value; }

        
        public PerformanceReport WriteAssessment(int assessmentID, string assessmentName)
        {
            return new PerformanceReport(StudentID, assessmentName); // Now passes required args
        }

        public PerformanceReport ViewPerformanceReport()
        {
            return new PerformanceReport(StudentID, "General Performance"); // Example subject
        }

        public void SaveAnswer(int userId, int questionId, string answer)
        {
            try
            {
                using (SQLiteConnection conn = new SQLiteConnection("Data Source=assessment.db;Version=3;"))
                {
                    conn.Open();

                    string check = "SELECT COUNT(*) FROM Answers WHERE UserID = @userId AND QuestionID = @questionId";
                    using (SQLiteCommand cmd = new SQLiteCommand(check, conn))
                    {
                        cmd.Parameters.AddWithValue("@userId", userId);
                        cmd.Parameters.AddWithValue("@questionId", questionId);
                        int count = Convert.ToInt32(cmd.ExecuteScalar());

                        string query;
                        if (count > 0)
                        {
                            query = "UPDATE Answers SET Answer = @answer WHERE UserID = @userId AND QuestionID = @questionId";
                        }
                        else
                        {
                            query = "INSERT INTO Answers (UserID, QuestionID, Answer) VALUES (@userId, @questionId, @answer)";
                        }

                        using (SQLiteCommand updateCmd = new SQLiteCommand(query, conn))
                        {
                            updateCmd.Parameters.AddWithValue("@userId", userId);
                            updateCmd.Parameters.AddWithValue("@questionId", questionId);
                            updateCmd.Parameters.AddWithValue("@answer", answer);
                            updateCmd.ExecuteNonQuery();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error saving answer: " + ex.Message, "Save Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public string GetAnswerFromPanel(Panel panel, string questionType)
        {
            try
            {
                // Handle null questionType
                if (string.IsNullOrEmpty(questionType))
                {
                    MessageBox.Show("Question type not specified!", "Error",
                                  MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return string.Empty;
                }

                switch (questionType.ToLower())
                {
                    case "multiple choice":
                        var selectedRadio = panel.Controls.OfType<RadioButton>()
                                              .FirstOrDefault(r => r.Checked);
                        return selectedRadio?.Tag?.ToString() ?? string.Empty;

                    case "structured":
                        var textBox = panel.Controls.OfType<TextBox>()
                                          .FirstOrDefault(tb => tb.Tag?.ToString() == "Answer");
                        return textBox?.Text.Trim() ?? string.Empty;

                    case "essay":
                        var richTextBox = panel.Controls.OfType<RichTextBox>()
                                              .FirstOrDefault(rtb => rtb.Tag?.ToString() == "Answer");
                        return richTextBox?.Text.Trim() ?? string.Empty;

                    default:
                        MessageBox.Show($"Unknown question type: {questionType}", "Error",
                                      MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return string.Empty;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error getting answer: {ex.Message}", "Error",
                              MessageBoxButtons.OK, MessageBoxIcon.Error);
                return string.Empty;
            }
        }
        /* public byte[] GeneratePdfReport()
         {
             var subjectGrades = GetAllSubjectGrades();
             double overallAverage = subjectGrades.Values.Average();
             int position = GetClassPosition();

             using (MemoryStream ms = new MemoryStream())
             {
                 Document doc = new Document(PageSize.A4);
                 PdfWriter.GetInstance(doc, ms);
                 doc.Open();

                 // Title
                 doc.Add(new Paragraph("STUDENT SCHOOL REPORT", new iTextSharp.text.Font(iTextSharp.text.Font.FontFamily.HELVETICA, 18, iTextSharp.text.Font.BOLD)));
                 doc.Add(new Paragraph($"Generated on: {DateTime.Now.ToString("dd/MM/yyyy")}"));
                 doc.Add(Chunk.NEWLINE);

                 // Student Info
                 doc.Add(new Paragraph($"Student ID: {this.StudentID}"));
                 doc.Add(new Paragraph($"Class Level: {this.ClassLevel}"));
                 doc.Add(Chunk.NEWLINE);

                 // Grades Table
                 PdfPTable table = new PdfPTable(2);
                 table.AddCell("Subject");
                 table.AddCell("Grade (%)");
                 foreach (var subject in subjectGrades)
                 {
                     table.AddCell(subject.Key);
                     table.AddCell(subject.Value.ToString());
                 }
                 doc.Add(table);

                 // Summary
                 doc.Add(Chunk.NEWLINE);
                 doc.Add(new Paragraph($"Overall Average: {overallAverage:F1}%"));
                 doc.Add(new Paragraph($"Class Position: {position}"));
                 doc.Close();

                 return ms.ToArray();
             }
         }
         public void SendReportToParent(string parentEmail, string studentName)
         {
             try
             {
                 byte[] pdfBytes = GeneratePdfReport();
                 string subject = $"School Report for {studentName} (Term {DateTime.Now.Month}/2024)";
                 using (SmtpClient smtp = new SmtpClient("smtp.gmail.com", 587))
                 {
                     smtp.EnableSsl = true;
                     smtp.Credentials = new NetworkCredential("your-school@gmail.com", "your-password");
                     MailMessage mail = new MailMessage();
                     mail.From = new MailAddress("your-school@gmail.com");
                     mail.To.Add(parentEmail);
                     mail.Subject = subject;
                     mail.Body = "Please find the attached school report for your child.";
                     // Attach PDF
                     mail.Attachments.Add(new Attachment(new MemoryStream(pdfBytes), $"{studentName}_Report.pdf"));
                     smtp.Send(mail);
                 }
             }
             catch (Exception ex)
             {
                 throw new Exception($"Failed to send email: {ex.Message}");
             }
         }*/

        public byte[] GeneratePdfReport()
        {
            var assessmentPerformance = new Dictionary<string, (int Score, int MaxMarks)>();
            int totalStudentsInClass = 1;
            int position = 1;
            double overallAverage = 0;

            using (var conn = new SQLiteConnection("Data Source=assessment.db;Version=3;"))
            {
                conn.Open();

                // 1. Get student's performance by assessment
                string performanceQuery = @"
            SELECT 
                a.Title as AssessmentName,
                COALESCE(SUM(ag.MarksGiven), 0) as TotalScore,
                COALESCE(SUM(q.qmarks), 0) as TotalMarks
            FROM AssessmentGrades ag
            JOIN Questions q ON ag.QuestionID = q.QuestionID
            JOIN Assessments a ON q.AssessmentID = a.AssessmentID
            WHERE ag.StudentID = @StudentID
            GROUP BY a.AssessmentID, a.Title";

                using (var cmd = new SQLiteCommand(performanceQuery, conn))
                {
                    cmd.Parameters.AddWithValue("@StudentID", this.StudentID);
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string assessmentName = reader["AssessmentName"].ToString();
                            int score = Convert.ToInt32(reader["TotalScore"]);
                            int max = Convert.ToInt32(reader["TotalMarks"]);
                            assessmentPerformance[assessmentName] = (score, max);
                        }
                    }
                }

                // 2. Calculate overall average
                if (assessmentPerformance.Count > 0)
                {
                    double totalPercentage = 0;
                    int count = 0;
                    foreach (var item in assessmentPerformance)
                    {
                        if (item.Value.MaxMarks > 0)
                        {
                            totalPercentage += (item.Value.Score * 100.0) / item.Value.MaxMarks;
                            count++;
                        }
                    }
                    overallAverage = count > 0 ? totalPercentage / count : 0;
                }

                // 3. Get class ranking based on assessments
                string rankingQuery = @"
            SELECT 
                ag.StudentID,
                a.AssessmentID,
                SUM(ag.MarksGiven) as TotalScore,
                SUM(q.qmarks) as TotalMarks
            FROM AssessmentGrades ag
            JOIN Questions q ON ag.QuestionID = q.QuestionID
            JOIN Assessments a ON q.AssessmentID = a.AssessmentID
            GROUP BY ag.StudentID, a.AssessmentID";

                var studentAverages = new Dictionary<int, List<double>>();
                using (var cmd = new SQLiteCommand(rankingQuery, conn))
                {
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            int studentId = Convert.ToInt32(reader["StudentID"]);
                            int score = Convert.ToInt32(reader["TotalScore"]);
                            int max = Convert.ToInt32(reader["TotalMarks"]);

                            if (!studentAverages.ContainsKey(studentId))
                            {
                                studentAverages[studentId] = new List<double>();
                            }

                            if (max > 0)
                            {
                                studentAverages[studentId].Add((score * 100.0) / max);
                            }
                        }
                    }
                }

                // Calculate average for each student
                var rankedStudents = studentAverages
                    .Select(x => new {
                        StudentID = x.Key,
                        Average = x.Value.Count > 0 ? x.Value.Average() : 0
                    })
                    .OrderByDescending(x => x.Average)
                    .ToList();

                // Find current student's position
                var currentStudent = rankedStudents.FirstOrDefault(x => x.StudentID == this.StudentID);
                if (currentStudent != null)
                {
                    position = rankedStudents.IndexOf(currentStudent) + 1;
                }
                totalStudentsInClass = rankedStudents.Count > 0 ? rankedStudents.Count : 1;
            }

            // Generate PDF
            using (MemoryStream ms = new MemoryStream())
            {
                Document doc = new Document(PageSize.A4);
                PdfWriter.GetInstance(doc, ms);
                doc.Open();

                // Header
                doc.Add(new Paragraph("STUDENT ASSESSMENT REPORT",
                    new iTextSharp.text.Font(iTextSharp.text.Font.FontFamily.HELVETICA, 18, iTextSharp.text.Font.BOLD)));
                doc.Add(new Paragraph($"Student ID: {this.StudentID}"));
                doc.Add(new Paragraph($"Class Level: {this.ClassLevel}"));
                doc.Add(Chunk.NEWLINE);

                // Assessment Performance Table
                PdfPTable table = new PdfPTable(3);
                table.WidthPercentage = 100;

                table.AddCell(new PdfPCell(new Phrase("Assessment",
                    new iTextSharp.text.Font(iTextSharp.text.Font.FontFamily.HELVETICA, 10, iTextSharp.text.Font.BOLD))));
                table.AddCell(new PdfPCell(new Phrase("Score",
                    new iTextSharp.text.Font(iTextSharp.text.Font.FontFamily.HELVETICA, 10, iTextSharp.text.Font.BOLD))));
                table.AddCell(new PdfPCell(new Phrase("Percentage",
                    new iTextSharp.text.Font(iTextSharp.text.Font.FontFamily.HELVETICA, 10, iTextSharp.text.Font.BOLD))));

                foreach (var item in assessmentPerformance)
                {
                    table.AddCell(item.Key);
                    table.AddCell($"{item.Value.Score}/{item.Value.MaxMarks}");

                    double percentage = item.Value.MaxMarks > 0 ?
                        Math.Round((item.Value.Score * 100.0) / item.Value.MaxMarks, 1) : 0;
                    table.AddCell($"{percentage}%");
                }
                doc.Add(table);
                doc.Add(Chunk.NEWLINE);

                // Summary
                doc.Add(new Paragraph($"Overall Average: {overallAverage:F1}%"));
                doc.Add(new Paragraph($"Class Rank: {position} of {totalStudentsInClass}"));
                doc.Add(new Paragraph($"Report Date: {DateTime.Now:yyyy-MM-dd}"));

                doc.Close();
                return ms.ToArray();
            }
        }

        public void SendReportToParent(string parentEmail, string studentName)
        {
            try
            {
                // 1. Generate PDF report
                byte[] pdfBytes = GeneratePdfReport();
                string subject = $"{studentName} - School Assessment Report";

                // 2. Get student performance data from AssessmentGrades table
                var performanceData = new List<(string Assessment, int Score, int MaxMarks)>();
                using (var conn = new SQLiteConnection("Data Source=assessment.db;Version=3;"))
                {
                    conn.Open();
                    string query = @"
                SELECT 
                    a.Title as AssessmentName,
                    COALESCE(SUM(ag.MarksGiven), 0) as TotalScore,
                    COALESCE(SUM(q.qmarks), 0) as TotalMarks
                FROM AssessmentGrades ag
                JOIN Questions q ON ag.QuestionID = q.QuestionID
                JOIN Assessments a ON q.AssessmentID = a.AssessmentID
                WHERE ag.StudentID = @StudentID
                GROUP BY a.AssessmentID, a.Title";

                    using (var cmd = new SQLiteCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@StudentID", this.StudentID);
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                performanceData.Add((
                                    reader["AssessmentName"].ToString(),
                                    Convert.ToInt32(reader["TotalScore"]),
                                    Convert.ToInt32(reader["TotalMarks"])
                                ));
                            }
                        }
                    }
                }

                // 3. Create email body with performance data
                StringBuilder emailBody = new StringBuilder();
                emailBody.AppendLine($"<h2>{studentName}'s Assessment Report</h2>");
                emailBody.AppendLine("<table border='1' style='border-collapse:collapse; width:100%;'>");
                emailBody.AppendLine("<tr style='background-color:#f2f2f2;'>");
                emailBody.AppendLine("<th style='padding:8px; text-align:left;'>Assessment</th>");
                emailBody.AppendLine("<th style='padding:8px; text-align:left;'>Score</th>");
                emailBody.AppendLine("<th style='padding:8px; text-align:left;'>Percentage</th>");
                emailBody.AppendLine("</tr>");

                foreach (var item in performanceData)
                {
                    double percentage = item.MaxMarks > 0 ? Math.Round((item.Score * 100.0) / item.MaxMarks, 1) : 0;
                    emailBody.AppendLine("<tr>");
                    emailBody.AppendLine($"<td style='padding:8px;'>{item.Assessment}</td>");
                    emailBody.AppendLine($"<td style='padding:8px;'>{item.Score}/{item.MaxMarks}</td>");
                    emailBody.AppendLine($"<td style='padding:8px;'>{percentage}%</td>");
                    emailBody.AppendLine("</tr>");
                }

                emailBody.AppendLine("</table>");

                // 4. Send email
                using (var smtp = new SmtpClient("smtp.gmail.com", 587))
                {
                    smtp.EnableSsl = true;
                    smtp.Credentials = new NetworkCredential("princekamnga1@gmail.com", "bqvybghkgprijkcg");

                    var mail = new MailMessage();
                    mail.From = new MailAddress("princekamnga1@gmail.com");
                    mail.To.Add(parentEmail);
                    mail.Subject = subject;
                    mail.Body = emailBody.ToString();
                    mail.IsBodyHtml = true;
                    mail.Attachments.Add(new Attachment(new MemoryStream(pdfBytes), $"{studentName}_Assessment_Report.pdf"));

                    smtp.Send(mail);
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to send email: {ex.Message}");
            }
        }
        public Dictionary<string, int> GetAllSubjectGrades()
        {
            var grades = new Dictionary<string, int>();
            using (var conn = new SQLiteConnection("Data Source=assessment.db;Version=3;"))
            {
                conn.Open();
                string query = "SELECT Subject, Grade FROM Grades WHERE StudentID = @StudentID";
                using (var cmd = new SQLiteCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@StudentID", this.StudentID);
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            grades.Add(reader["Subject"].ToString(), Convert.ToInt32(reader["Grade"]));
                        }
                    }
                }
            }
            return grades;
        }

        public int GetClassPosition()
        {
            using (var conn = new SQLiteConnection("Data Source=assessment.db;Version=3;"))
            {
                conn.Open();
                string query = @"
                    SELECT s.UserID, AVG(g.Grade) as AvgGrade
                    FROM Students s
                    JOIN Grades g ON s.UserID = g.StudentID
                    WHERE s.ClassLevel = @ClassLevel AND s.Stream = @Stream
                    GROUP BY s.UserID
                    ORDER BY AvgGrade DESC";
                using (var cmd = new SQLiteCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@ClassLevel", this.ClassLevel);
                    cmd.Parameters.AddWithValue("@Stream", this.Stream);

                    using (var reader = cmd.ExecuteReader())
                    {
                        int position = 1;
                        while (reader.Read())
                        {
                            int userId = Convert.ToInt32(reader["UserID"]);
                            if (userId == this.StudentID)
                                return position;
                            position++;
                        }
                    }
                }
            }
            return -1; // Not found
        }
        private int GetTotalStudentsInClass()
        {
            // Implementation to get total students in the same class level
            using (var conn = new SQLiteConnection("Data Source=assessment.db;Version=3;"))
            {
                conn.Open();
                string query = "SELECT COUNT(*) FROM Students WHERE ClassLevel = @ClassLevel";
                using (var cmd = new SQLiteCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@ClassLevel", this.ClassLevel);
                    return Convert.ToInt32(cmd.ExecuteScalar());
                }
            }
        }

    }
}
    

