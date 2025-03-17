using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Assessment_management_and_performance_evaluation
{
    internal class PerformanceReport
    {
        
            private int reportID;
            private List<string> weakAreas;
            private int grades;
            private int studentID;
            private string subject;

            public int ReportID { get => reportID; set => reportID = value; }
            public List<string> WeakAreas { get => weakAreas; set => weakAreas = value; }
            public int Grades { get => grades; set => grades = value; }
            public int StudentID { get => studentID; set => studentID = value; }
            public string Subject { get => subject; set => subject = value; }

            public void GenerateReport(int studentID, string subject) { /* Logic */ }
            public List<string> FindWeakAreas() { return weakAreas; }
        }

    }

