using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebApplication1.Controllers
{
    [Authorize(Roles = "Lecturer")]
    public class LecturerDashboardController : Controller
    {
        // Main Dashboard
        public IActionResult Index()
        {
            return View();
        }

        // Upload Study Content
        public IActionResult ContentUpload()
        {
            return View(); // Views/LecturerDashboard/ContentUpload.cshtml
        }

        // Create Quizzes & Assignments
        public IActionResult QuizAssignment()
        {
            ViewBag.Faculties = new List<string>
            {
                "FACULTY OF ACCOUNTING & INFORMATICS",
                "FACULTY OF APPLIED SCIENCES",
                "FACULTY OF ARTS AND DESIGN",
                "FACULTY OF ENGINEERING & THE BUILT ENVIRONMENT",
                "FACULTY OF HEALTH SCIENCES",
                "FACULTY OF MANAGEMENT SCIENCES"
            };

            return View();
        }

        [HttpPost]
        public IActionResult CreateQuiz(string faculty, string course, string module, string title, string description, DateTime deadline)
        {
            // Logic for storing the quiz/assignment details in the database
            // Example:
            // var quiz = new Quiz
            // {
            //     Faculty = faculty,
            //     Course = course,
            //     Module = module,
            //     Title = title,
            //     Description = description,
            //     Deadline = deadline
            // };
            // _context.Quizzes.Add(quiz);
            // _context.SaveChanges();

            TempData["SuccessMessage"] = "Quiz/Assignment created successfully!";
            return RedirectToAction(nameof(QuizAssignment));
        }

        // Set Up Online Classes
        public IActionResult OnlineClass()
        {
            return View(); // Views/LecturerDashboard/OnlineClass.cshtml
        }

        // Track Student Performance
        public IActionResult Performance()
        {
            return View(); // Views/LecturerDashboard/Performance.cshtml
        }
    }
}