using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Assessment_management_and_performance_evaluation
{
    internal class User
    {
       
            private int userID;
            private string username;
            private string email;
            private string password;
            private string userType;

            public int UserID { get => userID; set => userID = value; }
            public string Username { get => username; set => username = value; }
            public string Email { get => email; set => email = value; }
            public string Password { get => password; set => password = value; }
            public string UserType { get => userType; set => userType = value; }
        }

    }
