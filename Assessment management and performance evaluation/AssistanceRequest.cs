using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Assessment_management_and_performance_evaluation
{
    internal class AssistanceRequest
    {
        
            private int requestID;
            private int studentID;
            private string message;

            public int RequestID { get => requestID; set => requestID = value; }
            public int StudentID { get => studentID; set => studentID = value; }
            public string Message { get => message; set => message = value; }

            public void SubmitRequest(int studentID, int teacherID, string message) { /* Logic */ }
        }

    }

