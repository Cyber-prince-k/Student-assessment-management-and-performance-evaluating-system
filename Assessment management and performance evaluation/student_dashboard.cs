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
        public student_dashboard(int userId)
        {
            InitializeComponent();
            loggedInUserId = userId;
        }

        /*private void LoadAssessment()
         {
             try
             {
                 using (SQLiteConnection conn = new SQLiteConnection("Data Source=assessment.db;Version=3;"))
                 {
                     conn.Open();

                     string classQuery = "SELECT ClassLevel FROM Students WHERE UserID = @userId";
                     int classLevel;

                     using (SQLiteCommand cmd = new SQLiteCommand(classQuery, conn))
                     {
                         cmd.Parameters.AddWithValue("@userId", loggedInUserId);
                         classLevel = Convert.ToInt32(cmd.ExecuteScalar());
                     }

                     string assessmentQuery = "SELECT * FROM Assessments WHERE ClassLevel = @level LIMIT 1";
                     using (SQLiteCommand cmd = new SQLiteCommand(assessmentQuery, conn))
                     {
                         cmd.Parameters.AddWithValue("@level", classLevel);
                         using (SQLiteDataReader reader = cmd.ExecuteReader())
                         {
                             if (reader.Read())
                             {
                                 assessmentId = Convert.ToInt32(reader["AssessmentID"]);
                                 label1.Text = "Assessment: " + reader["Title"].ToString();
                                 label2.Text = "Class Level: " + reader["ClassLevel"].ToString();
                                 totalTimeInSeconds = Convert.ToInt32(reader["TimeLimit"]) * 60 * 60;
                             }
                             else
                             {
                                 MessageBox.Show("No assessment found for your class.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                                 return;
                             }
                         }
                     /

                     // ✅ Load questions
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
         }*/
        /*  private void LoadAssessment()
          {
              try
              {
                  using (SQLiteConnection conn = new SQLiteConnection("Data Source=assessment.db;Version=3;"))
                  {
                      conn.Open();

                      string classQuery = "SELECT ClassLevel FROM Students WHERE UserID = @userId";
                      int classLevel;

                      using (SQLiteCommand cmd = new SQLiteCommand(classQuery, conn))
                      {
                          cmd.Parameters.AddWithValue("@userId", loggedInUserId);
                          classLevel = Convert.ToInt32(cmd.ExecuteScalar());
                      }

                      string assessmentQuery = "SELECT * FROM Assessments WHERE ClassLevel = @level LIMIT 1";
                      using (SQLiteCommand cmd = new SQLiteCommand(assessmentQuery, conn))
                      {
                          cmd.Parameters.AddWithValue("@level", classLevel);
                          using (SQLiteDataReader reader = cmd.ExecuteReader())
                          {
                              if (reader.Read())
                              {
                                  assessmentId = Convert.ToInt32(reader["AssessmentID"]);
                                  currentAssessmentType = reader["AssessmentType"].ToString(); // Assuming you have this column
                                  label1.Text = "Assessment: " + reader["Title"].ToString();
                                  label2.Text = "Class Level: " + reader["ClassLevel"].ToString();
                                  totalTimeInSeconds = Convert.ToInt32(reader["TimeLimit"]) * 60 * 60;
                              }
                              else
                              {
                                  MessageBox.Show("No assessment found for your class.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                                  return;
                              }
                          }
                      }

                      // Load questions
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
        */
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

                    // 2. Get assessment
                    string assessmentQuery = "SELECT AssessmentID, Title, TimeLimit FROM Assessments WHERE ClassLevel = @level LIMIT 1";

                    using (SQLiteCommand cmd = new SQLiteCommand(assessmentQuery, conn))
                    {
                        cmd.Parameters.AddWithValue("@level", classLevel);

                        using (SQLiteDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                assessmentId = reader.GetInt32(reader.GetOrdinal("AssessmentID"));
                                string title = reader.GetString(reader.GetOrdinal("Title"));
                                int timeLimit = reader.GetInt32(reader.GetOrdinal("TimeLimit"));

                                label1.Text = "Assessment: " + title;
                                label2.Text = "Class Level: " + classLevel.ToString();
                                totalTimeInSeconds = timeLimit * 60 * 60;

                                Console.WriteLine($"Loaded Assessment: ID={assessmentId}, Title='{title}', TimeLimit={timeLimit} hours");
                            }
                            else
                            {
                                Console.WriteLine($"No assessment found for class level {classLevel}");
                                MessageBox.Show("No assessment found for your class level.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                                return;
                            }
                        }
                    }

                    // 3. Load questions
                    questionsList = Question.LoadQuestions(assessmentId);
                    Console.WriteLine($"\nQuestion Loading Results:");
                    Console.WriteLine($"Total questions loaded: {questionsList?.Count ?? 0}");

                    if (questionsList == null || questionsList.Count == 0)
                    {
                        Console.WriteLine("WARNING: No questions were loaded!");
                        MessageBox.Show("No questions found in the assessment.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return;
                    }
                    else
                    {
                        foreach (var question in questionsList)
                        {
                            Console.WriteLine($"\nQuestion ID: {question.QuestionID}");
                            Console.WriteLine($"Text: {question.QuestionText}");
                            Console.WriteLine($"Options: {question.Options?.Count ?? 0}");

                            if (question.Options != null)
                            {
                                foreach (var option in question.Options)
                                {
                                    Console.WriteLine($"- {option.Text} {(option.IsCorrect ? "(Correct)" : "")}");
                                }
                            }
                        }
                    }

                    // 4. Initialize and display first question
                    currentIndex = 0;
                    Console.WriteLine($"\nDisplaying question {currentIndex + 1} of {questionsList.Count}");
                    StartCountdown();
                    DisplayCurrentQuestion();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\nCRITICAL ERROR IN LoadAssessment():");
                Console.WriteLine($"Message: {ex.Message}");
                Console.WriteLine($"Stack Trace: {ex.StackTrace}");

                MessageBox.Show($"Error loading assessment: {ex.Message}\n\nCheck console for details.",
                              "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
        /*private void DisplayCurrentQuestion()
        {
            if (currentIndex >= 0 && currentIndex < questionsList.Count)
            {
                Question currentQuestion = questionsList[currentIndex];
                // currentQuestion.DisplayCurrentQuestion(panelQuestions, label4, currentIndex, questionsList.Count);
               Question.DisplayCurrentQuestion(currentQuestion, panelQuestions, label4, currentIndex, questionsList.Count);

            }
        }*/

        private void DisplayCurrentQuestion()
        {
            panelQuestions.SuspendLayout();
            panelQuestions.Controls.Clear();

            try
            {
                if (questionsList == null || currentIndex >= questionsList.Count)
                {
                    Label errorLabel = new Label
                    {
                        Text = "No questions available",
                        ForeColor = Color.Red,
                        Location = new Point(20, 20)
                    };
                    panelQuestions.Controls.Add(errorLabel);
                    return;
                }

                Question current = questionsList[currentIndex];
                int yPos = 20;

                // 1. Display Question Text
                Label questionLabel = new Label
                {
                    Text = current.QuestionText,
                    Font = new Font("Segoe UI", 12, FontStyle.Bold),
                    ForeColor = Color.Black,
                    Location = new Point(20, yPos),
                    MaximumSize = new Size(panelQuestions.Width - 40, 0),
                    AutoSize = true
                };
                panelQuestions.Controls.Add(questionLabel);
                yPos += questionLabel.Height + 20;

                // 2. Display based on question type
                switch (currentAssessmentType?.ToLower())
                {
                    case "multiple choice":
                        if (current.Options != null)
                        {
                            foreach (var option in current.Options)
                            {
                                RadioButton rb = new RadioButton
                                {
                                    Text = option.Text,
                                    Tag = option.IsCorrect,
                                    Location = new Point(30, yPos),
                                    AutoSize = true
                                };
                                panelQuestions.Controls.Add(rb);
                                yPos += rb.Height + 10;
                            }
                        }
                        break;

                    case "structured":
                        TextBox answerBox = new TextBox
                        {
                            Multiline = true,
                            Width = panelQuestions.Width - 60,
                            Height = 100,
                            Location = new Point(20, yPos)
                        };
                        panelQuestions.Controls.Add(answerBox);
                        yPos += answerBox.Height + 20;
                        break;

                    case "essay":
                        RichTextBox essayBox = new RichTextBox
                        {
                            Width = panelQuestions.Width - 60,
                            Height = 200,
                            Location = new Point(20, yPos)
                        };
                        panelQuestions.Controls.Add(essayBox);
                        break;

                    default:
                        Label typeLabel = new Label
                        {
                            Text = $"Unknown question type: {currentAssessmentType}",
                            ForeColor = Color.Red,
                            Location = new Point(20, yPos)
                        };
                        panelQuestions.Controls.Add(typeLabel);
                        break;
                }
            }
            catch (Exception ex)
            {
                Label errorLabel = new Label
                {
                    Text = $"Error: {ex.Message}",
                    ForeColor = Color.Red,
                    Location = new Point(20, 20)
                };
                panelQuestions.Controls.Add(errorLabel);
            }
            finally
            {
                panelQuestions.ResumeLayout(true);
                panelQuestions.Refresh();
            }
        }
        private static void LoadOptionsFromDatabase(Question question)
        {
            try
            {
                using (SQLiteConnection conn = new SQLiteConnection("Data Source=assessment.db;Version=3;"))
                {
                    conn.Open();
                    string query = "SELECT OptionA, OptionB, OptionC, OptionD, CorrectAnswer FROM Questions WHERE QuestionID = @questionId";

                    using (SQLiteCommand cmd = new SQLiteCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@questionId", question.QuestionID);

                        using (SQLiteDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                question.Options = new List<Option>();

                                // Add all options
                                question.Options.Add(new Option { Text = reader["OptionA"].ToString(), IsCorrect = reader["OptionA"].ToString() == reader["CorrectAnswer"].ToString() });
                                question.Options.Add(new Option { Text = reader["OptionB"].ToString(), IsCorrect = reader["OptionB"].ToString() == reader["CorrectAnswer"].ToString() });
                                question.Options.Add(new Option { Text = reader["OptionC"].ToString(), IsCorrect = reader["OptionC"].ToString() == reader["CorrectAnswer"].ToString() });
                                question.Options.Add(new Option { Text = reader["OptionD"].ToString(), IsCorrect = reader["OptionD"].ToString() == reader["CorrectAnswer"].ToString() });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error loading options: " + ex.Message, "Database Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public string GetAnswerFromPanel(Panel panel, string questionType)
        {
            try
            {
                switch (questionType.ToLower())
                {
                    case "multiple choice":
                        var selectedRadio = panel.Controls.OfType<RadioButton>().FirstOrDefault(r => r.Checked);
                        return selectedRadio?.Tag?.ToString() ?? string.Empty;

                    case "structured":
                        var textBox = panel.Controls.OfType<TextBox>().FirstOrDefault(tb => tb.Tag?.ToString() == "Answer");
                        return textBox?.Text.Trim() ?? string.Empty;

                    case "essay":
                        var richTextBox = panel.Controls.OfType<RichTextBox>().FirstOrDefault(rtb => rtb.Tag?.ToString() == "Answer");
                        return richTextBox?.Text.Trim() ?? string.Empty;

                    default:
                        return string.Empty;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error retrieving answer: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return string.Empty;
            }
        }

        // In student_dashboard.cs, modify the button click handler to pass question type
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
                string answer = student.GetAnswerFromPanel(panelQuestions, currentAssessmentType);
                if (string.IsNullOrEmpty(answer))
                {
                    MessageBox.Show("Please provide an answer before proceeding!", "Warning",
                                  MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                // 6. Save answer
                student.SaveAnswer(loggedInUserId, current.QuestionID, answer);

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
        // Add this to student_dashboard.cs to store the assessment type
        private string currentAssessmentType;

        // Modify LoadAssessment to get the assessment type
        
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

        }

       /* private void guna2Button3_Click(object sender, EventArgs e)
        {
            if (currentIndex < questionsList.Count)
            {
                Question current = questionsList[currentIndex];
                Student student = new Student();
                string answer = student.GetAnswerFromPanel(panelQuestions);
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
       */
        private void student_dashboard_Load(object sender, EventArgs e)
        {
            try
            {
                LoadAssessment();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error loading dashboard: " + ex.Message);
            }
        }
    }
}
