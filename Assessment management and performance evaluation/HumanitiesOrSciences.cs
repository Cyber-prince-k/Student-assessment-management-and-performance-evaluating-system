using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Assessment_management_and_performance_evaluation
{
    internal class HumanitiesOrSciences
    {
        
            private string streamName;
            private int studentID;
            private int teacherID;

            public string StreamName { get => streamName; set => streamName = value; }
            public int StudentID { get => studentID; set => studentID = value; }
            public int TeacherID { get => teacherID; set => teacherID = value; }

            public void AssignStream(int studentID, int teacherID, string streamName) { /* Logic */ }
            public void ChangeStream(int teacherID, string newStream) { /* Logic */ }
        }

    }
