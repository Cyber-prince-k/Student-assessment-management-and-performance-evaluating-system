using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Data.SQLite;

namespace Assessment_management_and_performance_evaluation
{

    internal class Teacher
    {
        private int lectureID;
        private List<string> subjects;
        private string subjectRoot;
        private string assessmentTimeLimit; // ✅ Make sure it's a STRING

        public int LectureID { get => lectureID; set => lectureID = value; }
        public List<string> Subjects { get => subjects; set => subjects = value; }
        public string SubjectRoot { get => subjectRoot; set => subjectRoot = value; }
        public string AssessmentTimeLimit { get => assessmentTimeLimit; set => assessmentTimeLimit = value; } // ✅ Ensure this is a STRING

        public void GenerateQuestionStructure(string sectionType, Panel panel)
        {
            panel.Controls.Clear(); // Clear previous questions

            Label questionLabel = new Label
            {
                Text = "Enter Question:",
                AutoSize = true,
                Top = 10,
                Left = 10
            };
            panel.Controls.Add(questionLabel);

            TextBox questionBox = new TextBox
            {
                Width = 400,
                Top = 40,
                Left = 10,
                Tag = "Question"
            };
            panel.Controls.Add(questionBox);

            if (sectionType == "Multiple Choice")
            {
                Label instructionLabel = new Label
                {
                    Text = "Enter options and select the correct one:",
                    AutoSize = true,
                    Top = 80,
                    Left = 10
                };
                panel.Controls.Add(instructionLabel);

                // Create a group box for options
                GroupBox optionsGroup = new GroupBox
                {
                    Text = "Options",
                    Width = 450,
                    Height = 200,
                    Top = 110,
                    Left = 10
                };
                panel.Controls.Add(optionsGroup);

                for (int i = 1; i <= 4; i++) // Creating 4 options
                {
                    RadioButton optionRadio = new RadioButton
                    {
                        Text = $"Option {i}",
                        Top = 10 + (i * 30),
                        Left = 10,
                        AutoSize = true,
                        Tag = $"Option{i}"  // Unique tag for identifying the correct answer
                    };

                    TextBox optionTextBox = new TextBox
                    {
                        Width = 300,
                        Top = optionRadio.Top,
                        Left = 100,
                        Tag = $"Option{i}" // Tag for identifying option text fields
                    };

                    optionsGroup.Controls.Add(optionRadio);
                    optionsGroup.Controls.Add(optionTextBox);
                }
            }

            // Add input for marks
            Label marksLabel = new Label
            {
                Text = "Marks:",
                AutoSize = true,
                Top = panel.Controls[panel.Controls.Count - 1].Bottom + 10,
                Left = 10
            };
            panel.Controls.Add(marksLabel);

            NumericUpDown marksInput = new NumericUpDown
            {
                Width = 50,
                Top = marksLabel.Top,
                Left = 80,
                Tag = "Marks"
            };
            panel.Controls.Add(marksInput);
        }

        public void UpdateTotalMarks(long assessmentID)
        {
            try
            {
                using (SQLiteConnection conn = new SQLiteConnection("Data Source=assessment.db;Version=3;"))
                {
                    conn.Open();

                    // ✅ Recalculate Total Marks
                    string updateTotalMarksQuery = @"
                UPDATE Assessments 
                SET TotalMarks = (SELECT SUM(Marks) FROM Questions WHERE AssessmentID = @AssessmentID) 
                WHERE AssessmentID = @AssessmentID;";

                    using (SQLiteCommand cmd = new SQLiteCommand(updateTotalMarksQuery, conn))
                    {
                        cmd.Parameters.AddWithValue("@AssessmentID", assessmentID);
                        int rowsAffected = cmd.ExecuteNonQuery();

                        if (rowsAffected > 0)
                        {
                            MessageBox.Show("Total marks updated successfully!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }
                        else
                        {
                            MessageBox.Show("Failed to update total marks!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error updating total marks: " + ex.Message, "Database Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        private string connectionString = "Data Source=assessment.db;Version=3;";


        public int CreateAssessment(string title, int timeLimit, int classLevel, DateTime assessmentDateTime)
        {
            try
            {
                using (SQLiteConnection conn = new SQLiteConnection(connectionString))
                {
                    conn.Open();
                    string query = @"INSERT INTO Assessments 
                               (Title, TimeLimit, ClassLevel, TotalMarks, AssessmentDateTime) 
                               VALUES (@Title, @TimeLimit, @ClassLevel, 0, @AssessmentDateTime);
                               SELECT last_insert_rowid();";

                    using (SQLiteCommand cmd = new SQLiteCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@Title", title);
                        cmd.Parameters.AddWithValue("@TimeLimit", timeLimit);
                        cmd.Parameters.AddWithValue("@ClassLevel", classLevel);
                        cmd.Parameters.AddWithValue("@AssessmentDateTime", assessmentDateTime.ToString("yyyy-MM-dd HH:mm:ss"));

                        return Convert.ToInt32(cmd.ExecuteScalar());
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error creating assessment: " + ex.Message,
                              "Database Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return -1;
            }
        }
        public bool AddQuestion(int assessmentId, string questionText, string questionType,
                              int marks, string optionA, string optionB,
                              string optionC, string optionD, string correctAnswer)
        {
            try
            {
                using (SQLiteConnection conn = new SQLiteConnection(connectionString))
                {
                    conn.Open();
                    using (SQLiteTransaction transaction = conn.BeginTransaction())
                    {
                        try
                        {
                            // Insert the question with all required fields
                            string questionQuery = @"INSERT INTO Questions 
                                              (AssessmentID, QuestionText, questiontype, qmarks,
                                               OptionA, OptionB, OptionC, OptionD, CorrectAnswer) 
                                              VALUES (@AssessmentID, @QuestionText, @QuestionType, @Marks,
                                                      @OptionA, @OptionB, @OptionC, @OptionD, @CorrectAnswer)";

                            using (SQLiteCommand cmd = new SQLiteCommand(questionQuery, conn))
                            {
                                cmd.Parameters.AddWithValue("@AssessmentID", assessmentId);
                                cmd.Parameters.AddWithValue("@QuestionText", questionText);
                                cmd.Parameters.AddWithValue("@QuestionType", questionType);
                                cmd.Parameters.AddWithValue("@Marks", marks);
                                cmd.Parameters.AddWithValue("@OptionA", optionA);
                                cmd.Parameters.AddWithValue("@OptionB", optionB);
                                cmd.Parameters.AddWithValue("@OptionC", optionC);
                                cmd.Parameters.AddWithValue("@OptionD", optionD);
                                cmd.Parameters.AddWithValue("@CorrectAnswer", correctAnswer);

                                cmd.ExecuteNonQuery();
                            }

                            // Update total marks
                            string updateMarksQuery = @"UPDATE Assessments 
                                                  SET TotalMarks = (SELECT SUM(qmarks) 
                                                                  FROM Questions 
                                                                  WHERE AssessmentID = @AssessmentID) 
                                                  WHERE AssessmentID = @AssessmentID";

                            using (SQLiteCommand cmd = new SQLiteCommand(updateMarksQuery, conn))
                            {
                                cmd.Parameters.AddWithValue("@AssessmentID", assessmentId);
                                cmd.ExecuteNonQuery();
                            }

                            transaction.Commit();
                            return true;
                        }
                        catch
                        {
                            transaction.Rollback();
                            throw;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error adding question: " + ex.Message,
                               "Database Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }
    }
}

    

