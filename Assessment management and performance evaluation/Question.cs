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


        public string Text { get; set; }
        public string Answer { get; set; }

        public int QuestionID { get; set; }
        public string QuestionText { get; set; }
        public string QuestionType { get; set; }
        public int Marks { get; set; }
        public List<Option> Options { get; set; }  // Store Options

        public Question()
        {
            Options = new List<Option>(); // Initialize options list
        }
    }

    // ✅ Define the Option Class
    internal class Option
    {
        public string Text { get; set; }
        public bool IsCorrect { get; set; } // True if correct option
    }

}
