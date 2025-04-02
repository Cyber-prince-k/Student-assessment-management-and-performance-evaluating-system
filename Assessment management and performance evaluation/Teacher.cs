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

        public bool SaveAssessment(string assessmentTitle, int timeLimit, string classLevel, List<Question> questionsList)
        {

            using (SQLiteConnection conn = new SQLiteConnection(connectionString))
            {
                conn.Open();
                using (SQLiteTransaction transaction = conn.BeginTransaction())
                {
                    try
                    {
                        // ✅ Save Assessment First
                        string insertAssessmentQuery = "INSERT INTO Assessments (Title, TimeLimit, ClassLevel) VALUES (@Title, @TimeLimit, @ClassLevel);";
                        using (SQLiteCommand cmd = new SQLiteCommand(insertAssessmentQuery, conn))
                        {
                            cmd.Parameters.AddWithValue("@Title", assessmentTitle);
                            cmd.Parameters.AddWithValue("@TimeLimit", timeLimit);
                            cmd.Parameters.AddWithValue("@ClassLevel", classLevel);
                            cmd.ExecuteNonQuery();
                        }

                        // ✅ Get the Last Inserted Assessment ID
                        long assessmentId = conn.LastInsertRowId;

                        // ✅ Save Each Question and Its Options
                        foreach (var question in questionsList)
                        {
                            string insertQuestionQuery = "INSERT INTO Questions (AssessmentID, QuestionText) VALUES (@AssessmentID, @QuestionText);";
                            using (SQLiteCommand cmd = new SQLiteCommand(insertQuestionQuery, conn))
                            {
                                cmd.Parameters.AddWithValue("@AssessmentID", assessmentId);
                                cmd.Parameters.AddWithValue("@QuestionText", question.Text);
                                cmd.ExecuteNonQuery();
                            }

                            // ✅ Get the Last Inserted Question ID
                            long questionId = conn.LastInsertRowId;

                            // ✅ Insert Options
                            foreach (var option in question.Options)
                            {
                                string insertOptionQuery = "INSERT INTO Options (QuestionID, OptionText, IsCorrect) VALUES (@QuestionID, @OptionText, @IsCorrect);";
                                using (SQLiteCommand cmd = new SQLiteCommand(insertOptionQuery, conn))
                                {
                                    cmd.Parameters.AddWithValue("@QuestionID", questionId);
                                    cmd.Parameters.AddWithValue("@OptionText", option.Text);
                                    cmd.Parameters.AddWithValue("@IsCorrect", option.IsCorrect ? 1 : 0);
                                    cmd.ExecuteNonQuery();
                                }
                            }
                        }

                        // ✅ Commit Transaction
                        transaction.Commit();
                        return true;
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        MessageBox.Show("Error saving assessment: " + ex.Message, "Database Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return false;
                    }
                }
            }
        }

    }
}

    

