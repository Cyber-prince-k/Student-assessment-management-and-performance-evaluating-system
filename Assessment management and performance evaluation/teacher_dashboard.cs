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

namespace Assessment_management_and_performance_evaluation
{
    public partial class teacher_dashboard : Form
    {
        private Teacher teacher;
        private int loggedInUserId;
        private string currentSection = "";
        private bool sectionActive = false;
        private List<Question> questionsList = new List<Question>(); // Store questions
        


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

        private void btnSaveAssessment_Click(object sender, EventArgs e)
        {
            if (questionsList == null || questionsList.Count == 0)
            {
                MessageBox.Show("No questions added! Please add at least one question.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(txtAssessmentTitle.Text))
            {
                MessageBox.Show("Please enter a title for the assessment.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // ✅ Get values from NumericUpDown
            int timeLimit = (int)guna2NumericUpDown1.Value;  // Time in hours
            int classLevel = (int)guna2NumericUpDown2.Value; // Class Level

            // ✅ Calculate total marks for the assessment
            int totalMarks = questionsList.Sum(q => q.Marks);

            if (totalMarks == 0)
            {
                MessageBox.Show("Total marks cannot be zero! Please assign marks to questions.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // ✅ Call the Teacher class method to save assessment
            bool success = teacher.CreateAssessment(txtAssessmentTitle.Text, timeLimit, classLevel, totalMarks, questionsList);

            if (success)
            {
                // ✅ Clear Fields After Saving
                txtAssessmentTitle.Clear();
                guna2NumericUpDown1.Value = 0;  // Reset time limit
                guna2NumericUpDown2.Value = 0;  // Reset class level
                panelQuestions.Controls.Clear();
                questionsList.Clear();

                MessageBox.Show("Assessment saved successfully!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void guna2Button3_Click(object sender, EventArgs e)
        {
            if (!sectionActive)
            {
                MessageBox.Show("Please select a section before adding questions.");
                return;
            }

            // ✅ Find the question TextBox by tag
            TextBox questionBox = panelQuestions.Controls
                .OfType<TextBox>()
                .FirstOrDefault(tb => tb.Tag != null && tb.Tag.ToString() == "Question");

            NumericUpDown marksBox = panelQuestions.Controls
                .OfType<NumericUpDown>()
                .FirstOrDefault();

            // ✅ Ensure a question is entered
            if (questionBox == null || string.IsNullOrWhiteSpace(questionBox.Text))
            {
                MessageBox.Show("Please enter a question before moving to the next one.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // ✅ Ensure marks are assigned
            if (marksBox == null || marksBox.Value == 0)
            {
                MessageBox.Show("Please assign marks before moving to the next question.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // ✅ Ensure multiple-choice options are filled
            if (currentSection == "Multiple Choice")
            {
                var optionBoxes = panelQuestions.Controls.OfType<TextBox>()
                    .Where(tb => tb.Tag != null && tb.Tag.ToString().StartsWith("Option"))
                    .ToList();

                foreach (var optionBox in optionBoxes)
                {
                    if (string.IsNullOrWhiteSpace(optionBox.Text))
                    {
                        MessageBox.Show("Please enter all multiple-choice options before proceeding.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                }
            }

            // ✅ Save question
            Question newQuestion = new Question
            {
                QuestionText = questionBox.Text,
                QuestionType = currentSection,
                Marks = (int)marksBox.Value
            };
            questionsList.Add(newQuestion);

            // ✅ Clear panel and prepare for next question
            panelQuestions.Controls.Clear();
            teacher.GenerateQuestionStructure(currentSection, panelQuestions);

        }

        private void guna2Button1_Click(object sender, EventArgs e)
        {

        }
    }
    }

