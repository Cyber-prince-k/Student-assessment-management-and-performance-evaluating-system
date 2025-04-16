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

        private void LoadAssessment()
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
                    }

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
                // currentQuestion.DisplayCurrentQuestion(panelQuestions, label4, currentIndex, questionsList.Count);
               Question.DisplayCurrentQuestion(currentQuestion, panelQuestions, label4, currentIndex, questionsList.Count);

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

        }

        private void guna2Button3_Click(object sender, EventArgs e)
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
