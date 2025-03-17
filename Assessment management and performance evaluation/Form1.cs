using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.UI.WebControls;
using System.Data.SQLite;
using System.Windows.Forms;
using System.Security.Principal;

namespace Assessment_management_and_performance_evaluation
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        private void guna2Button1_Click(object sender, EventArgs e)
        {
            try
            {
                string username = txtUsername.Text.Trim();
                string password = txtPassword.Text;

                if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
                {
                    MessageBox.Show("All fields must be filled!", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                // Create an instance of Account and validate login
                Account account = new Account();
                string userType;
                int userId;
                bool isLoggedIn = account.Login(username, password, out userType, out userId); // Pass 4 arguments

                if (isLoggedIn)
                {
                    MessageBox.Show("Login successful!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);

                    // Open the correct dashboard and pass the logged-in UserID
                    Form dashboard = null;
                    if (userType == "Administrator")
                        dashboard = new Admin_dashboard(userId);
                    else if (userType == "Teacher")
                        dashboard = new teacher_dashboard(userId);
                    else if (userType == "Student")
                        dashboard = new student_dashboard(userId);
                    else
                    {
                        MessageBox.Show("Unknown user type!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }

                    // Open the dashboard and hide the login form
                    dashboard.Show();
                    this.Hide();
                }
                else
                {
                    MessageBox.Show("Invalid username or password!", "Login Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("An unexpected error occurred: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }


        }

        private void linkLabel1_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            // Open the CreateAccount form
            createAccount createAccountForm = new createAccount();
            createAccountForm.Show();

            // Optional: Hide Form1 (login form)
            this.Hide();
        }
    }
}
