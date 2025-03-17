using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Assessment_management_and_performance_evaluation
{
    public partial class Admin_dashboard : Form
    {
        private int loggedInUserId;
        private Administrator admin = new Administrator();
        public Admin_dashboard(int userId)
        {
            InitializeComponent();
            loggedInUserId = userId;
        }

        private void create_account_Load(object sender, EventArgs e)
        {

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

        private void guna2TextBox7_TextChanged(object sender, EventArgs e)
        {

        }

        private void label6_Click(object sender, EventArgs e)
        {

        }

        private void guna2TextBox8_TextChanged(object sender, EventArgs e)
        {

        }

        private void guna2TextBox5_TextChanged(object sender, EventArgs e)
        {

        }

        private void label2_Click(object sender, EventArgs e)
        {

        }

        private void label3_Click(object sender, EventArgs e)
        {

        }

        private void label4_Click(object sender, EventArgs e)
        {

        }

        private void profile_Click(object sender, EventArgs e)
        {

        }

        private void guna2Button1_Click(object sender, EventArgs e)
        {

        }

        private void guna2Button2_Click(object sender, EventArgs e)
        {
            try
            {
                // Get user input from textboxes and combo box
                string firstName = txtFirstName.Text.Trim();
                string lastName = txtLastName.Text.Trim();
                string email = txtEmail.Text.Trim();
                string subjectsExperienced = txtSubjects.Text.Trim();
                string classesToTeach = txtClasses.Text.Trim();
                string subjectRoot = cmbSubjectRoot.SelectedItem?.ToString();

                // Call the RegisterTeacher method from Administrator class
                bool success = admin.RegisterTeacher(firstName, lastName, email, subjectsExperienced, classesToTeach, subjectRoot);

                if (success)
                {
                    // Clear input fields after successful registration
                    txtFirstName.Clear();
                    txtLastName.Clear();
                    txtEmail.Clear();
                    txtSubjects.Clear();
                    txtClasses.Clear();
                    cmbSubjectRoot.SelectedIndex = -1;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Unexpected error: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
