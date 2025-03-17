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
using System.Text.RegularExpressions;


namespace Assessment_management_and_performance_evaluation
{
    public partial class createAccount : Form
    {
        private string connectionString = "Data Source=assessment.db;Version=3;";
        private Account account = new Account();
        public createAccount()
        {
            InitializeComponent();
        }

        private void label1_Click(object sender, EventArgs e)
        {

        }

        private void linkLabel1_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            // Open the CreateAccount form
            Form1 login = new Form1();
            login.Show();

            // Optional: Hide Form1 (login form)
            this.Hide();
        }

        private void guna2Button1_Click(object sender, EventArgs e)
        {
            try
            {
                string username = txtUsername.Text.Trim();
                string password = txtPassword.Text;
                string confirmPassword = txtConfirmPassword.Text;

                // Validate that all fields are filled
                if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(confirmPassword))
                {
                    MessageBox.Show("All fields must be filled!", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                // Check if user already exists
                if (account.UserExists(username))
                {
                    MessageBox.Show("Username already exists! Please choose a different username.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                // Validate password security
                if (!IsValidPassword(password))
                {
                    MessageBox.Show("Password must be at least 12 characters long and include uppercase letters, lowercase letters, numbers, and symbols.",
                                    "Weak Password", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                // Validate that passwords match
                if (password != confirmPassword)
                {
                    MessageBox.Show("Passwords do not match!", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                // Create account if all validations pass
                bool success = account.CreateAccount(username, password);
                if (success)
                {
                    MessageBox.Show("Account created successfully! Redirecting to login...", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);

                    // Open login form
                    Form1 loginForm = new Form1();
                    loginForm.Show();

                    // Close this form
                    this.Close();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("An unexpected error occurred: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        // Method to check if the password meets security requirements
        private bool IsValidPassword(string password)
        {
            try
            {
                if (password.Length < 12) return false;
                if (!Regex.IsMatch(password, "[A-Z]")) return false; // At least one uppercase letter
                if (!Regex.IsMatch(password, "[a-z]")) return false; // At least one lowercase letter
                if (!Regex.IsMatch(password, "[0-9]")) return false; // At least one number
                if (!Regex.IsMatch(password, "[^a-zA-Z0-9]")) return false; // At least one special character
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Password validation error: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }
    }
}
