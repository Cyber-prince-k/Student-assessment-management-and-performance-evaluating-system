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

                            // Send email using provided email
                            SendEmail(email, tempPassword);

                            MessageBox.Show("Teacher registered successfully! Temporary password sent via email.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
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

        /*  private void SendEmail(string recipientEmail, string tempPassword)
          {
              try
              {
                  MailMessage mail = new MailMessage();
                  SmtpClient smtp = new SmtpClient("smtp.gmail.com"); // Change to your email provider

                  mail.From = new MailAddress("your-email@gmail.com");
                  mail.To.Add(recipientEmail);
                  mail.Subject = "Temporary Password for Your Account";
                  mail.Body = $"Hello,\n\nYour account has been created. Your temporary password is: {tempPassword}\n\nPlease change it upon first login.\n\nBest regards,\nAdmin";

                  smtp.Port = 570; // Use correct port (587 for TLS, 465 for SSL)
                  smtp.Credentials = new NetworkCredential("your-email@gmail.com", "hvijsqefkxssebki");
                  smtp.EnableSsl = true; // Must be true for encryption

                  smtp.Send(mail);
                  MessageBox.Show("Email sent successfully!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
              }
              catch (Exception ex)
              {
                  MessageBox.Show("Failed to send email: " + ex.Message, "Email Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
              }
          }
        */
        private void SendEmail(string recipientEmail, string tempPassword)
        {
            try
            {
                MailMessage mail = new MailMessage();
                SmtpClient smtp = new SmtpClient("smtp.gmail.com");

                mail.From = new MailAddress("princekamnga1@gmail.com");
                mail.To.Add(recipientEmail);
                mail.Subject = "Temporary Password for Your Account";
                mail.Body = $"Hello,\n\nYour account has been created. Your temporary password is: {tempPassword}\n\nPlease change it upon first login.\n\nBest regards,\nAdmin";

                smtp.Port = 587; // Use port 587 for TLS
                smtp.Credentials = new NetworkCredential("princekamnga1@gmail.com", "jjjboroxxgiiadns"); // Use App Password here
                smtp.EnableSsl = true; // Enable SSL for encryption

                smtp.Send(mail);
                MessageBox.Show("Email sent successfully!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to send email: " + ex.ToString(), "Email Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        public void DeleteAccount(int userID) { /* Logic */ }
        }

    }

