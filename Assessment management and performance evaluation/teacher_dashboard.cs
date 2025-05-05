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
using System.Net.Mail;
using System.Net;

namespace Assessment_management_and_performance_evaluation
{
    public partial class teacher_dashboard : Form
    {
        private Teacher teacher;
        private string currentSection = "";
        private bool sectionActive = false;
        private List<Question> questionsList = new List<Question>();
        private Panel AssessmentPanel;
        private int currentAssessmentId;
        private int currentStudentId;
        private Dictionary<int, NumericUpDown> questionMarkControls = new Dictionary<int, NumericUpDown>();
        private int totalAssessmentMarks = 0;
        private int currentQuestionIndex = 0;
        private List<(int QuestionId, string QuestionType, string QuestionText, string Answer, int MaxMarks, int? MarksGiven)> questions =
            new List<(int, string, string, string, int, int?)>();

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
                LoadAnsweredAssessments();
                UpdateDashboardAnalytics();
                LoadStudentsIntoComboBox();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading dashboard: {ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void LoadAnsweredAssessments()
        {
            try
            {
                using (var conn = new SQLiteConnection("Data Source=assessment.db;Version=3;"))
                {
                    conn.Open();

                    const string query = @"
                SELECT 
                    a.AssessmentID,
                    ans.StudentID,
                    s.FirstName || ' ' || s.LastName as StudentName,
                    a.Title as AssessmentTitle,
                    date(ans.SubmissionTime) as SubmissionDate,
                    COUNT(DISTINCT q.QuestionID) as TotalQuestions,
                    SUM(CASE WHEN ans.IsCorrect = 1 THEN q.qmarks ELSE 0 END) as TotalScore,
                    SUM(q.qmarks) as TotalMarks
                FROM Assessments a
                JOIN Answers ans ON a.AssessmentID = ans.AssessmentID
                JOIN Questions q ON ans.QuestionID = q.QuestionID
                JOIN Students s ON ans.StudentID = s.UserID
                GROUP BY a.AssessmentID, ans.StudentID, s.FirstName, s.LastName, a.Title, date(ans.SubmissionTime)
                ORDER BY date(ans.SubmissionTime) DESC";

                    using (var cmd = new SQLiteCommand(query, conn))
                    {
                        var dt = new DataTable();
                        using (var adapter = new SQLiteDataAdapter(cmd))
                        {
                            adapter.Fill(dt);
                        }

                        if (dt.Columns.Contains("SubmissionDate"))
                        {
                            foreach (DataRow row in dt.Rows)
                            {
                                if (row["SubmissionDate"] != DBNull.Value)
                                {
                                    if (DateTime.TryParse(row["SubmissionDate"].ToString(), out var parsedDate))
                                    {
                                        row["SubmissionDate"] = parsedDate.Date;
                                    }
                                }
                            }
                        }

                        AnsweredQue.DataSource = dt;

                        if (AnsweredQue.Columns.Contains("SubmissionDate"))
                        {
                            AnsweredQue.Columns["SubmissionDate"].DefaultCellStyle.Format = "yyyy-MM-dd";
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Error loading answered assessments: " + ex.Message);
            }
        }

        private void UpdateDashboardAnalytics()
        {
            try
            {
                using (var conn = new SQLiteConnection("Data Source=assessment.db;Version=3;"))
                {
                    conn.Open();

                    // 1. Get student performance data
                    const string performanceQuery = @"
                WITH RankedAssessments AS (
                    SELECT 
                        a.StudentID,
                        a.AssessmentID,
                        SUM(CASE WHEN a.IsCorrect = 1 THEN COALESCE(q.qmarks, 0) ELSE 0 END) as Score,
                        MAX(a.SubmissionTime) as SubmissionTime,
                        ROW_NUMBER() OVER (PARTITION BY a.StudentID ORDER BY MAX(a.SubmissionTime) DESC) as rn
                    FROM Answers a
                    JOIN Questions q ON a.QuestionID = q.QuestionID
                    GROUP BY a.StudentID, a.AssessmentID
                )
                SELECT 
                    s.UserID as StudentID,
                    s.FirstName || ' ' || s.LastName as StudentName,
                    COALESCE(r1.Score, 0) as RecentScore,
                    COALESCE(r2.Score, 0) as PreviousScore
                FROM Students s
                LEFT JOIN RankedAssessments r1 ON s.UserID = r1.StudentID AND r1.rn = 1
                LEFT JOIN RankedAssessments r2 ON s.UserID = r2.StudentID AND r2.rn = 2
                WHERE EXISTS (SELECT 1 FROM Answers WHERE StudentID = s.UserID)";

                    var studentScores = new List<(string Name, double Recent, double Previous)>();
                    var improvingStudents = new List<string>();
                    var nonImprovingStudents = new List<string>();

                    using (var cmd = new SQLiteCommand(performanceQuery, conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string studentName = reader["StudentName"].ToString();
                            double recentScore = reader["RecentScore"] == DBNull.Value ? 0 : Convert.ToDouble(reader["RecentScore"]);
                            double previousScore = reader["PreviousScore"] == DBNull.Value ? 0 : Convert.ToDouble(reader["PreviousScore"]);

                            studentScores.Add((studentName, recentScore, previousScore));

                            if (recentScore > previousScore)
                                improvingStudents.Add(studentName);
                            else if (recentScore <= previousScore)
                                nonImprovingStudents.Add(studentName);
                        }
                    }

                    // Update chart
                    chart1.Series.Clear();
                    var series = chart1.Series.Add("Student Performance");
                    series.ChartType = System.Windows.Forms.DataVisualization.Charting.SeriesChartType.Column;

                    foreach (var score in studentScores)
                    {
                        series.Points.AddXY(score.Name, score.Recent);
                    }

                    // Update text boxes
                    richTextBox1.Text = "Improving Students:\n\n" +
                        (improvingStudents.Count > 0 ? string.Join("\n", improvingStudents) : "No improving students found.");

                    richTextBox2.Text = "Non-Improving Students:\n\n" +
                        (nonImprovingStudents.Count > 0 ? string.Join("\n", nonImprovingStudents) : "No non-improving students found.");

                    // 2. Get trouble areas
                    const string troubleAreasQuery = @"
                SELECT 
                    q.QuestionText,
                    COUNT(DISTINCT a.StudentID) as TotalAttempts,
                    SUM(CASE WHEN COALESCE(a.IsCorrect, 0) = 0 THEN 1 ELSE 0 END) as FailCount,
                    CAST(SUM(CASE WHEN COALESCE(a.IsCorrect, 0) = 0 THEN 1 ELSE 0 END) AS FLOAT) * 100.0 / 
                    COUNT(DISTINCT a.StudentID) as FailureRate
                FROM Questions q
                LEFT JOIN Answers a ON q.QuestionID = a.QuestionID
                GROUP BY q.QuestionID, q.QuestionText
                HAVING FailureRate > 50 AND TotalAttempts > 0
                ORDER BY FailureRate DESC";

                    var troubleAreasLabel = new Label();
                    troubleAreasLabel.AutoSize = true;
                    troubleAreasLabel.Dock = DockStyle.Fill;
                    troubleAreasLabel.Text = "Trouble Areas:\n\n";

                    using (var cmd = new SQLiteCommand(troubleAreasQuery, conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string questionText = reader["QuestionText"].ToString();
                            double failureRate = Convert.ToDouble(reader["FailureRate"]);
                            troubleAreasLabel.Text += $"{questionText} (Failure Rate: {failureRate:F1}%)\n";
                        }
                    }

                    if (troubleAreasLabel.Text == "Trouble Areas:\n\n")
                    {
                        troubleAreasLabel.Text += "No significant trouble areas found.";
                    }

                    troubleAreas.Controls.Clear();
                    troubleAreas.Controls.Add(troubleAreasLabel);

                    // 3. Get passing students count
                    const string passingQuery = @"
                SELECT COUNT(DISTINCT a.StudentID) as PassingCount
                FROM Answers a
                JOIN Questions q ON a.QuestionID = q.QuestionID
                WHERE a.AssessmentID = (
                    SELECT AssessmentID 
                    FROM Assessments 
                    ORDER BY AssessmentDateTime DESC 
                    LIMIT 1
                )
                GROUP BY a.StudentID
                HAVING CAST(SUM(CASE WHEN a.IsCorrect = 1 THEN COALESCE(q.qmarks, 0) ELSE 0 END) AS FLOAT) * 100.0 / 
                       NULLIF(SUM(COALESCE(q.qmarks, 0)), 0) >= 50";

                    using (var cmd = new SQLiteCommand(passingQuery, conn))
                    {
                        var result = cmd.ExecuteScalar();
                        int passingCount = result == null || result == DBNull.Value ? 0 : Convert.ToInt32(result);
                        guna2HtmlLabel6.Text = $"Passing Students: {passingCount}";
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error updating dashboard analytics: {ex.Message}",
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
            try
            {
                if (AnsweredQue.SelectedRows.Count > 0)
                {
                    // Get selected assessment details
                    int studentId = Convert.ToInt32(AnsweredQue.SelectedRows[0].Cells["StudentID"].Value);
                    string assessmentTitle = AnsweredQue.SelectedRows[0].Cells["AssessmentTitle"].Value.ToString();

                    // Check if already marked
                    if (IsAssessmentMarked(studentId, assessmentTitle))
                    {
                        MessageBox.Show("This assessment has already been marked.",
                            "Already Marked", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    // If not marked, proceed with loading the assessment
                    currentAssessmentId = Convert.ToInt32(AnsweredQue.SelectedRows[0].Cells["AssessmentID"].Value);
                    currentStudentId = studentId;
                    totalAssessmentMarks = Convert.ToInt32(AnsweredQue.SelectedRows[0].Cells["TotalMarks"].Value);

                    // Clear existing controls and data
                    guna2ShadowPanel1.Controls.Clear();
                    questionMarkControls.Clear();
                    questions.Clear();
                    currentQuestionIndex = 0;

                    LoadQuestions();

                    if (questions.Count > 0)
                    {
                        ShowCurrentQuestion();
                        guna2ShadowPanel1.Visible = true;
                    }
                    else
                    {
                        MessageBox.Show("No structured or essay questions found in this assessment.",
                            "No Questions", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
                else
                {
                    MessageBox.Show("Please select an assessment to mark.",
                        "No Selection", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error selecting assessment: {ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private bool IsAssessmentMarked(int studentId, string assessmentTitle)
        {
            using (SQLiteConnection conn = new SQLiteConnection("Data Source=assessment.db;Version=3;"))
            {
                conn.Open();
                string query = @"
                    SELECT COUNT(*) 
                    FROM SubjectsMarked 
                    WHERE StudentID = @studentId 
                    AND AssessmentTitle = @title";

                using (SQLiteCommand cmd = new SQLiteCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@studentId", studentId);
                    cmd.Parameters.AddWithValue("@title", assessmentTitle);
                    return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
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

                // Question Counter Label
                Label counterLabel = new Label
                {
                    Text = $"Question {currentQuestionIndex + 1} of {questions.Count}",
                    Location = new Point(5, 5),
                    AutoSize = true,
                    Font = new Font("Segoe UI", 9, FontStyle.Bold)
                };
                questionPanel.Controls.Add(counterLabel);

                // Question Label
                Label questionLabel = new Label
                {
                    Text = $"({question.QuestionType}): {question.QuestionText}",
                    Location = new Point(5, 25),
                    Width = questionPanel.Width - 10,
                    Height = 50,
                    Font = new Font("Segoe UI", 10)
                };
                questionPanel.Controls.Add(questionLabel);

                // Answer Label
                Label answerLabel = new Label
                {
                    Text = $"Answer:\n{question.Answer}",
                    Location = new Point(5, 85),
                    Width = questionPanel.Width - 10,
                    Height = 80,
                    Font = new Font("Segoe UI", 9)
                };
                questionPanel.Controls.Add(answerLabel);

                // Max Marks Label
                Label maxMarksLabel = new Label
                {
                    Text = $"Maximum Marks: {question.MaxMarks}",
                    Location = new Point(5, 170),
                    AutoSize = true,
                    Font = new Font("Segoe UI", 9)
                };
                questionPanel.Controls.Add(maxMarksLabel);

                // Marks Input
                NumericUpDown markInput = new NumericUpDown
                {
                    Location = new Point(120, 168),
                    Width = 60,
                    Maximum = question.MaxMarks,
                    Minimum = 0,
                    Value = question.MarksGiven ?? 0
                };
                questionPanel.Controls.Add(markInput);
                questionMarkControls[question.QuestionId] = markInput;

                guna2ShadowPanel1.Controls.Add(questionPanel);
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

        private bool AreAllQuestionsMarked()
        {
            using (SQLiteConnection conn = new SQLiteConnection("Data Source=assessment.db;Version=3;"))
            {
                conn.Open();
                string query = @"
                    SELECT COUNT(*) as UnmarkedCount
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
                int marksObtained = 0;

                using (SQLiteConnection conn = new SQLiteConnection("Data Source=assessment.db;Version=3;"))
                {
                    conn.Open();
                    using (SQLiteTransaction transaction = conn.BeginTransaction())
                    {
                        try
                        {
                            foreach (var mark in questionMarkControls)
                            {
                                marksObtained += (int)mark.Value.Value;
                            }

                            string status = marksObtained >= (totalAssessmentMarks / 2) ? "Pass" : "Fail";

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

                            // Clear current assessment data
                            questions.Clear();
                            questionMarkControls.Clear();
                            currentQuestionIndex = 0;
                            guna2ShadowPanel1.Visible = false;
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

        private void guna2Button8_Click(object sender, EventArgs e)
        {
            try
            {
                // Get current question's marks input
                if (currentQuestionIndex >= 0 && currentQuestionIndex < questions.Count)
                {
                    var currentQuestion = questions[currentQuestionIndex];
                    var markInput = questionMarkControls[currentQuestion.QuestionId];

                    // Validate marks for current question
                    if (markInput.Value > currentQuestion.MaxMarks)
                    {
                        MessageBox.Show($"Marks cannot exceed the maximum marks ({currentQuestion.MaxMarks}) for this question.",
                            "Invalid Marks", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    // Calculate total marks that would be given
                    int totalGivenMarks = 0;
                    foreach (var mark in questionMarkControls)
                    {
                        if (mark.Key != currentQuestion.QuestionId)  // Add other questions' marks
                        {
                            totalGivenMarks += (int)mark.Value.Value;
                        }
                    }
                    totalGivenMarks += (int)markInput.Value;  // Add current question's marks

                    // Validate total marks
                    if (totalGivenMarks > totalAssessmentMarks)
                    {
                        MessageBox.Show($"Total marks ({totalGivenMarks}) cannot exceed the assessment total marks ({totalAssessmentMarks}).",
                            "Invalid Total Marks", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    // Save current question marks
                    SaveCurrentQuestionMarks(currentQuestion.QuestionId, markInput);

                    // Move to next question if available
                    if (currentQuestionIndex < questions.Count - 1)
                    {
                        currentQuestionIndex++;
                        ShowCurrentQuestion();
                    }
                    else if (AreAllQuestionsMarked())
                    {
                        // All questions are marked, calculate final grade
                        CalculateAndSaveFinalGrade();
                    }
                    else
                    {
                        MessageBox.Show("Please mark all questions before calculating final grade.",
                            "Marking Incomplete", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving marks: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void guna2Button9_Click(object sender, EventArgs e)
        {
            try
            {
                // Check if there is a previous question
                if (currentQuestionIndex > 0)
                {
                    // Save current question marks before moving
                    if (questions.Count > 0)
                    {
                        var currentQuestion = questions[currentQuestionIndex];
                        if (questionMarkControls.ContainsKey(currentQuestion.QuestionId))
                        {
                            var markInput = questionMarkControls[currentQuestion.QuestionId];

                            // Validate marks for current question
                            if (markInput.Value > currentQuestion.MaxMarks)
                            {
                                MessageBox.Show($"Marks cannot exceed the maximum marks ({currentQuestion.MaxMarks}) for this question.",
                                    "Invalid Marks", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                                return;
                            }

                            // Calculate total marks that would be given
                            int totalGivenMarks = 0;
                            foreach (var mark in questionMarkControls)
                            {
                                if (mark.Key != currentQuestion.QuestionId)
                                {
                                    totalGivenMarks += (int)mark.Value.Value;
                                }
                            }
                            totalGivenMarks += (int)markInput.Value;

                            // Validate total marks
                            if (totalGivenMarks > totalAssessmentMarks)
                            {
                                MessageBox.Show($"Total marks ({totalGivenMarks}) cannot exceed the assessment total marks ({totalAssessmentMarks}).",
                                    "Invalid Total Marks", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                                return;
                            }

                            SaveCurrentQuestionMarks(currentQuestion.QuestionId, markInput);
                        }
                    }

                    // Move to previous question
                    currentQuestionIndex--;
                    ShowCurrentQuestion();
                }
                else
                {
                    MessageBox.Show("This is the first question.",
                        "Navigation", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error navigating to previous question: {ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void label13_Click(object sender, EventArgs e)
        {

        }

        private void guna2ShadowPanel1_Paint(object sender, PaintEventArgs e)
        {

        }


        // Add this class within the teacher_dashboard class
        private class AssessmentEmailService
        {
            private string smtpAddress = "smtp.gmail.com";
            private int port = 587;
            private string emailFrom = "princekamnga1@gmail.com";
            private string password = "bqvybghkgprijkcg"; // Consider moving to config

            public void SendAssessmentReport(int studentId, string parentEmail)
            {
                try
                {
                    using (var mail = new MailMessage())
                    using (var smtp = new SmtpClient(smtpAddress, port))
                    {
                        mail.From = new MailAddress(emailFrom);
                        mail.To.Add(parentEmail);
                        mail.Subject = "Student Assessment Report";
                        mail.Body = GenerateEmailBody(studentId);
                        mail.IsBodyHtml = true;

                        // Attach PDF report
                        var pdfBytes = GeneratePdfReport(studentId);
                        mail.Attachments.Add(new Attachment(new MemoryStream(pdfBytes), "Report.pdf"));

                        smtp.Credentials = new NetworkCredential(emailFrom, password);
                        smtp.EnableSsl = true;
                        smtp.Send(mail);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error sending email: {ex.Message}", "Email Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }

            private string GenerateEmailBody(int studentId)
            {
                var studentInfo = GetStudentInfo(studentId);
                return $@"
            <h3>Dear Parent/Guardian,</h3>
            <p>Here is {studentInfo.Name}'s final assessment report for {DateTime.Now.Year}:</p>
            <ul>
                <li>Class Position: {studentInfo.ClassPosition}</li>
                <li>Overall Grade: {studentInfo.OverallGrade}</li>
                <li>Total Subjects: {studentInfo.Subjects.Count}</li>
            </ul>
            <p>See attached PDF for detailed marks breakdown.</p>
            <p>Best regards,<br/>School Administration</p>";
            }

            private byte[] GeneratePdfReport(int studentId)
            {
                var studentInfo = GetStudentInfo(studentId);
                var sb = new StringBuilder();

                sb.AppendLine($"Student Report - {studentInfo.Name}");
                sb.AppendLine($"Class: {studentInfo.ClassLevel}");
                sb.AppendLine($"Position: {studentInfo.ClassPosition}");
                sb.AppendLine("\nSubject Marks:");

                foreach (var subject in studentInfo.Subjects)
                {
                    sb.AppendLine($"{subject.Name}: {subject.Mark}/100");
                }

                return Encoding.UTF8.GetBytes(sb.ToString());
            }

            private StudentInfo GetStudentInfo(int studentId)
            {
                // Implement database query to get student details
                var info = new StudentInfo();
                using (var conn = new SQLiteConnection("Data Source=assessment.db;Version=3;"))
                {
                    conn.Open();
                    // Add actual database queries here
                }
                return info;
            }
        }

        // Add this nested class for student information
        private class StudentInfo
        {
            public string Name { get; set; }
            public string ClassLevel { get; set; }
            public int ClassPosition { get; set; }
            public decimal OverallGrade { get; set; }
            public List<SubjectMark> Subjects { get; set; } = new List<SubjectMark>();
        }

        private class SubjectMark
        {
            public string Name { get; set; }
            public decimal Mark { get; set; }
        }

        private AssessmentEmailService emailService = new AssessmentEmailService();


        private void btnSendReports_Click(object sender, EventArgs e)
        {
            if (AnsweredQue.SelectedRows.Count > 0)
            {
                var studentId = Convert.ToInt32(AnsweredQue.SelectedRows[0].Cells["StudentID"].Value);
                var parentEmail = GetParentEmail(studentId);
                emailService.SendAssessmentReport(studentId, parentEmail);
            }
        }

        private string GetParentEmail(int studentId)
        {
            try
            {
                using (var conn = new SQLiteConnection("Data Source=assessment.db;Version=3;"))
                {
                    conn.Open();
                    string query = "SELECT ParentEmail FROM Students WHERE UserID = @studentId";
                    using (var cmd = new SQLiteCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@studentId", studentId);
                        var result = cmd.ExecuteScalar();
                        return result?.ToString() ?? string.Empty;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error getting parent email: {ex.Message}", "Database Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return string.Empty;
            }
        }

        private void guna2Button5_Click(object sender, EventArgs e)
        {
            try
            {
                // 1. Validate selection
                if (guna2ComboBox2.SelectedItem == null)
                {
                    MessageBox.Show("Please select a student first.",
                                  "Selection Required",
                                  MessageBoxButtons.OK,
                                  MessageBoxIcon.Warning);
                    return;
                }

                // 2. Get selected student data
                var selectedStudent = (ComboBoxStudentItem)guna2ComboBox2.SelectedItem;

                // 3. Validate email
                if (string.IsNullOrWhiteSpace(selectedStudent.ParentEmail) ||
                    !IsValidEmail(selectedStudent.ParentEmail))
                {
                    MessageBox.Show($"Invalid parent email: {selectedStudent.ParentEmail}",
                                  "Validation Error",
                                  MessageBoxButtons.OK,
                                  MessageBoxIcon.Error);
                    return;
                }

                // 4. Create student and send report
                var student = new Student
                {
                    StudentID = selectedStudent.StudentID,
                    ClassLevel = selectedStudent.ClassLevel
                };

                // 5. Send report with progress indication
                Cursor.Current = Cursors.WaitCursor;
                student.SendReportToParent(
                    selectedStudent.ParentEmail,
                    selectedStudent.DisplayText.Replace($" (Class {selectedStudent.ClassLevel})", ""));
                Cursor.Current = Cursors.Default;

                // 6. Show success message
                MessageBox.Show($"Report sent to {selectedStudent.ParentEmail}",
                              "Success",
                              MessageBoxButtons.OK,
                              MessageBoxIcon.Information);
            }
            catch (InvalidCastException)
            {
                MessageBox.Show("Invalid student data format",
                              "System Error",
                              MessageBoxButtons.OK,
                              MessageBoxIcon.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}",
                              "Operation Failed",
                              MessageBoxButtons.OK,
                              MessageBoxIcon.Error);
            }
        }

        private bool IsValidEmail(string email)
        {
            try
            {
                var addr = new System.Net.Mail.MailAddress(email);
                return addr.Address == email;
            }
            catch
            {
                return false;
            }
        }
        private int GetClassLevelFromDisplayText(string displayText)
        {
            // Extract class level from text like "John Doe (Class 5)"
            var match = System.Text.RegularExpressions.Regex.Match(displayText, @"Class (\d+)");
            return match.Success ? int.Parse(match.Groups[1].Value) : 0;
        }

        private void LoadStudentsIntoComboBox()
        {
            guna2ComboBox2.Items.Clear();

            try
            {
                using (var conn = new SQLiteConnection("Data Source=assessment.db;Version=3;"))
                {
                    conn.Open();
                    string query = "SELECT StudentID, FirstName, LastName, ParentEmail, ClassLevel FROM Students";

                    using (var cmd = new SQLiteCommand(query, conn))
                    {
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                guna2ComboBox2.Items.Add(new ComboBoxStudentItem
                                {
                                    StudentID = Convert.ToInt32(reader["StudentID"]),
                                    DisplayText = $"{reader["FirstName"]} {reader["LastName"]} (Class {reader["ClassLevel"]})",
                                    ParentEmail = reader["ParentEmail"].ToString(),
                                    ClassLevel = reader["ClassLevel"].ToString()
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load students: {ex.Message}",
                              "Database Error",
                              MessageBoxButtons.OK,
                              MessageBoxIcon.Error);
            }
        }

        private void guna2ComboBox2_SelectedIndexChanged(object sender, EventArgs e)
        {

        }
        
        public class ComboBoxStudentItem
        {
            public int StudentID { get; set; }
            public string DisplayText { get; set; }
            public string ParentEmail { get; set; }
            public string ClassLevel { get; set; }
            public override string ToString() => DisplayText;
        }
    }


}

