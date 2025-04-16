using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Data.SQLite;
using System.Net.Mail;
using System.Net;
namespace Assessment_management_and_performance_evaluation
{
    internal class Account
    {
        private string connectionString = "Data Source=assessment.db;Version=3;";

        private int accountID;
        private string userType;
        private int userID;

        public int AccountID { get => accountID; set => accountID = value; }
        public string UserType { get => userType; set => userType = value; }
        public int UserID { get => userID; set => userID = value; }

        // Method to create an account
        public bool CreateAccount(string username, string password)
        {
            try
            {
                string hashedPassword = HashPassword(password);
                string userType = "Administrator"; // Default user type

                using (SQLiteConnection conn = new SQLiteConnection(connectionString))
                {
                    conn.Open();
                    string query = "INSERT INTO Users (Username, Password, UserType) VALUES (@username, @password, @userType)";

                    using (SQLiteCommand cmd = new SQLiteCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@username", username);
                        cmd.Parameters.AddWithValue("@password", hashedPassword);
                        cmd.Parameters.AddWithValue("@userType", userType);

                        int result = cmd.ExecuteNonQuery();
                        if (result > 0)
                        {
                            MessageBox.Show("Account created successfully!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            return true;
                        }
                        else
                        {
                            MessageBox.Show("Failed to create account!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                    }
                }
            }
            catch (SQLiteException ex)
            {
                MessageBox.Show("Database error: " + ex.Message, "Database Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            return false;
        }

        // Method to hash passwords securely
        private string HashPassword(string password)
        {
            try
            {
                using (SHA256 sha256 = SHA256.Create())
                {
                    byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
                    StringBuilder builder = new StringBuilder();
                    foreach (byte b in bytes)
                    {
                        builder.Append(b.ToString("x2"));
                    }
                    return builder.ToString();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error hashing password: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return string.Empty;
            }
        }

        // Method to check if the username already exists
        public bool UserExists(string username)
        {
            try
            {
                using (SQLiteConnection conn = new SQLiteConnection(connectionString))
                {
                    conn.Open();
                    string query = "SELECT COUNT(*) FROM Users WHERE Username = @username";

                    using (SQLiteCommand cmd = new SQLiteCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@username", username);
                        int count = Convert.ToInt32(cmd.ExecuteScalar());

                        return count > 0; // Returns true if user exists
                    }
                }
            }
            catch (SQLiteException ex)
            {
                MessageBox.Show("Database error: " + ex.Message, "Database Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }

        public void UpdateAccount(User user) { /* Logic */ }

        // Method to handle user login
        public bool Login(string username, string password, out string userType, out int userId)
        {
            userType = null;
            userId = -1; // Default value for invalid login

            try
            {
                using (SQLiteConnection conn = new SQLiteConnection(connectionString))
                {
                    conn.Open();
                    string query = "SELECT UserID, UserType, Password FROM Users WHERE Username = @username";

                    using (SQLiteCommand cmd = new SQLiteCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@username", username);
                        using (SQLiteDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                string storedHashedPassword = reader["Password"].ToString();
                                userType = reader["UserType"].ToString();
                                userId = Convert.ToInt32(reader["UserID"]);

                                if (VerifyPassword(password, storedHashedPassword))
                                {
                                    return true; // Login successful
                                }
                            }
                        }
                    }
                }
            }
            catch (SQLiteException ex)
            {
                MessageBox.Show("Database error: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            return false;
        }

        // Method to store the logged-in user
        private void StoreLoggedInUser(string username, string userType)
        {
            try
            {
                using (SQLiteConnection conn = new SQLiteConnection(connectionString))
                {
                    conn.Open();
                    string query = "INSERT INTO LoggedInUsers (Username, UserType) VALUES (@username, @userType)";

                    using (SQLiteCommand cmd = new SQLiteCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@username", username);
                        cmd.Parameters.AddWithValue("@userType", userType);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (SQLiteException ex)
            {
                MessageBox.Show("Error storing logged-in user: " + ex.Message, "Database Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // Method to verify password hash
        private bool VerifyPassword(string inputPassword, string storedHashedPassword)
        {
            string hashedInput = HashPassword(inputPassword);
            return hashedInput == storedHashedPassword;
        }

        public bool RegisterStudent(string firstName, string lastName, string parentEmail, string category, byte[] studentImage, string password, int classLevel)
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
                            string hashedPassword = HashPassword(password);

                            // Insert into Users
                            string insertUserQuery = "INSERT INTO Users (Username, Password, UserType) VALUES (@username, @password, 'Student')";
                            using (SQLiteCommand cmd = new SQLiteCommand(insertUserQuery, conn))
                            {
                                cmd.Parameters.AddWithValue("@username", firstName);
                                cmd.Parameters.AddWithValue("@password", hashedPassword);
                                cmd.ExecuteNonQuery();
                            }

                            // Get the newly inserted UserID
                            int userID;
                            using (SQLiteCommand cmd = new SQLiteCommand("SELECT last_insert_rowid()", conn))
                            {
                                userID = Convert.ToInt32(cmd.ExecuteScalar());
                            }

                            // Insert into Students including ClassLevel
                            string insertStudentQuery = "INSERT INTO Students (UserID, FirstName, LastName, ParentEmail, Category, StudentImage, ClassLevel) " +
                                                        "VALUES (@userID, @firstName, @lastName, @parentEmail, @category, @studentImage, @classLevel)";
                            using (SQLiteCommand cmd = new SQLiteCommand(insertStudentQuery, conn))
                            {
                                cmd.Parameters.AddWithValue("@userID", userID);
                                cmd.Parameters.AddWithValue("@firstName", firstName);
                                cmd.Parameters.AddWithValue("@lastName", lastName);
                                cmd.Parameters.AddWithValue("@parentEmail", parentEmail);
                                cmd.Parameters.AddWithValue("@category", category);
                                cmd.Parameters.AddWithValue("@studentImage", studentImage);
                                cmd.Parameters.AddWithValue("@classLevel", classLevel);
                                cmd.ExecuteNonQuery();
                            }

                            // Assign predefined subjects based on category
                            AssignPredefinedSubjects(conn, userID, category);

                            // Commit transaction
                            transaction.Commit();

                            // Send password email after successful registration
                            SendEmail(parentEmail, password);

                            return true;
                        }
                        catch (Exception ex)
                        {
                            transaction.Rollback();
                            MessageBox.Show("Error: " + ex.Message, "Database Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            return false;
                        }
                    }
                }
            }
            catch (SQLiteException ex)
            {
                MessageBox.Show("Database connection error: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }

        public void SendEmail(string recipientEmail, string tempPassword)
        {
            try
            {
                MailMessage mail = new MailMessage();
                SmtpClient smtp = new SmtpClient("smtp.gmail.com");

                mail.From = new MailAddress("princekamnga1@gmail.com");
                mail.To.Add(recipientEmail);
                mail.Subject = "Temporary Password for Your Child's Account";
                mail.Body = $"Dear Parent,\n\nYour child's account has been created successfully.\n\nTemporary Password: {tempPassword}\n\nPlease ensure your child changes this password upon first login.\n\nBest regards,\nSchool Administration";

                smtp.Port = 587;
                smtp.Credentials = new NetworkCredential("princekamnga1@gmail.com", "jjjboroxxgiiadns"); // Use App Password here

                smtp.EnableSsl = true;

                smtp.Send(mail);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to send email: " + ex.Message, "Email Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // Method to assign predefined subjects based on category

        private void AssignPredefinedSubjects(SQLiteConnection conn, int studentID, string category)
        {
            string[] scienceSubjects = { "Mathematics", "Agriculture", "English", "Chichewa", "Physics", "Chemistry" };
            string[] humanitiesSubjects = { "Mathematics", "Agriculture", "English", "Chichewa", "History", "Geography" };

            string[] selectedSubjects = category == "Science" ? scienceSubjects : humanitiesSubjects;

            foreach (string subject in selectedSubjects)
            {
                string query = "INSERT INTO StudentSubjects (StudentID, SubjectName) VALUES (@studentID, @subject)";
                using (SQLiteCommand cmd = new SQLiteCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@studentID", studentID);
                    cmd.Parameters.AddWithValue("@subject", subject);
                    cmd.ExecuteNonQuery();
                }
            }
        }

    }
}
