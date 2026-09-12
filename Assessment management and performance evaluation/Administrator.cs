using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data.SQLite;
using System.Net;
using System.Net.Mail;
using System.Windows.Forms;
using System.Security.Cryptography;

namespace Assessment_management_and_performance_evaluation
{
    internal class Administrator
    {
        private string connectionString = "Data Source=assessment.db;Version=3;";

        private string username;
            private string userRole;

            public string Username { get => username; set => username = value; }
            public string UserRole { get => userRole; set => userRole = value; }

        public bool RegisterTeacher(string firstName, string lastName, string email, string subjectsExperienced, string classesToTeach, string subjectRoot)
        {
            try
            {
                // Validate input fields
                if (string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(lastName) ||
                    string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(subjectsExperienced) ||
                    string.IsNullOrWhiteSpace(classesToTeach) || string.IsNullOrWhiteSpace(subjectRoot))
                {
                    MessageBox.Show("All fields must be filled!", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return false;
                }

                // Generate temporary password
                string tempPassword = GenerateTemporaryPassword();
                string hashedPassword = HashPassword(tempPassword);

                // Generate a username based on the teacher's first name (ensure uniqueness if needed)
                string username = firstName.ToLower() + "." + lastName.ToLower();

                using (SQLiteConnection conn = new SQLiteConnection(connectionString))
                {
                    conn.Open();
                    using (SQLiteTransaction transaction = conn.BeginTransaction())
                    {
                        try
                        {
                            // Check if username already exists in Users to avoid UNIQUE constraint failure
                            string checkUserQuery = "SELECT COUNT(*) FROM Users WHERE Username = @username;";
                            using (SQLiteCommand cmdCheck = new SQLiteCommand(checkUserQuery, conn))
                            {
                                cmdCheck.Parameters.AddWithValue("@username", username);
                                long count = Convert.ToInt64(cmdCheck.ExecuteScalar());
                                if (count > 0)
                                {
                                    username = $"{username}{new Random().Next(10, 99)}";
                                }
                            }

                            // 1️⃣ Insert into Users table to generate UserID first
                            string insertUserQuery = "INSERT INTO Users (Username, Password, UserType) VALUES (@username, @password, 'Teacher');";
                            using (SQLiteCommand cmdUser = new SQLiteCommand(insertUserQuery, conn))
                            {
                                cmdUser.Parameters.AddWithValue("@username", username);
                                cmdUser.Parameters.AddWithValue("@password", hashedPassword);
                                cmdUser.ExecuteNonQuery();
                            }

                            // 2️⃣ Get the generated UserID
                            long userID = conn.LastInsertRowId;

                            // 3️⃣ Insert into Teachers table with the generated UserID
                            string insertTeacherQuery = "INSERT INTO Teachers (UserID, FirstName, LastName, Email, SubjectsExperienced, ClassesToTeach, SubjectRoot) " +
                                                        "VALUES (@userID, @firstName, @lastName, @email, @subjectsExperienced, @classesToTeach, @subjectRoot);";
                            using (SQLiteCommand cmdTeacher = new SQLiteCommand(insertTeacherQuery, conn))
                            {
                                cmdTeacher.Parameters.AddWithValue("@userID", userID);
                                cmdTeacher.Parameters.AddWithValue("@firstName", firstName);
                                cmdTeacher.Parameters.AddWithValue("@lastName", lastName);
                                cmdTeacher.Parameters.AddWithValue("@email", email);
                                cmdTeacher.Parameters.AddWithValue("@subjectsExperienced", subjectsExperienced);
                                cmdTeacher.Parameters.AddWithValue("@classesToTeach", classesToTeach);
                                cmdTeacher.Parameters.AddWithValue("@subjectRoot", subjectRoot);
                                cmdTeacher.ExecuteNonQuery();
                            }

                            // Commit transaction
                            transaction.Commit();

                            // Send email with credentials (including username!)
                            string emailSubject = "Your Teacher Account Credentials";
                            string emailBody = $"Hello {firstName},\n\n" +
                                               $"Your teacher account has been successfully created.\n\n" +
                                               $"Username: {username}\n" +
                                               $"Temporary Password: {tempPassword}\n\n" +
                                               $"Please log in and change your password upon first login.\n\n" +
                                               $"Best regards,\nSchool Administration";

                            bool emailSent = EmailService.SendEmail(email, emailSubject, emailBody, out string emailError);

                            if (emailSent)
                            {
                                MessageBox.Show($"Teacher registered successfully!\nLogin credentials sent to {email}.\n\nUsername: {username}",
                                    "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            }
                            else
                            {
                                string message = $"Teacher registered successfully in the database.\n\n" +
                                                 $"Notice: The credentials email could not be delivered.\n" +
                                                 $"Error: {emailError}\n\n" +
                                                 $"Please provide the credentials directly to the teacher:\n" +
                                                 $"----------------------------------------\n" +
                                                 $"Username: {username}\n" +
                                                 $"Temporary Password: {tempPassword}\n" +
                                                 $"----------------------------------------\n" +
                                                 $"(The credentials have been copied to your clipboard)";

                                try
                                {
                                    Clipboard.SetText($"Username: {username}\r\nPassword: {tempPassword}");
                                }
                                catch { }

                                MessageBox.Show(message, "Email Delivery Notice", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            }

                            return true;
                        }
                        catch (Exception ex)
                        {
                            transaction.Rollback();
                            MessageBox.Show("Error registering teacher: " + ex.Message, "Database Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Unexpected error: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            return false;
        }


        private string GenerateTemporaryPassword()
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789@#$%!";
            StringBuilder result = new StringBuilder();
            Random random = new Random();

            for (int i = 0; i < 12; i++)
            {
                result.Append(chars[random.Next(chars.Length)]);
            }

            return result.ToString();
        }

        private string HashPassword(string password)
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

        public bool SendEmail(string recipientEmail, string tempPassword)
        {
            string subject = "Temporary Password for Your Account";
            string body = $"Hello,\n\nYour account has been created. Your temporary password is: {tempPassword}\n\nPlease change it upon first login.\n\nBest regards,\nSchool Administration";
            bool sent = EmailService.SendEmail(recipientEmail, subject, body, out string error);
            if (!sent)
            {
                MessageBox.Show($"Failed to send email: {error}", "Email Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            return sent;
        }
        public void DeleteAccount(int userID) { /* Logic */ }
        }

    }

