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
            return View(); // Views/LecturerDashboard/QuizAssignment.cshtml
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