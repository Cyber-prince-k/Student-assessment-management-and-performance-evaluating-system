using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Assessment_management_and_performance_evaluation
{
    internal class Assessment
    {
        
            private int assessmentID;
            private DateTime timeLimit;
            private List<string> questions;
            private int teacherID;
            private string subject;

            public int AssessmentID { get => assessmentID; set => assessmentID = value; }
            public DateTime TimeLimit { get => timeLimit; set => timeLimit = value; }
            public List<string> Questions { get => questions; set => questions = value; }
            public int TeacherID { get => teacherID; set => teacherID = value; }
            public string Subject { get => subject; set => subject = value; }

            public void AddQuestion(string question) { questions.Add(question); }
            public void GradeAssessment(int studentID, int assessmentID, string subject) { /* Logic */ }
            public void StoreScore(float score) { /* Logic */ }
            public float GetScore() { return 0; }
            public void AssignAssessment(int teacherID, int studentID) { /* Logic */ }
            public void SubmitAssessment(int studentID, int assessmentID) { /* Logic */ }
        }

    }

