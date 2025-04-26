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
            Marks = 0; // Default marks to 0
        }

        public static void DisplayCurrentQuestion(Question q, Panel panel, Label lblProgress, int index, int total)
        {
            try
            {
                panel.Controls.Clear();

                // Display question text with marks
                Label questionLabel = new Label
                {
                    Text = $"{q.QuestionText} [{q.Marks} marks]",
                    AutoSize = true,
                    Font = new Font("Segoe UI", 12, FontStyle.Bold),
                    Location = new Point(10, 10),
                    MaximumSize = new Size(panel.Width - 20, 0)
                };
                panel.Controls.Add(questionLabel);

                int verticalPosition = questionLabel.Bottom + 10;

                // Show options if it's multiple choice
                if (q.QuestionType?.ToLower() == "multiple choice" && q.Options != null && q.Options.Count > 0)
                {
                    foreach (var option in q.Options)
                    {
                        RadioButton rb = new RadioButton
                        {
                            Text = option.Text,
                            AutoSize = true,
                            Location = new Point(30, verticalPosition),
                            Tag = option.Text,
                            Font = new Font("Segoe UI", 10)
                        };
                        panel.Controls.Add(rb);
                        verticalPosition += rb.Height + 10;
                    }
                }
                else // For essay/structured questions
                {
                    TextBox answerBox = new TextBox
                    {
                        Width = panel.Width - 40,
                        Height = 200, // Increased height for essay questions
                        Multiline = true,
                        ScrollBars = ScrollBars.Vertical,
                        Top = verticalPosition,
                        Left = 10,
                        Tag = "Answer",
                        Font = new Font("Segoe UI", 10)
                    };
                    panel.Controls.Add(answerBox);
                }

                // Display question number with marks
                lblProgress.Text = $"Question {index + 1} of {total} - {q.Marks} marks";
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

                    // Debug output
                    Console.WriteLine($"Loading questions for assessment ID: {assessmentId}");

                    string query = @"SELECT QuestionID, QuestionText, OptionA, OptionB, OptionC, OptionD, CorrectAnswer, qmarks, questiontype 
                          FROM Questions 
                          WHERE AssessmentID = @assessmentID";

                    using (SQLiteCommand cmd = new SQLiteCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@assessmentID", assessmentId);

                        using (SQLiteDataReader reader = cmd.ExecuteReader())
                        {
                            // Debug output
                            Console.WriteLine($"Found {reader.HasRows} rows");

                            while (reader.Read())
                            {
                                var question = new Question
                                {
                                    QuestionID = reader.GetInt32(reader.GetOrdinal("QuestionID")),
                                    QuestionText = reader.GetString(reader.GetOrdinal("QuestionText")),
                                    Options = new List<Option>()
                                };

                                // Load question type
                                if (!reader.IsDBNull(reader.GetOrdinal("questiontype")))
                                {
                                    question.QuestionType = reader.GetString(reader.GetOrdinal("questiontype"));
                                    Console.WriteLine($"Loaded question type for question {question.QuestionID}: {question.QuestionType}");
                                }
                                else
                                {
                                    Console.WriteLine($"No question type found for question {question.QuestionID}");
                                    question.QuestionType = "multiple choice"; // Default to multiple choice
                                }

                                // Load marks
                                if (!reader.IsDBNull(reader.GetOrdinal("qmarks")))
                                {
                                    question.Marks = reader.GetInt32(reader.GetOrdinal("qmarks"));
                                    Console.WriteLine($"Loaded marks for question {question.QuestionID}: {question.Marks}");
                                }
                                else
                                {
                                    Console.WriteLine($"No marks found for question {question.QuestionID}");
                                    question.Marks = 0;
                                }

                                // Add options if they exist
                                if (!reader.IsDBNull(reader.GetOrdinal("OptionA")))
                                {
                                    question.Options.Add(new Option
                                    {
                                        Text = reader.GetString(reader.GetOrdinal("OptionA")),
                                        IsCorrect = reader.GetString(reader.GetOrdinal("CorrectAnswer")) == reader.GetString(reader.GetOrdinal("OptionA"))
                                    });
                                }
                                if (!reader.IsDBNull(reader.GetOrdinal("OptionB")))
                                {
                                    question.Options.Add(new Option
                                    {
                                        Text = reader.GetString(reader.GetOrdinal("OptionB")),
                                        IsCorrect = reader.GetString(reader.GetOrdinal("CorrectAnswer")) == reader.GetString(reader.GetOrdinal("OptionB"))
                                    });
                                }
                                if (!reader.IsDBNull(reader.GetOrdinal("OptionC")))
                                {
                                    question.Options.Add(new Option
                                    {
                                        Text = reader.GetString(reader.GetOrdinal("OptionC")),
                                        IsCorrect = reader.GetString(reader.GetOrdinal("CorrectAnswer")) == reader.GetString(reader.GetOrdinal("OptionC"))
                                    });
                                }
                                if (!reader.IsDBNull(reader.GetOrdinal("OptionD")))
                                {
                                    question.Options.Add(new Option
                                    {
                                        Text = reader.GetString(reader.GetOrdinal("OptionD")),
                                        IsCorrect = reader.GetString(reader.GetOrdinal("CorrectAnswer")) == reader.GetString(reader.GetOrdinal("OptionD"))
                                    });
                                }

                                questions.Add(question);

                                // Debug output for each question
                                Console.WriteLine($"Loaded question ID: {question.QuestionID}");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading questions: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            return questions;
        }
    }

    // Define the Option Class
    internal class Option
    {
        public string Text { get; set; }
        public bool IsCorrect { get; set; } // True if correct option





    }
}
