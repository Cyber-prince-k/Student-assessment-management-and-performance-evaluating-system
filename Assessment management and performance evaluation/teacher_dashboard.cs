using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data.SQLite;
using System.Windows.Forms;
using System.IO;

namespace Assessment_management_and_performance_evaluation
{
    public partial class teacher_dashboard : Form
    {
        private Teacher teacher;
        private string currentSection = "";
        private bool sectionActive = false;
        private List<Question> questionsList = new List<Question>(); // Store questions
        private Panel AssessmentPanel; // Panel for displaying assessment details

        public teacher_dashboard(int teacherId)
        {
            InitializeComponent();
            teacher = new Teacher();

            // Initialize and configure the AssessmentPanel
            AssessmentPanel = new Panel
            {
                Name = "AssessmentPanel",
                Dock = DockStyle.Fill,
                AutoScroll = true
            };
            this.Controls.Add(AssessmentPanel);
        }

        private void teacher_dashboard_Load(object sender, EventArgs e)
        {
            try
            {
                using (SQLiteConnection conn = new SQLiteConnection("Data Source=assessment.db;Version=3;"))
                {
                    conn.Open();
                    string query = @"
                        SELECT 
                            a.AssessmentID,
                            ans.StudentID,
                            s.FirstName || ' ' || s.LastName as StudentName,
                            a.Title as AssessmentTitle,
                            date(ans.SubmissionTime) as SubmissionDate,
                            COUNT(DISTINCT q.QuestionID) as TotalQuestions,
                            SUM(CASE WHEN ans.IsCorrect = 1 THEN q.qmarks ELSE 0 END) as TotalScore
                        FROM Assessments a
                        JOIN Answers ans ON a.AssessmentID = ans.AssessmentID
                        JOIN Questions q ON ans.QuestionID = q.QuestionID
                        JOIN Students s ON ans.StudentID = s.UserID
                        GROUP BY a.AssessmentID, ans.StudentID, s.FirstName, s.LastName, a.Title, date(ans.SubmissionTime)
                        ORDER BY date(ans.SubmissionTime) DESC";

                    using (SQLiteCommand cmd = new SQLiteCommand(query, conn))
                    {
                        // Create a DataTable to hold the results
                        DataTable dt = new DataTable();
                        using (SQLiteDataAdapter adapter = new SQLiteDataAdapter(cmd))
                        {
                            adapter.Fill(dt);
                        }

                        // Convert SubmissionDate strings to proper DateTime objects
                        if (dt.Columns.Contains("SubmissionDate"))
                        {
                            foreach (DataRow row in dt.Rows)
                            {
                                if (row["SubmissionDate"] != DBNull.Value)
                                {
                                    string dateStr = row["SubmissionDate"].ToString();
                                    if (DateTime.TryParse(dateStr, out DateTime parsedDate))
                                    {
                                        row["SubmissionDate"] = parsedDate.Date;
                                    }
                                }
                            }
                        }

                        // Bind the DataTable to the DataGridView
                        AnsweredQue.DataSource = dt;

                        // Format the columns for better display
                        AnsweredQue.AutoResizeColumns(DataGridViewAutoSizeColumnsMode.AllCells);
                        if (AnsweredQue.Columns.Contains("SubmissionDate"))
                        {
                            AnsweredQue.Columns["SubmissionDate"].DefaultCellStyle.Format = "yyyy-MM-dd";
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error loading answered assessments: " + ex.Message, 
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void guna2TextBox1_TextChanged(object sender, EventArgs e)
        {

        }

        private void guna2GroupBox1_Click(object sender, EventArgs e)
        {

        }

        private void label1_Click(object sender, EventArgs e)
        {

        }

        private void label2_Click(object sender, EventArgs e)
        {

        }

        private void guna2Button4_Click(object sender, EventArgs e)
        {

        }
        // Enable the combo box only when section ends
       
        private void guna2Button2_Click(object sender, EventArgs e)
        {
            if (!sectionActive)
            {
                MessageBox.Show("Please select a section before adding questions.");
                return;
            }

            string selectedSection = guna2ComboBox1.SelectedItem?.ToString();
            if (string.IsNullOrEmpty(selectedSection))
            {
                MessageBox.Show("Please select a section type before adding a question.");
                return;
            }

            // Get values from NumericUpDown
            int timeLimit = (int)guna2NumericUpDown1.Value; // Time in hours
            int selectedClass = (int)guna2NumericUpDown2.Value; // Class level

            // Convert timeLimit to string before assigning it
            teacher.AssessmentTimeLimit = timeLimit.ToString(); // Convert int to string

            // Generate question structure
            teacher.GenerateQuestionStructure(selectedSection, panelQuestions);

            // Store question in list
            Question newQuestion = new Question
            {
                QuestionText = "", // Empty for now, teacher will fill in
                QuestionType = selectedSection,
                Marks = 0
            };

            questionsList.Add(newQuestion);
        }

        private void section_end_Click(object sender, EventArgs e)
        {
            if (!sectionActive)
            {
                MessageBox.Show("No active section to end.");
                return;
            }

            sectionActive = false;
            currentSection = "";
            guna2ComboBox1.Enabled = true; // Enable combo box

            MessageBox.Show("Section ended. You can now select a new section.");
        }

        private void guna2ComboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (sectionActive)
            {
                MessageBox.Show("You must end the current section before selecting a new one.",
                                "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                guna2ComboBox1.SelectedIndex = -1; // Reset selection
                return;
            }

            if (guna2ComboBox1.SelectedItem != null)
            {
                currentSection = guna2ComboBox1.SelectedItem.ToString();
                sectionActive = true; // Mark section as active
                guna2ComboBox1.Enabled = false; // Disable combo box
                MessageBox.Show("You are now adding questions for: " + currentSection);
            }
        }
        private void guna2Button3_Click(object sender, EventArgs e)
        {
            if (!sectionActive)
            {
                MessageBox.Show("Please select a section before adding questions.");
                return;
            }

            if (lastAssessmentID == -1)
            {
                MessageBox.Show("Please save the assessment first before adding questions.",
                               "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Find controls
            TextBox questionBox = panelQuestions.Controls
                .OfType<TextBox>()
                .FirstOrDefault(tb => tb.Tag != null && tb.Tag.ToString() == "Question");

            NumericUpDown marksBox = panelQuestions.Controls
                .OfType<NumericUpDown>()
                .FirstOrDefault(n => n.Tag != null && n.Tag.ToString() == "Marks");

            // Validate inputs
            if (questionBox == null || string.IsNullOrWhiteSpace(questionBox.Text))
            {
                MessageBox.Show("Please enter a question before moving to the next one.",
                               "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (marksBox == null || marksBox.Value == 0)
            {
                MessageBox.Show("Please assign marks before moving to the next question.",
                               "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Prepare options - for non-MCQ questions, we'll use empty strings
            string optionA = "", optionB = "", optionC = "", optionD = "", correctAnswer = "";

            if (currentSection == "Multiple Choice")
            {
                if (!GetMultipleChoiceOptions(out optionA, out optionB, out optionC, out optionD, out correctAnswer))
                {
                    return; // Validation failed
                }
            }

            // Use Teacher class to save the question
            bool success = teacher.AddQuestion(
                lastAssessmentID,
                questionBox.Text,
                currentSection,
                (int)marksBox.Value,
                optionA,
                optionB,
                optionC,
                optionD,
                correctAnswer
            );

            if (success)
            {
                MessageBox.Show("Question added successfully!",
                               "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                panelQuestions.Controls.Clear();
                teacher.GenerateQuestionStructure(currentSection, panelQuestions);
            }
        }

        private bool GetMultipleChoiceOptions(out string optionA, out string optionB,
                                            out string optionC, out string optionD,
                                            out string correctAnswer)
        {
            optionA = optionB = optionC = optionD = correctAnswer = "";

            GroupBox optionsGroup = panelQuestions.Controls
                .OfType<GroupBox>()
                .FirstOrDefault(gb => gb.Text == "Options");

            if (optionsGroup == null)
            {
                MessageBox.Show("Options section not found!", "Error",
                               MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }

            var optionBoxes = optionsGroup.Controls
                .OfType<TextBox>()
                .Where(tb => tb.Tag != null && tb.Tag.ToString().StartsWith("Option"))
                .OrderBy(tb => tb.Tag.ToString())
                .ToList();

            var radioButtons = optionsGroup.Controls
                .OfType<RadioButton>()
                .OrderBy(rb => rb.Tag.ToString())
                .ToList();

            if (optionBoxes.Count != 4)
            {
                MessageBox.Show("Please enter all four multiple-choice options.",
                               "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            // Get option texts
            optionA = optionBoxes[0].Text.Trim();
            optionB = optionBoxes[1].Text.Trim();
            optionC = optionBoxes[2].Text.Trim();
            optionD = optionBoxes[3].Text.Trim();

            // Validate options aren't empty
            if (string.IsNullOrWhiteSpace(optionA) || string.IsNullOrWhiteSpace(optionB) ||
                string.IsNullOrWhiteSpace(optionC) || string.IsNullOrWhiteSpace(optionD))
            {
                MessageBox.Show("All multiple-choice options must be filled.",
                               "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            // Find correct answer
            var correctRadioButton = radioButtons.FirstOrDefault(rb => rb.Checked);
            if (correctRadioButton == null)
            {
                MessageBox.Show("Please select the correct answer.",
                               "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            // Get the text of the correct answer
            int correctIndex = radioButtons.IndexOf(correctRadioButton);
            correctAnswer = optionBoxes[correctIndex].Text.Trim();

            return true;
        }
        private List<Option> GetMultipleChoiceOptions()
        {
            GroupBox optionsGroup = panelQuestions.Controls
                .OfType<GroupBox>()
                .FirstOrDefault(gb => gb.Text == "Options");

            if (optionsGroup == null)
            {
                MessageBox.Show("Options section not found!", "Error",
                               MessageBoxButtons.OK, MessageBoxIcon.Error);
                return null;
            }

            var optionBoxes = optionsGroup.Controls
                .OfType<TextBox>()
                .Where(tb => tb.Tag != null && tb.Tag.ToString().StartsWith("Option"))
                .OrderBy(tb => tb.Tag.ToString())
                .ToList();

            var radioButtons = optionsGroup.Controls
                .OfType<RadioButton>()
                .OrderBy(rb => rb.Tag.ToString())
                .ToList();

            if (optionBoxes.Count != 4)
            {
                MessageBox.Show("Please enter all four multiple-choice options.",
                               "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return null;
            }

            List<Option> options = new List<Option>();
            for (int i = 0; i < optionBoxes.Count; i++)
            {
                if (string.IsNullOrWhiteSpace(optionBoxes[i].Text))
                {
                    MessageBox.Show("All multiple-choice options must be filled.",
                                   "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return null;
                }

                options.Add(new Option
                {
                    Text = optionBoxes[i].Text.Trim(),
                    IsCorrect = radioButtons[i].Checked
                });
            }

            if (!options.Any(o => o.IsCorrect))
            {
                MessageBox.Show("Please select the correct answer.",
                               "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return null;
            }

            return options;
        }
        private void guna2Button1_Click(object sender, EventArgs e)
        {
            // Get student details from form controls
            string firstName = txtFirstName.Text.Trim();
            string lastName = txtLastName.Text.Trim();
            string parentEmail = txtParentEmail.Text.Trim();
            string category = cmbCategory.SelectedItem?.ToString(); // Science or Humanities
            byte[] studentImage = ImageToByteArray(pictureBoxStudent.Image); // Use correct PictureBox name
            int classLevel = (int)guna2NumericUpDown3.Value;

            // Validate input fields
            if (string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(lastName) ||
                string.IsNullOrWhiteSpace(parentEmail) || string.IsNullOrWhiteSpace(category) || studentImage == null)
            {
                MessageBox.Show("All fields are required!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // Generate a temporary password
            string tempPassword = GenerateTemporaryPassword();

            // Create an instance of Account class to register student
            Account account = new Account();
            bool success = account.RegisterStudent(firstName, lastName, parentEmail, category, studentImage, tempPassword, classLevel);

            if (success)
            {
                MessageBox.Show("Student registered successfully!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                // Optionally clear form fields after successful registration
                txtFirstName.Clear();
                txtLastName.Clear();
                txtParentEmail.Clear();
                cmbCategory.SelectedIndex = -1;
                guna2NumericUpDown3.Value = 1; // Reset to default
                pictureBoxStudent.Image = null;
            }
            else
            {
                MessageBox.Show("Failed to register student!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // Method to convert image to byte array
        private byte[] ImageToByteArray(Image image)
        {
            if (image == null) return null;
            using (MemoryStream ms = new MemoryStream())
            {
                image.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                return ms.ToArray();
            }
            // Method to convert image to byte array
        }

        // Method to generate a temporary password
        private string GenerateTemporaryPassword()
        {
            return Guid.NewGuid().ToString("N").Substring(0, 8); // 8-character random password
        }

        
        private int lastAssessmentID = -1; // Store the last inserted AssessmentID
        private void btnSaveAssessment_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtAssessmentTitle.Text))
            {
                MessageBox.Show("Assessment title is required!", "Validation Error",
                               MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            int timeLimit = (int)guna2NumericUpDown1.Value;
            int classLevel = (int)guna2NumericUpDown2.Value;
            DateTime assessmentDateTime = guna2DateTimePicker1.Value;

            // Use Teacher class to save assessment with datetime
            lastAssessmentID = teacher.CreateAssessment(
                txtAssessmentTitle.Text,
                timeLimit,
                classLevel,
                assessmentDateTime
            );

            if (lastAssessmentID > 0)
            {
                MessageBox.Show("Assessment saved successfully! You can now add questions.",
                               "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);

                // Enable question adding
                sectionActive = true;
                guna2ComboBox1.Enabled = false;
            }
        }
        private List<Question> GetQuestionsFromForm()
        {
            List<Question> questions = new List<Question>();

            // Find the question text box
            TextBox questionBox = panelQuestions.Controls
                .OfType<TextBox>()
                .FirstOrDefault(tb => tb.Tag != null && tb.Tag.ToString() == "Question");

            // Find the marks input
            NumericUpDown marksBox = panelQuestions.Controls
                .OfType<NumericUpDown>()
                .FirstOrDefault(n => n.Tag != null && n.Tag.ToString() == "Marks");

            if (questionBox != null && marksBox != null && !string.IsNullOrWhiteSpace(questionBox.Text))
            {
                Question q = new Question
                {
                    QuestionText = questionBox.Text,
                    QuestionType = currentSection,
                    Marks = (int)marksBox.Value,
                    Options = new List<Option>()
                };

                // For multiple choice questions, collect options
                if (currentSection == "Multiple Choice")
                {
                    GroupBox optionsGroup = panelQuestions.Controls.OfType<GroupBox>().FirstOrDefault(gb => gb.Text == "Options");
                    if (optionsGroup != null)
                    {
                        var optionBoxes = optionsGroup.Controls.OfType<TextBox>()
                            .Where(tb => tb.Tag != null && tb.Tag.ToString().StartsWith("Option"))
                            .OrderBy(tb => tb.Tag.ToString())
                            .ToList();

                        var radioButtons = optionsGroup.Controls.OfType<RadioButton>()
                            .OrderBy(rb => rb.Tag.ToString())
                            .ToList();

                        for (int i = 0; i < optionBoxes.Count; i++)
                        {
                            q.Options.Add(new Option
                            {
                                Text = optionBoxes[i].Text,
                                IsCorrect = radioButtons[i].Checked
                            });
                        }
                    }
                }

                questions.Add(q);
            }

            return questions;
        }

        private void Std_Registraster_Click(object sender, EventArgs e)
        {

        }

        private void guna2Button1_Click_1(object sender, EventArgs e)
        {
            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Title = "Select a Student Picture";
                openFileDialog.Filter = "Image Files|*.jpg;*.jpeg;*.png;*.bmp;*.gif";

                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    // Load the selected image into the PictureBox
                    pictureBoxStudent.Image = Image.FromFile(openFileDialog.FileName);
                }
            }
        }

        private void guna2Button7_Click_1(object sender, EventArgs e)
        {
            if (AnsweredQue.SelectedRows.Count > 0)
            {
                currentAssessmentId = Convert.ToInt32(AnsweredQue.SelectedRows[0].Cells["AssessmentID"].Value);
                currentStudentId = Convert.ToInt32(AnsweredQue.SelectedRows[0].Cells["StudentID"].Value);
                
                // Clear existing controls and data
                guna2ShadowPanel1.Controls.Clear();
                questionMarkControls.Clear();
                questions.Clear();
                totalAssessmentMarks = 0;
                currentQuestionIndex = 0;

                LoadQuestions();

                if (questions.Count > 0)
                {
                    ShowCurrentQuestion();
                    // Switch to Assessment tab (index 2 for Assessment tab)
                    guna2TabControl1.SelectedIndex = 2;
                }
                else
                {
                    MessageBox.Show("No structured or essay questions found in this assessment.", 
                        "No Questions", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        }

        private void LoadQuestions()
        {
            using (SQLiteConnection conn = new SQLiteConnection("Data Source=assessment.db;Version=3;"))
            {
                conn.Open();
                string query = @"
                    SELECT 
                        q.QuestionID,
                        q.QuestionText, 
                        q.questiontype,
                        COALESCE(a.AnswerText, 'Not Answered') as AnswerText, 
                        q.qmarks as MaxMarks,
                        ag.MarksGiven
                    FROM Questions q
                    LEFT JOIN Answers a ON q.QuestionID = a.QuestionID 
                        AND a.StudentID = @studentId
                        AND a.AssessmentID = @assessmentId
                    LEFT JOIN AssessmentGrades ag ON q.QuestionID = ag.QuestionID
                        AND ag.StudentID = @studentId
                    WHERE q.AssessmentID = @assessmentId
                    AND q.questiontype IN ('Structured', 'Essay')
                    ORDER BY q.QuestionID";

                using (SQLiteCommand cmd = new SQLiteCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@assessmentId", currentAssessmentId);
                    cmd.Parameters.AddWithValue("@studentId", currentStudentId);

                    using (SQLiteDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            questions.Add((
                                Convert.ToInt32(reader["QuestionID"]),
                                reader["questiontype"].ToString(),
                                reader["QuestionText"].ToString(),
                                reader["AnswerText"].ToString(),
                                Convert.ToInt32(reader["MaxMarks"]),
                                reader["MarksGiven"] != DBNull.Value ? Convert.ToInt32(reader["MarksGiven"]) : (int?)null
                            ));
                            totalAssessmentMarks += Convert.ToInt32(reader["MaxMarks"]);
                        }
                    }
                }
            }
        }

        private void ShowCurrentQuestion()
        {
            if (currentQuestionIndex >= 0 && currentQuestionIndex < questions.Count)
            {
                var question = questions[currentQuestionIndex];
                guna2ShadowPanel1.Controls.Clear();

                // Question Panel
                Panel questionPanel = new Panel
                {
                    Location = new Point(10, 10),
                    Width = guna2ShadowPanel1.Width - 40,
                    Height = 200,
                    BorderStyle = BorderStyle.FixedSingle
                };

                // Question Label
                Label questionLabel = new Label
                {
                    Text = $"Question {currentQuestionIndex + 1} of {questions.Count} ({question.QuestionType}):\n{question.QuestionText}",
                    Location = new Point(5, 5),
                    Width = questionPanel.Width - 10,
                    Height = 50,
                    Font = new Font("Segoe UI", 10, FontStyle.Bold)
                };
                questionPanel.Controls.Add(questionLabel);

                // Answer Label
                Label answerLabel = new Label
                {
                    Text = $"Answer:\n{question.Answer}",
                    Location = new Point(5, 65),
                    Width = questionPanel.Width - 10,
                    Height = 80,
                    Font = new Font("Segoe UI", 9)
                };
                questionPanel.Controls.Add(answerLabel);

                // Max Marks Label
                Label maxMarksLabel = new Label
                {
                    Text = $"Maximum Marks: {question.MaxMarks}",
                    Location = new Point(5, 155),
                    AutoSize = true,
                    Font = new Font("Segoe UI", 9)
                };
                questionPanel.Controls.Add(maxMarksLabel);

                // Marks Input
                NumericUpDown markInput = new NumericUpDown
                {
                    Location = new Point(120, 155),
                    Width = 60,
                    Maximum = question.MaxMarks,
                    Minimum = 0,
                    Value = question.MarksGiven ?? 0
                };
                questionPanel.Controls.Add(markInput);
                questionMarkControls[question.QuestionId] = markInput;

                // Add Marks Button
                Button addMarksButton = new Button
                {
                    Text = "Add Marks",
                    Location = new Point(10, questionPanel.Bottom + 10),
                    Width = 120
                };
                addMarksButton.Click += (s, e) => {
                    // Save current question's marks
                    SaveCurrentQuestionMarks(question.QuestionId, markInput);

                    if (currentQuestionIndex < questions.Count - 1)
                    {
                        // Move to next question
                        currentQuestionIndex++;
                        ShowCurrentQuestion();
                    }
                    else if (AreAllQuestionsMarked())
                    {
                        // This is the last question and all questions are marked
                        CalculateAndSaveFinalGrade();
                    }
                };

                guna2ShadowPanel1.Controls.Add(questionPanel);
                guna2ShadowPanel1.Controls.Add(addMarksButton);
            }
        }

        private void SaveCurrentQuestionMarks(int questionId, NumericUpDown markInput)
        {
            using (SQLiteConnection conn = new SQLiteConnection("Data Source=assessment.db;Version=3;"))
            {
                conn.Open();
                string query = @"
                    INSERT OR REPLACE INTO AssessmentGrades 
                    (AssessmentID, QuestionID, StudentID, TeacherID, MarksGiven)
                    VALUES 
                    (@assessmentId, @questionId, @studentId, @teacherId, @marksGiven)";

                using (SQLiteCommand cmd = new SQLiteCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@assessmentId", currentAssessmentId);
                    cmd.Parameters.AddWithValue("@questionId", questionId);
                    cmd.Parameters.AddWithValue("@studentId", currentStudentId);
                    cmd.Parameters.AddWithValue("@teacherId", 1);
                    cmd.Parameters.AddWithValue("@marksGiven", (int)markInput.Value);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        private List<(int QuestionId, string QuestionType, string QuestionText, string Answer, int MaxMarks, int? MarksGiven)> questions = 
            new List<(int, string, string, string, int, int?)>();

        private bool AreAllQuestionsMarked()
        {
            using (SQLiteConnection conn = new SQLiteConnection("Data Source=assessment.db;Version=3;"))
            {
                conn.Open();
                string query = @"
                    SELECT COUNT(*) 
                    FROM Questions q
                    LEFT JOIN AssessmentGrades ag 
                        ON q.QuestionID = ag.QuestionID 
                        AND ag.StudentID = @studentId
                    WHERE q.AssessmentID = @assessmentId
                    AND q.questiontype IN ('Structured', 'Essay')
                    AND ag.MarksGiven IS NULL";

                using (SQLiteCommand cmd = new SQLiteCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@assessmentId", currentAssessmentId);
                    cmd.Parameters.AddWithValue("@studentId", currentStudentId);
                    int unmarkedCount = Convert.ToInt32(cmd.ExecuteScalar());
                    return unmarkedCount == 0;
                }
            }
        }

        private void CalculateAndSaveFinalGrade()
        {
            try
            {
                using (SQLiteConnection conn = new SQLiteConnection("Data Source=assessment.db;Version=3;"))
                {
                    conn.Open();
                    using (SQLiteTransaction transaction = conn.BeginTransaction())
                    {
                        try
                        {
                            // Calculate total marks obtained
                            string marksQuery = @"
                                SELECT SUM(MarksGiven) 
                                FROM AssessmentGrades 
                                WHERE AssessmentID = @assessmentId 
                                AND StudentID = @studentId";

                            int marksObtained = 0;
                            using (SQLiteCommand cmd = new SQLiteCommand(marksQuery, conn))
                            {
                                cmd.Parameters.AddWithValue("@assessmentId", currentAssessmentId);
                                cmd.Parameters.AddWithValue("@studentId", currentStudentId);
                                marksObtained = Convert.ToInt32(cmd.ExecuteScalar());
                            }

                            // Calculate pass/fail status
                            string status = marksObtained >= (totalAssessmentMarks / 2) ? "Pass" : "Fail";

                            // Save final grade
                            string resultQuery = @"
                                INSERT INTO SubjectsMarked 
                                (StudentID, AssessmentTitle, MarksObtained, TotalMarks, Status)
                                VALUES 
                                (@studentId, @title, @obtained, @total, @status)";

                            using (SQLiteCommand cmd = new SQLiteCommand(resultQuery, conn))
                            {
                                string assessmentTitle = AnsweredQue.SelectedRows[0].Cells["AssessmentTitle"].Value.ToString();
                                
                                cmd.Parameters.AddWithValue("@studentId", currentStudentId);
                                cmd.Parameters.AddWithValue("@title", assessmentTitle);
                                cmd.Parameters.AddWithValue("@obtained", marksObtained);
                                cmd.Parameters.AddWithValue("@total", totalAssessmentMarks);
                                cmd.Parameters.AddWithValue("@status", status);

                                cmd.ExecuteNonQuery();
                            }

                            transaction.Commit();

                            MessageBox.Show($"Assessment grading completed!\nTotal Marks: {marksObtained}/{totalAssessmentMarks}\nStatus: {status}", 
                                "Grading Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                MessageBox.Show($"Error saving final grade: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private int currentAssessmentId;
        private int currentStudentId;
        private Dictionary<int, NumericUpDown> questionMarkControls = new Dictionary<int, NumericUpDown>();
        private int totalAssessmentMarks = 0;
        private int currentQuestionIndex = 0;

        private void guna2Button8_Click(object sender, EventArgs e)
        {
            try
            {
                int marksObtained = 0;

                using (SQLiteConnection conn = new SQLiteConnection("Data Source=assessment.db;Version=3;"))
                {
                    conn.Open();
                    using (SQLiteTransaction transaction = conn.BeginTransaction())
                    {
                        try
                        {
                            // Save individual question grades
                            foreach (var questionMark in questionMarkControls)
                            {
                                string gradeQuery = @"
                                    INSERT OR REPLACE INTO AssessmentGrades 
                                    (AssessmentID, QuestionID, StudentID, TeacherID, MarksGiven)
                                    VALUES 
                                    (@assessmentId, @questionId, @studentId, @teacherId, @marksGiven)";

                                using (SQLiteCommand cmd = new SQLiteCommand(gradeQuery, conn))
                                {
                                    cmd.Parameters.AddWithValue("@assessmentId", currentAssessmentId);
                                    cmd.Parameters.AddWithValue("@questionId", questionMark.Key);
                                    cmd.Parameters.AddWithValue("@studentId", currentStudentId);
                                    cmd.Parameters.AddWithValue("@teacherId", 1); // Change to your actual teacher ID
                                    
                                    int marks = (int)questionMark.Value.Value;
                                    cmd.Parameters.AddWithValue("@marksGiven", marks);
                                    marksObtained += marks;

                                    cmd.ExecuteNonQuery();
                                }
                            }

                            // Calculate pass/fail status
                            string status = marksObtained >= (totalAssessmentMarks / 2) ? "Pass" : "Fail";

                            // Save overall assessment result
                            string resultQuery = @"
                                INSERT INTO SubjectsMarked 
                                (StudentID, AssessmentTitle, MarksObtained, TotalMarks, Status)
                                VALUES 
                                (@studentId, @title, @obtained, @total, @status)";

                            using (SQLiteCommand cmd = new SQLiteCommand(resultQuery, conn))
                            {
                                string assessmentTitle = AnsweredQue.SelectedRows[0].Cells["AssessmentTitle"].Value.ToString();
                                
                                cmd.Parameters.AddWithValue("@studentId", currentStudentId);
                                cmd.Parameters.AddWithValue("@title", assessmentTitle);
                                cmd.Parameters.AddWithValue("@obtained", marksObtained);
                                cmd.Parameters.AddWithValue("@total", totalAssessmentMarks);
                                cmd.Parameters.AddWithValue("@status", status);

                                cmd.ExecuteNonQuery();
                            }

                            transaction.Commit();

                            MessageBox.Show($"Assessment graded successfully!\nTotal Marks: {marksObtained}/{totalAssessmentMarks}\nStatus: {status}", 
                                "Grading Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                MessageBox.Show($"Error saving grades: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void label13_Click(object sender, EventArgs e)
        {

        }

        private void guna2ShadowPanel1_Paint(object sender, PaintEventArgs e)
        {

        }
    }
}