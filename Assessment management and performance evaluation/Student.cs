using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Assessment_management_and_performance_evaluation
{
    internal class Student
    {
        
            private string classLevel;
            private string stream;

            public string ClassLevel { get => classLevel; set => classLevel = value; }
            public string Stream { get => stream; set => stream = value; }

            public PerformanceReport WriteAssessment(int assessmentID, string assessmentName) { return new PerformanceReport(); }
            public PerformanceReport ViewPerformanceReport() { return new PerformanceReport(); }
            public AssistanceRequest RequestAssistance(AssistanceRequest request) { return request; }
        }

    }

