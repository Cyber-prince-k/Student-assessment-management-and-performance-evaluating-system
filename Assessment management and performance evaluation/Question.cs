using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Windows.Forms.Design.AxImporter;

namespace Assessment_management_and_performance_evaluation
{
    internal class Question
    {
        public string QuestionText { get; set; }
        public string QuestionType { get; set; }
        public int Marks { get; set; }

        // ✅ Add List to Store Options (for Multiple Choice)
        public List<string> Options { get; set; }

        // ✅ Store Correct Answer (for Multiple Choice)
        public string CorrectAnswer { get; set; }

        public Question()
        {
            Options = new List<string>(); // Initialize options list
        }
    }
}
