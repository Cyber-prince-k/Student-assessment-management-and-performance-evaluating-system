using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using static System.Windows.Forms.Design.AxImporter;
using System.Data.SQLite;



namespace Assessment_management_and_performance_evaluation
{
    internal class Question
    {


        public string Text { get; set; }
        public string Answer { get; set; }

        public int QuestionID { get; set; }
        public string QuestionText { get; set; }
        public string QuestionType { get; set; }
        public int Marks { get; set; }
        public List<Option> Options { get; set; }  // Store Options

        public Question()
        {
            Options = new List<Option>(); // Initialize options list
        }

        public static void DisplayCurrentQuestion(Question q, Panel panel, Label lblProgress, int index, int total)
        {
            try
            {
                panel.Controls.Clear();

                Label questionLabel = new Label
                {
                    Text = q.QuestionText,
                    AutoSize = true,
                    Font = new Font("Segoe UI", 12, FontStyle.Bold),
                    Location = new Point(10, 10)
                };
                panel.Controls.Add(questionLabel);

                // ✅ Show options if it's multiple choice
                if (q.QuestionType == "Multiple Choice" && q.Options != null && q.Options.Count > 0)
                {
                    int topOffset = questionLabel.Bottom + 20;
                    foreach (var option in q.Options)
                    {
                        RadioButton radio = new RadioButton
                        {
                            Text = option.Text,
                            AutoSize = true,
                            Location = new Point(10, topOffset)
                        };
                        panel.Controls.Add(radio);
                        topOffset += 30;
                    }
                }
                else // ✅ For essay or structured, use answer box
                {
                    TextBox answerBox = new TextBox
                    {
                        Width = 400,
                        Top = questionLabel.Bottom + 10,
                        Left = 10,
                        Tag = "Answer"
                    };
                    panel.Controls.Add(answerBox);
                }

                // ✅ Display question number
                lblProgress.Text = $"Question {index + 1} of {total}";
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error displaying question: " + ex.Message, "UI Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        public static List<Question> LoadQuestions(int assessmentId)
        {
            var questions = new List<Question>();

            try
            {
                using (SQLiteConnection conn = new SQLiteConnection("Data Source=assessment.db;Version=3;"))
                {
                    conn.Open();
                    string query = "SELECT * FROM Questions WHERE AssessmentID = @assessmentID";

                    using (SQLiteCommand cmd = new SQLiteCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@assessmentID", assessmentId);

                        using (SQLiteDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                questions.Add(new Question
                                {
                                    QuestionID = Convert.ToInt32(reader["QuestionID"]),
                                    QuestionText = reader["QuestionText"].ToString(),
                                    QuestionType = "Multiple Choice", // You can change dynamically if needed
                                    Marks = 0 // Also update if you store marks
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error loading questions: " + ex.Message, "Database Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            return questions;
        }

    }

    // ✅ Define the Option Class
    internal class Option
    {
        public string Text { get; set; }
        public bool IsCorrect { get; set; } // True if correct option
    


   
      
    }
}
