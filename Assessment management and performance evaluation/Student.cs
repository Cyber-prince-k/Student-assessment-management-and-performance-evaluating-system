using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data.SQLite;
using System.Windows.Forms;
using System.Drawing;


namespace Assessment_management_and_performance_evaluation
{
    internal class Student
    {
        
            private string classLevel;
            private string stream;

            public string ClassLevel { get => classLevel; set => classLevel = value; }
            public string Stream { get => stream; set => stream = value; }

            public PerformanceReport WriteAssessment(int assessmentID, string assessmentName) { return new PerformanceReport(); }
            public PerformanceReport ViewPerformanceReport() { return new PerformanceReport(); }
            public AssistanceRequest RequestAssistance(AssistanceRequest request) { return request; }
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

        public string GetAnswerFromPanel(Panel panel)
        {
            try
            {
                var box = panel.Controls.OfType<TextBox>().FirstOrDefault(tb => tb.Tag?.ToString() == "Answer");
                return box?.Text.Trim();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error retrieving answer: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return string.Empty;
            }
        }

    }

}

