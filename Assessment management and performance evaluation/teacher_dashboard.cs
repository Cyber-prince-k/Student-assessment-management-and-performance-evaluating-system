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
        private int loggedInUserId;
        private string currentSection = "";
        private bool sectionActive = false;
        private List<Question> questionsList = new List<Question>(); // Store questions
       // private List<Panel> questionPanels = new List<Panel>();




        public teacher_dashboard(int userId)
        {
            InitializeComponent();
            loggedInUserId = userId;
            teacher = new Teacher();
        }

        private void teacher_dashboard_Load(object sender, EventArgs e)
        {

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

            // ✅ Get values from NumericUpDown
            int timeLimit = (int)guna2NumericUpDown1.Value; // Time in hours
            int selectedClass = (int)guna2NumericUpDown2.Value; // Class level

            // ✅ Convert timeLimit to string before assigning it
            teacher.AssessmentTimeLimit = timeLimit.ToString(); // Convert int to string

            // ✅ Generate question structure
            teacher.GenerateQuestionStructure(selectedSection, panelQuestions);

            // ✅ Store question in list
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
                MessageBox.Show("Please save the assessment first before adding questions.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Find the Question TextBox and Marks NumericUpDown
            TextBox questionBox = panelQuestions.Controls
                .OfType<TextBox>()
                .FirstOrDefault(tb => tb.Tag != null && tb.Tag.ToString() == "Question");

            NumericUpDown marksBox = panelQuestions.Controls
                .OfType<NumericUpDown>()
                .FirstOrDefault();

            if (questionBox == null || string.IsNullOrWhiteSpace(questionBox.Text))
            {
                MessageBox.Show("Please enter a question before moving to the next one.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (marksBox == null || marksBox.Value == 0)
            {
                MessageBox.Show("Please assign marks before moving to the next question.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string optionA = "", optionB = "", optionC = "", optionD = "", correctAnswer = "";

            // Handle Multiple Choice Questions
            if (currentSection == "Multiple Choice")
            {
                // Find the GroupBox containing the options
                GroupBox optionsGroup = panelQuestions.Controls.OfType<GroupBox>().FirstOrDefault(gb => gb.Text == "Options");

                if (optionsGroup == null)
                {
                    MessageBox.Show("Options section not found!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                // Search for TextBox controls inside the GroupBox
                var optionBoxes = optionsGroup.Controls.OfType<TextBox>()
                    .Where(tb => tb.Tag != null && tb.Tag.ToString().StartsWith("Option"))
                    .OrderBy(tb => tb.Tag.ToString())
                    .ToList();

                // Debugging: Display the count of optionBoxes found
                MessageBox.Show($"Debug: Found {optionBoxes.Count} option textboxes.", "Debug", MessageBoxButtons.OK, MessageBoxIcon.Information);

                foreach (var tb in optionBoxes)
                {
                    MessageBox.Show($"Tag: {tb.Tag}, Value: {tb.Text}", "Debug", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                // Ensure exactly 4 options exist
                if (optionBoxes.Count != 4)
                {
                    MessageBox.Show($"Found {optionBoxes.Count} option textboxes instead of 4. Please enter all four multiple-choice options before proceeding.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                // Assign values properly
                optionA = optionBoxes[0].Text.Trim();
                optionB = optionBoxes[1].Text.Trim();
                optionC = optionBoxes[2].Text.Trim();
                optionD = optionBoxes[3].Text.Trim();

                // Ensure no empty options
                if (string.IsNullOrWhiteSpace(optionA) || string.IsNullOrWhiteSpace(optionB) ||
                    string.IsNullOrWhiteSpace(optionC) || string.IsNullOrWhiteSpace(optionD))
                {
                    MessageBox.Show("All multiple-choice options must be filled before proceeding.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                // Find the selected correct answer
                var correctRadioButton = optionsGroup.Controls.OfType<RadioButton>()
                    .FirstOrDefault(rb => rb.Checked);

                if (correctRadioButton == null)
                {
                    MessageBox.Show("Please select the correct answer.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                // Match the correct answer with its corresponding textbox
                correctAnswer = optionBoxes.FirstOrDefault(tb => tb.Tag.ToString() == correctRadioButton.Tag.ToString())?.Text.Trim();

                if (string.IsNullOrWhiteSpace(correctAnswer))
                {
                    MessageBox.Show("Selected correct answer is invalid.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
            }

            // Save question to the database
            try
            {
                using (SQLiteConnection conn = new SQLiteConnection("Data Source=assessment.db;Version=3;"))
                {
                    conn.Open();

                    string query = "INSERT INTO Questions (AssessmentID, QuestionText, OptionA, OptionB, OptionC, OptionD, CorrectAnswer) " +
                                   "VALUES (@assessmentID, @question, @optionA, @optionB, @optionC, @optionD, @correctAnswer)";

                    using (SQLiteCommand cmd = new SQLiteCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@assessmentID", lastAssessmentID);
                        cmd.Parameters.AddWithValue("@question", questionBox.Text);
                        cmd.Parameters.AddWithValue("@optionA", optionA);
                        cmd.Parameters.AddWithValue("@optionB", optionB);
                        cmd.Parameters.AddWithValue("@optionC", optionC);
                        cmd.Parameters.AddWithValue("@optionD", optionD);
                        cmd.Parameters.AddWithValue("@correctAnswer", correctAnswer);

                        cmd.ExecuteNonQuery();
                    }
                }

                MessageBox.Show("Question added successfully!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error: " + ex.Message, "Database Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            panelQuestions.Controls.Clear();
            teacher.GenerateQuestionStructure(currentSection, panelQuestions);
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
                MessageBox.Show("All fields must be filled!", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            int timeLimit = (int)guna2NumericUpDown1.Value;
            int classLevel = (int)guna2NumericUpDown2.Value;

            List<Question> questionsList = GetQuestionsFromForm();

            using (SQLiteConnection conn = new SQLiteConnection("Data Source=assessment.db;Version=3;"))
            {
                conn.Open();
                using (SQLiteTransaction transaction = conn.BeginTransaction())
                {
                    try
                    {
                        // ✅ Insert Assessment and Get AssessmentID
                        string insertAssessmentQuery = "INSERT INTO Assessments (Title, TimeLimit, ClassLevel) VALUES (@Title, @TimeLimit, @ClassLevel)";
                        using (SQLiteCommand cmd = new SQLiteCommand(insertAssessmentQuery, conn))
                        {
                            cmd.Parameters.AddWithValue("@Title", txtAssessmentTitle.Text);
                            cmd.Parameters.AddWithValue("@TimeLimit", timeLimit);
                            cmd.Parameters.AddWithValue("@ClassLevel", classLevel);
                            cmd.ExecuteNonQuery();
                        }

                        // ✅ Get the last inserted AssessmentID
                        lastAssessmentID = (int)conn.LastInsertRowId;

                        transaction.Commit();
                        MessageBox.Show("Assessment saved successfully!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        MessageBox.Show("Error saving assessment: " + ex.Message, "Database Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }

        }
        private List<Question> GetQuestionsFromForm()
        {
            List<Question> questions = new List<Question>();

            MessageBox.Show($"Total Controls in Panel: {panelQuestions.Controls.Count}", "Debug", MessageBoxButtons.OK, MessageBoxIcon.Information);

            foreach (Control control in panelQuestions.Controls)
            {
                if (control is Panel questionPanel)
                {
                    TextBox questionBox = questionPanel.Controls
                        .OfType<TextBox>()
                        .FirstOrDefault(tb => tb.Tag?.ToString() == "Question");

                    NumericUpDown marksBox = questionPanel.Controls
                        .OfType<NumericUpDown>()
                        .FirstOrDefault();

                    if (questionBox == null || string.IsNullOrWhiteSpace(questionBox.Text))
                    {
                        MessageBox.Show("Skipping empty question!", "Debug", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        continue;
                    }

                    if (marksBox == null)
                    {
                        MessageBox.Show("Skipping question with no marks!", "Debug", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        continue;
                    }

                    Question q = new Question
                    {
                        QuestionText = questionBox.Text,
                        QuestionType = currentSection,
                        Marks = (int)marksBox.Value,
                        Options = new List<Option>()
                    };

                    questions.Add(q);
                }
            }

            MessageBox.Show($"Total Questions Collected: {questions.Count}", "Debug", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
    }
}

