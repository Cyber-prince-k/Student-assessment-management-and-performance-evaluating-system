using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Data.SQLite;
using System.IO;

namespace Assessment_management_and_performance_evaluation
{
    public partial class student_dashboard : Form
    {
        private int loggedInUserId;
        private int assessmentId;
        private List<Question> questionsList = new List<Question>();
        private int currentIndex = 0;
        private int currentAssessmentID = 0; // Store the assessment ID
        private int totalTimeInSeconds;
        private System.Windows.Forms.Timer countdownTimer;
        private List<RadioButton> currentRadioButtons = new List<RadioButton>();
        private TextBox currentStructuredAnswerBox = null;
        private RichTextBox currentEssayAnswerBox = null;
        public student_dashboard(int userId)
        {
            InitializeComponent();
            loggedInUserId = userId;

            // Initialize questionNO label
            questionNO = new Label
            {
                AutoSize = true,
                Location = new Point(20, 10),
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                ForeColor = Color.Black,
                Name = "questionNO"
            };
            panelQuestions.Controls.Add(questionNO);
        }

        private void LoadAssessment()
        {
            try
            {
                using (SQLiteConnection conn = new SQLiteConnection("Data Source=assessment.db;Version=3;"))
                {
                    conn.Open();

                    // 1. Get student's class level
                    int classLevel = 0;
                    string classQuery = "SELECT ClassLevel FROM Students WHERE UserID = @userId";

                    using (SQLiteCommand cmd = new SQLiteCommand(classQuery, conn))
                    {
                        cmd.Parameters.AddWithValue("@userId", loggedInUserId);
                        object result = cmd.ExecuteScalar();

                        if (result != null && result != DBNull.Value)
                        {
                            classLevel = Convert.ToInt32(result);
                            Console.WriteLine($"Student Class Level: {classLevel}");
                        }
                        else
                        {
                            Console.WriteLine("No class level found for student!");
                            MessageBox.Show("Student class level not found!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            return;
                        }
                    }

                    // 2. Get assessment and its type
                    string assessmentQuery = @"
                        SELECT a.AssessmentID, a.Title, a.TimeLimit, q.questiontype as AssessmentType 
                        FROM Assessments a
                        LEFT JOIN Questions q ON q.AssessmentID = a.AssessmentID
                        WHERE a.AssessmentID = @assessmentId
                        LIMIT 1";

                    using (SQLiteCommand cmd = new SQLiteCommand(assessmentQuery, conn))
                    {
                        cmd.Parameters.AddWithValue("@assessmentId", currentAssessmentID);

                        using (SQLiteDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                assessmentId = reader.GetInt32(reader.GetOrdinal("AssessmentID"));
                                string title = reader.GetString(reader.GetOrdinal("Title"));
                                int timeLimit = reader.GetInt32(reader.GetOrdinal("TimeLimit"));
                                currentAssessmentType = reader.GetString(reader.GetOrdinal("AssessmentType"));

                                label1.Text = "Assessment: " + title;
                                label2.Text = "Class Level: " + classLevel.ToString();
                                totalTimeInSeconds = timeLimit * 60 * 60;

                                Console.WriteLine($"Loaded Assessment: ID={assessmentId}, Title='{title}', TimeLimit={timeLimit} hours, Type={currentAssessmentType}");
                            }
                            else
                            {
                                Console.WriteLine($"No assessment found with ID {currentAssessmentID}");
                                MessageBox.Show("Assessment not found.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                                return;
                            }
                        }
                    }

                    // 3. Load questions
                    questionsList = Question.LoadQuestions(assessmentId);
                    if (questionsList.Count == 0)
                    {
                        MessageBox.Show("No questions found in the assessment.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return;
                    }

                    StartCountdown();
                    DisplayCurrentQuestion();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error loading assessment: " + ex.Message);
            }
        }
        private void StartCountdown()
        {
            countdownTimer = new System.Windows.Forms.Timer();
            countdownTimer.Interval = 1000;
            countdownTimer.Tick += CountdownTimer_Tick;
            countdownTimer.Start();
        }

        private void CountdownTimer_Tick(object sender, EventArgs e)
        {
            if (totalTimeInSeconds <= 0)
            {
                countdownTimer.Stop();
                MessageBox.Show("Time's up!", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            totalTimeInSeconds--;
            TimeSpan timeLeft = TimeSpan.FromSeconds(totalTimeInSeconds);
            label3.Text = "Time Left: " + timeLeft.ToString(@"hh\:mm\:ss");
        }

        private void DisplayCurrentQuestion()
        {
            if (currentIndex >= 0 && currentIndex < questionsList.Count)
            {
                Question currentQuestion = questionsList[currentIndex];

                // Clear previous options and references
                panelQuestions.Controls.Clear();
                currentRadioButtons.Clear();
                currentStructuredAnswerBox = null;
                currentEssayAnswerBox = null;

                // Add and update question number and marks
                questionNO.Location = new Point(20, 10);
                questionNO.Text = $"Question {currentIndex + 1} of {questionsList.Count} [{currentQuestion.Marks} marks]";
                panelQuestions.Controls.Add(questionNO);

                // Add question text below the question number
                Label questionLabel = new Label
                {
                    Text = currentQuestion.QuestionText,
                    Location = new Point(20, 40),
                    AutoSize = true,
                    MaximumSize = new Size(panelQuestions.Width - 40, 0),
                    Font = new Font("Segoe UI", 11)
                };
                panelQuestions.Controls.Add(questionLabel);

                int yPos = questionLabel.Bottom + 20;

                if (currentQuestion.QuestionType?.ToLower() == "multiple choice")
                {
                    LoadOptionsFromDatabase(currentQuestion.QuestionID);
                    DisplayMultipleChoiceOptions(currentQuestion, yPos);
                }
                else if (currentQuestion.QuestionType?.ToLower() == "structured" || currentQuestion.QuestionType?.ToLower() == "essay")
                {
                    DisplayTextAnswer(yPos);
                }
            }
        }

        private void LoadOptionsFromDatabase(int questionId)
        {
            try
            {
                using (SQLiteConnection conn = new SQLiteConnection("Data Source=assessment.db;Version=3;"))
                {
                    conn.Open();
                    string query = @"
                        SELECT QuestionID, QuestionText, OptionA, OptionB, OptionC, OptionD, 
                               CorrectAnswer, qmarks 
                        FROM Questions 
                        WHERE QuestionID = @questionId";

                    using (SQLiteCommand cmd = new SQLiteCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@questionId", questionId);

                        using (SQLiteDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                Question current = questionsList[currentIndex];
                                current.Options = new List<Option>();

                                // Load marks
                                int qmarksOrdinal = reader.GetOrdinal("qmarks");
                                if (!reader.IsDBNull(qmarksOrdinal))
                                {
                                    current.Marks = reader.GetInt32(qmarksOrdinal);
                                    Console.WriteLine($"Loaded marks for question {questionId}: {current.Marks}");
                                }
                                else
                                {
                                    Console.WriteLine($"No marks found for question {questionId}");
                                    current.Marks = 0;
                                }

                                string correctAnswer = reader.GetString(reader.GetOrdinal("CorrectAnswer"));

                                // Add each non-empty option
                                if (!reader.IsDBNull(reader.GetOrdinal("OptionA")))
                                    current.Options.Add(new Option
                                    {
                                        Text = reader["OptionA"].ToString(),
                                        IsCorrect = reader["OptionA"].ToString() == correctAnswer
                                    });

                                if (!reader.IsDBNull(reader.GetOrdinal("OptionB")))
                                    current.Options.Add(new Option
                                    {
                                        Text = reader["OptionB"].ToString(),
                                        IsCorrect = reader["OptionB"].ToString() == correctAnswer
                                    });

                                if (!reader.IsDBNull(reader.GetOrdinal("OptionC")))
                                    current.Options.Add(new Option
                                    {
                                        Text = reader["OptionC"].ToString(),
                                        IsCorrect = reader["OptionC"].ToString() == correctAnswer
                                    });

                                if (!reader.IsDBNull(reader.GetOrdinal("OptionD")))
                                    current.Options.Add(new Option
                                    {
                                        Text = reader["OptionD"].ToString(),
                                        IsCorrect = reader["OptionD"].ToString() == correctAnswer
                                    });
                            }
                            else
                            {
                                Console.WriteLine($"No question found with ID {questionId}");
                            }
                        }
                    }

                    // Debug: Show all columns in Questions table
                    string debugQuery = "PRAGMA table_info(Questions)";
                    using (SQLiteCommand cmd = new SQLiteCommand(debugQuery, conn))
                    {
                        using (SQLiteDataReader reader = cmd.ExecuteReader())
                        {
                            Console.WriteLine("Questions table columns:");
                            while (reader.Read())
                            {
                                string columnName = reader["name"].ToString();
                                string columnType = reader["type"].ToString();
                                Console.WriteLine($"Column: {columnName}, Type: {columnType}");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading options: {ex.Message}\nStack trace: {ex.StackTrace}");
            }
        }

        private void DisplayMultipleChoiceOptions(Question question, int startY)
        {
            if (question.Options == null || question.Options.Count == 0)
                return;

            int yPos = startY;
            foreach (var option in question.Options)
            {
                if (!string.IsNullOrEmpty(option.Text))
                {
                    RadioButton rb = new RadioButton
                    {
                        Text = option.Text,
                        Location = new Point(40, yPos),
                        AutoSize = true,
                        Font = new Font("Segoe UI", 10),
                        Tag = option.IsCorrect
                    };
                    panelQuestions.Controls.Add(rb);
                    currentRadioButtons.Add(rb);
                    yPos += rb.Height + 10;
                }
            }
        }

        private void DisplayTextAnswer(int yPos)
        {
            Question currentQuestion = questionsList[currentIndex];
            string questionType = currentQuestion.QuestionType?.ToLower();

            if (questionType == "structured")
            {
                // For structured questions, use a regular TextBox
                currentStructuredAnswerBox = new TextBox
                {
                    Width = panelQuestions.Width - 40,
                    Height = 100,
                    Multiline = true,
                    Top = yPos,
                    Left = 20,
                    Tag = "Answer",
                    Font = new Font("Segoe UI", 10),
                    ScrollBars = ScrollBars.Vertical,
                    BorderStyle = BorderStyle.FixedSingle
                };
                panelQuestions.Controls.Add(currentStructuredAnswerBox);
            }
            else // Default to essay type for any non-multiple choice question
            {
                // For essay questions, use a RichTextBox
                currentEssayAnswerBox = new RichTextBox
                {
                    Width = panelQuestions.Width - 40,
                    Height = 200, // Larger height for essays
                    Top = yPos,
                    Left = 20,
                    Tag = "Answer",
                    Font = new Font("Segoe UI", 10),
                    BackColor = Color.White,
                    BorderStyle = BorderStyle.FixedSingle,
                    AcceptsTab = true,
                    EnableAutoDragDrop = true
                };
                panelQuestions.Controls.Add(currentEssayAnswerBox);
            }
        }

        private void SaveAnswer(string answerText, bool? isCorrect = null)
        {
            try
            {
                using (SQLiteConnection conn = new SQLiteConnection("Data Source=assessment.db;Version=3;"))
                {
                    conn.Open();

                    // For multiple choice, verify against correct answer in Questions table
                    if (currentAssessmentType?.ToLower() == "multiple choice")
                    {
                        string verifyQuery = @"
                            SELECT CorrectAnswer, qmarks 
                            FROM Questions 
                            WHERE QuestionID = @questionId";

                        using (SQLiteCommand cmd = new SQLiteCommand(verifyQuery, conn))
                        {
                            cmd.Parameters.AddWithValue("@questionId", questionsList[currentIndex].QuestionID);
                            using (SQLiteDataReader reader = cmd.ExecuteReader())
                            {
                                if (reader.Read())
                                {
                                    string correctAnswer = reader.GetString(reader.GetOrdinal("CorrectAnswer"));
                                    isCorrect = (answerText == correctAnswer);
                                }
                            }
                        }
                    }

                    // Save the answer with verified correctness
                    string insertQuery = @"
                        INSERT OR REPLACE INTO Answers 
                        (StudentID, AssessmentID, QuestionID, AnswerText, IsCorrect) 
                        VALUES 
                        (@studentId, @assessmentId, @questionId, @answerText, @isCorrect)";

                    using (SQLiteCommand cmd = new SQLiteCommand(insertQuery, conn))
                    {
                        cmd.Parameters.AddWithValue("@studentId", loggedInUserId);
                        cmd.Parameters.AddWithValue("@assessmentId", assessmentId);
                        cmd.Parameters.AddWithValue("@questionId", questionsList[currentIndex].QuestionID);
                        cmd.Parameters.AddWithValue("@answerText", answerText);
                        cmd.Parameters.AddWithValue("@isCorrect", isCorrect);

                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error saving answer: " + ex.Message);
            }
        }

        private string GetAnswerFromPanel()
        {
            if (currentIndex >= 0 && currentIndex < questionsList.Count)
            {
                Question currentQuestion = questionsList[currentIndex];
                string questionType = currentQuestion.QuestionType?.ToLower();

                if (questionType == "multiple choice")
                {
                    var selectedButton = currentRadioButtons.FirstOrDefault(rb => rb.Checked);
                    return selectedButton?.Text ?? string.Empty;
                }
                else if (questionType == "structured")
                {
                    return currentStructuredAnswerBox?.Text ?? string.Empty;
                }
                else // essay or any other type
                {
                    return currentEssayAnswerBox?.Text ?? string.Empty;
                }
            }
            return string.Empty;
        }

        private void guna2Button3_Click(object sender, EventArgs e)
        {
            try
            {
                // 1. Validate we have questions loaded
                if (questionsList == null || questionsList.Count == 0)
                {
                    MessageBox.Show("No questions available!", "Error",
                                  MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                // 2. Check current index bounds
                if (currentIndex < 0 || currentIndex >= questionsList.Count)
                {
                    MessageBox.Show("Invalid question index!", "Error",
                                  MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                // 3. Get current question
                Question current = questionsList[currentIndex];
                Student student = new Student();

                // 4. Validate assessment type
                if (string.IsNullOrEmpty(currentAssessmentType))
                {
                    MessageBox.Show("Assessment type not set!", "Error",
                                  MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                // 5. Get and validate answer
                string answer = GetAnswerFromPanel();
                if (string.IsNullOrEmpty(answer))
                {
                    MessageBox.Show("Please provide an answer before proceeding!", "Warning",
                                  MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                // 6. Save answer
                SaveAnswer(answer);

                // 7. Move to next question or finish
                currentIndex++;
                if (currentIndex >= questionsList.Count)
                {
                    MessageBox.Show("Assessment completed successfully!", "Complete",
                                  MessageBoxButtons.OK, MessageBoxIcon.Information);
                    // Additional completion logic here
                }
                else
                {
                    DisplayCurrentQuestion();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error processing question: {ex.Message}", "Error",
                              MessageBoxButtons.OK, MessageBoxIcon.Error);

                // Debug output
                Console.WriteLine($"Error in guna2Button3_Click: {ex.ToString()}");
            }
        }

        private string currentAssessmentType;

        private void guna2Button1_Click(object sender, EventArgs e)
        {
            if (AssessmentTitles.SelectedItem == null)
            {
                MessageBox.Show("Please select an assessment first.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            KeyValuePair<int, string> selectedAssessment = (KeyValuePair<int, string>)AssessmentTitles.SelectedItem;
            currentAssessmentID = selectedAssessment.Key;

            // Load the assessment questions
            LoadAssessment();

            // Switch to tabPage2
            guna2TabControl1.SelectedIndex = 1; // This will select the second tab (index 1)
        }

        private void guna2Button1_Click_1(object sender, EventArgs e)
        {
            if (AssessmentTitles.SelectedItem == null)
            {
                MessageBox.Show("Please select an assessment first.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            KeyValuePair<int, string> selectedAssessment = (KeyValuePair<int, string>)AssessmentTitles.SelectedItem;
            currentAssessmentID = selectedAssessment.Key;

            // Load the assessment questions
            LoadAssessment();

            // Switch to tabPage2
            guna2TabControl1.SelectedIndex = 1; // This will select the second tab (index 1)
        }

        private void student_dashboard_Load(object sender, EventArgs e)
        {
            try
            {
                using (SQLiteConnection conn = new SQLiteConnection("Data Source=assessment.db;Version=3;"))
                {
                    conn.Open();

                    // Load student information
                    string studentQuery = "SELECT FirstName, LastName, ClassLevel, StudentImage FROM Students WHERE UserID = @userId";
                    using (SQLiteCommand cmd = new SQLiteCommand(studentQuery, conn))
                    {
                        cmd.Parameters.AddWithValue("@userId", loggedInUserId);
                        using (SQLiteDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                Fname.Text = reader["FirstName"].ToString();
                                Sname.Text = reader["LastName"].ToString();
                                int classLevel = Convert.ToInt32(reader["ClassLevel"]);

                                // Load student image
                                if (!reader.IsDBNull(reader.GetOrdinal("StudentImage")))
                                {
                                    byte[] imageData = (byte[])reader["StudentImage"];
                                    using (MemoryStream ms = new MemoryStream(imageData))
                                    {
                                        guna2PictureBox1.Image = Image.FromStream(ms);
                                    }
                                }

                                // Load assessment titles for the student's class level
                                string assessmentQuery = "SELECT AssessmentID, Title FROM Assessments WHERE ClassLevel = @level";
                                using (SQLiteCommand assessCmd = new SQLiteCommand(assessmentQuery, conn))
                                {
                                    assessCmd.Parameters.AddWithValue("@level", classLevel);

                                    AssessmentTitles.Items.Clear();
                                    using (SQLiteDataReader assessReader = assessCmd.ExecuteReader())
                                    {
                                        while (assessReader.Read())
                                        {
                                            string title = assessReader["Title"].ToString();
                                            int id = Convert.ToInt32(assessReader["AssessmentID"]);
                                            AssessmentTitles.Items.Add(new KeyValuePair<int, string>(id, title));
                                        }
                                    }
                                    AssessmentTitles.DisplayMember = "Value";
                                    AssessmentTitles.ValueMember = "Key";
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error loading dashboard: " + ex.Message);
            }
        }

        private void guna2TextBox3_TextChanged(object sender, EventArgs e)
        {

        }

        private void guna2TextBox1_TextChanged(object sender, EventArgs e)
        {

        }

        private void guna2TextBox2_TextChanged(object sender, EventArgs e)
        {

        }

        private void guna2TextBox8_TextChanged(object sender, EventArgs e)
        {

        }

        private void label6_Click(object sender, EventArgs e)
        {

        }

        private void guna2TextBox7_TextChanged(object sender, EventArgs e)
        {

        }

        private void guna2TextBox5_TextChanged(object sender, EventArgs e)
        {

        }

        private void tabPage1_Click(object sender, EventArgs e)
        {

        }

        private void questionNO_Click(object sender, EventArgs e)
        {

        }

        private void ClassLevel_Click(object sender, EventArgs e)
        {

        }

        private void guna2Button2_Click(object sender, EventArgs e)
        {
            if (!IsAnswerSelected())
            {
                MessageBox.Show("Please provide an answer before proceeding!", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Save the current answer
            if (currentAssessmentType?.ToLower() == "multiple choice")
            {
                var selectedButton = currentRadioButtons.FirstOrDefault(rb => rb.Checked);
                if (selectedButton != null)
                {
                    SaveAnswer(selectedButton.Text, (bool)selectedButton.Tag);
                }
            }
            else if (currentAssessmentType?.ToLower() == "structured" || currentAssessmentType?.ToLower() == "essay")
            {
                if (currentAssessmentType?.ToLower() == "structured")
                {
                    if (currentStructuredAnswerBox != null)
                    {
                        SaveAnswer(currentStructuredAnswerBox.Text);
                    }
                }
                else if (currentAssessmentType?.ToLower() == "essay")
                {
                    if (currentEssayAnswerBox != null)
                    {
                        SaveAnswer(currentEssayAnswerBox.Text);
                    }
                }
            }

            // Move to next question
            if (currentIndex < questionsList.Count - 1)
            {
                currentIndex++;
                DisplayCurrentQuestion();
            }
            else
            {
                MessageBox.Show("This is the last question.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void guna2Button1_Click_2(object sender, EventArgs e)
        {
            if (currentIndex > 0)
            {
                currentIndex--;
                DisplayCurrentQuestion();
            }
            else
            {
                MessageBox.Show("This is the first question.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private bool IsAnswerSelected()
        {
            if (currentIndex >= 0 && currentIndex < questionsList.Count)
            {
                Question currentQuestion = questionsList[currentIndex];
                string questionType = currentQuestion.QuestionType?.ToLower();

                if (questionType == "multiple choice")
                {
                    return currentRadioButtons.Any(rb => rb.Checked);
                }
                else if (questionType == "structured")
                {
                    return currentStructuredAnswerBox != null && !string.IsNullOrWhiteSpace(currentStructuredAnswerBox.Text);
                }
                else // essay or any other type
                {
                    return currentEssayAnswerBox != null && !string.IsNullOrWhiteSpace(currentEssayAnswerBox.Text);
                }
            }
            return false;
        }

        private void guna2Button3_Click_1(object sender, EventArgs e)
        {
            if (currentIndex < questionsList.Count)
            {
                Question current = questionsList[currentIndex];
                Student student = new Student();
                string answer = GetAnswerFromPanel();
                student.SaveAnswer(loggedInUserId, current.QuestionID, answer);

                currentIndex++;
                if (currentIndex >= questionsList.Count)
                {
                    MessageBox.Show("End of assessment!", "Done", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    currentIndex = questionsList.Count - 1;
                }
                else
                {
                    DisplayCurrentQuestion();
                }
            }
        }

        private Label questionNO;  // Field declaration
    }
}
