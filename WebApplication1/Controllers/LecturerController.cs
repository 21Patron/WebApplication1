﻿using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Threading.Tasks;
using WebApplication1.Data;
using WebApplication1.Models;
using WebApplication1.Models.ViewModels;

namespace WebApplication1.Controllers
{
    [Authorize(Roles = "Lecturer")]
    public class LecturerController : Controller
    {
        private readonly NexelContext _context;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public LecturerController(
            NexelContext context,
            UserManager<IdentityUser> userManager,
            IWebHostEnvironment webHostEnvironment)
        {
            _context = context;
            _userManager = userManager;
            _webHostEnvironment = webHostEnvironment;
        }

        // GET: Dashboard
        public async Task<IActionResult> Dashboard()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            // Get all modules assigned to this lecturer
            var modules = await _context.ModuleLecturers
                .Where(ml => ml.LecturerId == userId)
                .Include(ml => ml.Module)
                .ThenInclude(m => m.Course)
                .Select(ml => ml.Module)
                .OrderBy(m => m.Course.Faculty)
                .ThenBy(m => m.Course.CourseName)
                .ThenBy(m => m.ModuleName)
                .ToListAsync();

            // Count study materials and quizzes for each module
            var moduleStats = new List<ModuleStatViewModel>();
            foreach (var module in modules)
            {
                var studyMaterialsCount = await _context.StudyMaterials
                    .CountAsync(sm => sm.ModuleId == module.ModuleId);

                var quizzesCount = await _context.Quizzes
                    .CountAsync(q => q.ModuleId == module.ModuleId);

                moduleStats.Add(new ModuleStatViewModel
                {
                    Module = module,
                    StudyMaterialsCount = studyMaterialsCount,
                    QuizzesCount = quizzesCount
                });
            }


            var viewModel = new LecturerDashboardViewModel
            {
                ModuleStats = moduleStats
            };

            return View(viewModel);
        }

        // GET: Module details
        public async Task<IActionResult> ModuleDetails(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            // Check if lecturer is assigned to this module
            var moduleAssignment = await _context.ModuleLecturers
                .FirstOrDefaultAsync(ml => ml.ModuleId == id && ml.LecturerId == userId);

            if (moduleAssignment == null)
            {
                return Forbid();
            }

            var module = await _context.Modules
                .Include(m => m.Course)
                .FirstOrDefaultAsync(m => m.ModuleId == id);

            if (module == null)
            {
                return NotFound();
            }

            // Get study materials for this module
            var studyMaterials = await _context.StudyMaterials
                .Where(sm => sm.ModuleId == id)
                .OrderByDescending(sm => sm.UploadedDate)
                .ToListAsync();

            // Get quizzes for this module
            var quizzes = await _context.Quizzes
                .Where(q => q.ModuleId == id)
                .OrderByDescending(q => q.CreatedDate)
                .ToListAsync();

            var viewModel = new ModuleDetailsViewModel
            {
                Module = module,
                StudyMaterials = studyMaterials,
                Quizzes = quizzes
            };

            return View(viewModel);
        }

        #region Study Materials

        // GET: Add study material
        public async Task<IActionResult> AddStudyMaterial(int moduleId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            // Check if lecturer is assigned to this module
            var moduleAssignment = await _context.ModuleLecturers
                .FirstOrDefaultAsync(ml => ml.ModuleId == moduleId && ml.LecturerId == userId);

            if (moduleAssignment == null)
            {
                return Forbid();
            }

            var module = await _context.Modules
                .Include(m => m.Course)
                .FirstOrDefaultAsync(m => m.ModuleId == moduleId);

            if (module == null)
            {
                return NotFound();
            }

            var viewModel = new AddStudyMaterialViewModel
            {
                ModuleId = moduleId,
                ModuleName = module.ModuleName,
                CourseName = module.Course.CourseName
            };

            return View(viewModel);
        }

        // POST: Add study material
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddStudyMaterial(AddStudyMaterialViewModel model)
        {
            if (ModelState.IsValid)
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

                // Check if lecturer is assigned to this module
                var moduleAssignment = await _context.ModuleLecturers
                    .FirstOrDefaultAsync(ml => ml.ModuleId == model.ModuleId && ml.LecturerId == userId);

                if (moduleAssignment == null)
                {
                    return Forbid();
                }

                var studyMaterial = new StudyMaterial
                {
                    Title = model.Title,
                    Description = model.Description,
                    Type = model.Type,
                    ModuleId = model.ModuleId,
                    UploadedById = userId,
                    UploadedDate = DateTime.UtcNow
                };

                // Handle file upload or URL
                if (model.ResourceFile != null)
                {
                    string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "uploads", "materials");
                    Directory.CreateDirectory(uploadsFolder); // Ensure directory exists

                    string uniqueFileName = Guid.NewGuid().ToString() + "_" + model.ResourceFile.FileName;
                    string filePath = Path.Combine(uploadsFolder, uniqueFileName);

                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        await model.ResourceFile.CopyToAsync(fileStream);
                    }

                    studyMaterial.ResourceUrl = "/uploads/materials/" + uniqueFileName;
                }
                else if (!string.IsNullOrEmpty(model.ResourceUrl))
                {
                    studyMaterial.ResourceUrl = model.ResourceUrl;
                }

                _context.StudyMaterials.Add(studyMaterial);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Study material added successfully.";
                return RedirectToAction(nameof(ModuleDetails), new { id = model.ModuleId });
            }

            return View(model);
        }

        // GET: Edit study material
        public async Task<IActionResult> EditStudyMaterial(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var studyMaterial = await _context.StudyMaterials
                .Include(sm => sm.Module)
                .ThenInclude(m => m.Course)
                .FirstOrDefaultAsync(sm => sm.Id == id);

            if (studyMaterial == null)
            {
                return NotFound();
            }

            // Check if lecturer is assigned to this module
            var moduleAssignment = await _context.ModuleLecturers
                .FirstOrDefaultAsync(ml => ml.ModuleId == studyMaterial.ModuleId && ml.LecturerId == userId);

            if (moduleAssignment == null)
            {
                return Forbid();
            }

            var viewModel = new EditStudyMaterialViewModel
            {
                Id = studyMaterial.Id,
                Title = studyMaterial.Title,
                Description = studyMaterial.Description,
                Type = studyMaterial.Type,
                ResourceUrl = studyMaterial.ResourceUrl,
                ModuleId = studyMaterial.ModuleId,
                ModuleName = studyMaterial.Module.ModuleName,
                CourseName = studyMaterial.Module.Course.CourseName
            };

            return View(viewModel);
        }

        // POST: Edit study material
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditStudyMaterial(EditStudyMaterialViewModel model)
        {
            if (ModelState.IsValid)
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

                var studyMaterial = await _context.StudyMaterials.FindAsync(model.Id);

                if (studyMaterial == null)
                {
                    return NotFound();
                }

                // Check if lecturer is assigned to this module
                var moduleAssignment = await _context.ModuleLecturers
                    .FirstOrDefaultAsync(ml => ml.ModuleId == studyMaterial.ModuleId && ml.LecturerId == userId);

                if (moduleAssignment == null)
                {
                    return Forbid();
                }

                studyMaterial.Title = model.Title;
                studyMaterial.Description = model.Description;
                studyMaterial.Type = model.Type;

                // Handle file upload or URL update
                if (model.ResourceFile != null)
                {
                    // Delete old file if it exists and is stored locally
                    if (!string.IsNullOrEmpty(studyMaterial.ResourceUrl) &&
                        studyMaterial.ResourceUrl.StartsWith("/uploads/"))
                    {
                        var oldFilePath = Path.Combine(_webHostEnvironment.WebRootPath,
                            studyMaterial.ResourceUrl.TrimStart('/'));

                        if (System.IO.File.Exists(oldFilePath))
                        {
                            System.IO.File.Delete(oldFilePath);
                        }
                    }

                    // Save new file
                    string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "uploads", "materials");
                    Directory.CreateDirectory(uploadsFolder);

                    string uniqueFileName = Guid.NewGuid().ToString() + "_" + model.ResourceFile.FileName;
                    string filePath = Path.Combine(uploadsFolder, uniqueFileName);

                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        await model.ResourceFile.CopyToAsync(fileStream);
                    }

                    studyMaterial.ResourceUrl = "/uploads/materials/" + uniqueFileName;
                }
                else if (!string.IsNullOrEmpty(model.ResourceUrl) && model.ResourceUrl != studyMaterial.ResourceUrl)
                {
                    // Update URL only if it's different
                    studyMaterial.ResourceUrl = model.ResourceUrl;
                }

                _context.Update(studyMaterial);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Study material updated successfully.";
                return RedirectToAction(nameof(ModuleDetails), new { id = studyMaterial.ModuleId });
            }

            return View(model);
        }

        // POST: Delete study material
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteStudyMaterial(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var studyMaterial = await _context.StudyMaterials.FindAsync(id);

            if (studyMaterial == null)
            {
                return NotFound();
            }

            // Check if lecturer is assigned to this module
            var moduleAssignment = await _context.ModuleLecturers
                .FirstOrDefaultAsync(ml => ml.ModuleId == studyMaterial.ModuleId && ml.LecturerId == userId);

            if (moduleAssignment == null)
            {
                return Forbid();
            }

            // Delete file if it exists and is stored locally
            if (!string.IsNullOrEmpty(studyMaterial.ResourceUrl) &&
                studyMaterial.ResourceUrl.StartsWith("/uploads/"))
            {
                var filePath = Path.Combine(_webHostEnvironment.WebRootPath,
                    studyMaterial.ResourceUrl.TrimStart('/'));

                if (System.IO.File.Exists(filePath))
                {
                    System.IO.File.Delete(filePath);
                }
            }

            int moduleId = studyMaterial.ModuleId;
            _context.StudyMaterials.Remove(studyMaterial);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Study material deleted successfully.";
            return RedirectToAction(nameof(ModuleDetails), new { id = moduleId });
        }

        #endregion

        #region Quiz Management

        // GET: Create Quiz
        public async Task<IActionResult> CreateQuiz(int moduleId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            // Check if lecturer is assigned to this module
            var moduleAssignment = await _context.ModuleLecturers
                .FirstOrDefaultAsync(ml => ml.ModuleId == moduleId && ml.LecturerId == userId);

            if (moduleAssignment == null)
            {
                return Forbid();
            }

            var module = await _context.Modules
                .Include(m => m.Course)
                .FirstOrDefaultAsync(m => m.ModuleId == moduleId);

            if (module == null)
            {
                return NotFound();
            }

            var viewModel = new CreateQuizViewModel
            {
                ModuleId = moduleId,
                ModuleName = module.ModuleName,
                CourseName = module.Course.CourseName
            };

            return View(viewModel);
        }

        // POST: Create Quiz
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateQuiz(CreateQuizViewModel model)
        {
            
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

                // Check if lecturer is assigned to this module
                var moduleAssignment = await _context.ModuleLecturers
                    .FirstOrDefaultAsync(ml => ml.ModuleId == model.ModuleId && ml.LecturerId == userId);

                if (moduleAssignment == null)
                {
                    return Forbid();
                }

                var quiz = new Quiz
                {
                    Title = model.Title,
                    Description = model.Description,
                    ModuleId = model.ModuleId,
                    CreatedById = userId,
                    CreatedDate = DateTime.UtcNow,
                    StartDate = model.StartDate,
                    EndDate = model.EndDate,
                    TimeLimit = model.TimeLimit,
                    IsPublished = model.IsPublished
                };

                _context.Quizzes.Add(quiz);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Quiz created successfully.";
                return RedirectToAction(nameof(EditQuiz), new { id = quiz.QuizId });
            
        }

        // GET: Edit Quiz
        public async Task<IActionResult> EditQuiz(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var quiz = await _context.Quizzes
                .Include(q => q.Module)
                .ThenInclude(m => m.Course)
                .Include(q => q.Questions)
                .FirstOrDefaultAsync(q => q.QuizId == id);

            if (quiz == null)
            {
                return NotFound();
            }

            // Check if lecturer is assigned to this module
            var moduleAssignment = await _context.ModuleLecturers
                .FirstOrDefaultAsync(ml => ml.ModuleId == quiz.ModuleId && ml.LecturerId == userId);

            if (moduleAssignment == null)
            {
                return Forbid();
            }

            var viewModel = new EditQuizViewModel
            {
                QuizId = quiz.QuizId,
                Title = quiz.Title,
                Description = quiz.Description,
                ModuleId = quiz.ModuleId,
                ModuleName = quiz.Module.ModuleName,
                CourseName = quiz.Module.Course.CourseName,
                StartDate = quiz.StartDate,
                EndDate = quiz.EndDate,
                TimeLimit = quiz.TimeLimit,
                IsPublished = quiz.IsPublished,
                Questions = quiz.Questions.ToList()
            };

            return View(viewModel);
        }

        // POST: Edit Quiz
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditQuiz(EditQuizViewModel model)
        {
            
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

                var quiz = await _context.Quizzes.FindAsync(model.QuizId);

                if (quiz == null)
                {
                    return NotFound();
                }

                // Check if lecturer is assigned to this module
                var moduleAssignment = await _context.ModuleLecturers
                    .FirstOrDefaultAsync(ml => ml.ModuleId == quiz.ModuleId && ml.LecturerId == userId);

                if (moduleAssignment == null)
                {
                    return Forbid();
                }

                quiz.Title = model.Title;
                quiz.Description = model.Description;
                quiz.StartDate = model.StartDate;
                quiz.EndDate = model.EndDate;
                quiz.TimeLimit = model.TimeLimit;
                quiz.IsPublished = model.IsPublished;

                _context.Update(quiz);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Quiz updated successfully.";
                return RedirectToAction(nameof(EditQuiz), new { id = quiz.QuizId });
            

            return View(model);
        }

        // GET: Add Question to Quiz
        public async Task<IActionResult> AddQuizQuestion(int quizId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var quiz = await _context.Quizzes
                .Include(q => q.Module)
                .FirstOrDefaultAsync(q => q.QuizId == quizId);

            if (quiz == null)
            {
                return NotFound();
            }

            // Check if lecturer is assigned to this module
            var moduleAssignment = await _context.ModuleLecturers
                .FirstOrDefaultAsync(ml => ml.ModuleId == quiz.ModuleId && ml.LecturerId == userId);

            if (moduleAssignment == null)
            {
                return Forbid();
            }

            var viewModel = new AddQuizQuestionViewModel
            {
                QuizId = quizId,
                QuizTitle = quiz.Title
            };

            return View(viewModel);
        }

        // POST: Add Question to Quiz
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddQuizQuestion(AddQuizQuestionViewModel model)
        {
            
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

                var quiz = await _context.Quizzes
                    .Include(q => q.Module)
                    .FirstOrDefaultAsync(q => q.QuizId == model.QuizId);

                if (quiz == null)
                {
                    return NotFound();
                }

                // Check if lecturer is assigned to this module
                var moduleAssignment = await _context.ModuleLecturers
                    .FirstOrDefaultAsync(ml => ml.ModuleId == quiz.ModuleId && ml.LecturerId == userId);

                if (moduleAssignment == null)
                {
                    return Forbid();
                }

                var question = new QuizQuestion
                {
                    QuizId = model.QuizId,
                    QuestionText = model.QuestionText,
                    Type = model.Type,
                    Points = model.Points
                };

                // Handle options for multiple choice
                if (model.Type == QuestionType.MultipleChoice && model.Options != null && model.Options.Count > 0)
                {
                    question.OptionsJson = System.Text.Json.JsonSerializer.Serialize(model.Options);
                }

                // Handle correct answer
                if (model.Type == QuestionType.MultipleChoice || model.Type == QuestionType.TrueFalse)
                {
                    question.CorrectAnswer = model.CorrectAnswer;
                }

                // Handle image upload if provided
                if (model.ImageFile != null)
                {
                    string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "uploads", "quizimages");
                    Directory.CreateDirectory(uploadsFolder);

                    string uniqueFileName = Guid.NewGuid().ToString() + "_" + model.ImageFile.FileName;
                    string filePath = Path.Combine(uploadsFolder, uniqueFileName);

                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        await model.ImageFile.CopyToAsync(fileStream);
                    }

                    question.ImageUrl = "/uploads/quizimages/" + uniqueFileName;
                }

                _context.QuizQuestions.Add(question);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Question added successfully.";
                return RedirectToAction(nameof(EditQuiz), new { id = model.QuizId });
            

            return View(model);
        }

        // GET: Edit Quiz Question
        public async Task<IActionResult> EditQuizQuestion(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var question = await _context.QuizQuestions
                .Include(q => q.Quiz)
                .ThenInclude(q => q.Module)
                .FirstOrDefaultAsync(q => q.QuestionId == id);

            if (question == null)
            {
                return NotFound();
            }

            // Check if lecturer is assigned to this module
            var moduleAssignment = await _context.ModuleLecturers
                .FirstOrDefaultAsync(ml => ml.ModuleId == question.Quiz.ModuleId && ml.LecturerId == userId);

            if (moduleAssignment == null)
            {
                return Forbid();
            }

            var viewModel = new EditQuizQuestionViewModel
            {
                QuestionId = question.QuestionId,
                QuizId = question.QuizId,
                QuizTitle = question.Quiz.Title,
                QuestionText = question.QuestionText,
                Type = question.Type,
                Points = question.Points,
                CorrectAnswer = question.CorrectAnswer,
                ImageUrl = question.ImageUrl
            };

            // Deserialize options if they exist
            if (question.Type == QuestionType.MultipleChoice && !string.IsNullOrEmpty(question.OptionsJson))
            {
                viewModel.Options = System.Text.Json.JsonSerializer.Deserialize<List<string>>(question.OptionsJson);
            }

            return View(question);
        }

        // POST: Edit Quiz Question
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditQuizQuestion(EditQuizQuestionViewModel model)
        {
            if (ModelState.IsValid)
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

                var question = await _context.QuizQuestions
                    .Include(q => q.Quiz)
                    .ThenInclude(q => q.Module)
                    .FirstOrDefaultAsync(q => q.QuestionId == model.QuestionId);

                if (question == null)
                {
                    return NotFound();
                }

                // Check if lecturer is assigned to this module
                var moduleAssignment = await _context.ModuleLecturers
                    .FirstOrDefaultAsync(ml => ml.ModuleId == question.Quiz.ModuleId && ml.LecturerId == userId);

                if (moduleAssignment == null)
                {
                    return Forbid();
                }

                question.QuestionText = model.QuestionText;
                question.Type = model.Type;
                question.Points = model.Points;

                // Handle options for multiple choice
                if (model.Type == QuestionType.MultipleChoice && model.Options != null && model.Options.Count > 0)
                {
                    question.OptionsJson = System.Text.Json.JsonSerializer.Serialize(model.Options);
                }
                else
                {
                    question.OptionsJson = null;
                }

                // Handle correct answer
                if (model.Type == QuestionType.MultipleChoice || model.Type == QuestionType.TrueFalse)
                {
                    question.CorrectAnswer = model.CorrectAnswer;
                }
                else
                {
                    question.CorrectAnswer = null;
                }

                // Handle image upload if provided
                if (model.ImageFile != null)
                {
                    // Delete old image if it exists
                    if (!string.IsNullOrEmpty(question.ImageUrl) &&
                        question.ImageUrl.StartsWith("/uploads/"))
                    {
                        var oldFilePath = Path.Combine(_webHostEnvironment.WebRootPath,
                            question.ImageUrl.TrimStart('/'));

                        if (System.IO.File.Exists(oldFilePath))
                        {
                            System.IO.File.Delete(oldFilePath);
                        }
                    }

                    // Upload new image
                    string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "uploads", "quizimages");
                    Directory.CreateDirectory(uploadsFolder);

                    string uniqueFileName = Guid.NewGuid().ToString() + "_" + model.ImageFile.FileName;
                    string filePath = Path.Combine(uploadsFolder, uniqueFileName);

                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        await model.ImageFile.CopyToAsync(fileStream);
                    }

                    question.ImageUrl = "/uploads/quizimages/" + uniqueFileName;
                }
                else if (model.RemoveImage && !string.IsNullOrEmpty(question.ImageUrl))
                {
                    // Delete image if remove flag is set
                    if (question.ImageUrl.StartsWith("/uploads/"))
                    {
                        var filePath = Path.Combine(_webHostEnvironment.WebRootPath,
                            question.ImageUrl.TrimStart('/'));

                        if (System.IO.File.Exists(filePath))
                        {
                            System.IO.File.Delete(filePath);
                        }
                    }

                    question.ImageUrl = null;
                }

                _context.Update(question);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Question updated successfully.";
                return RedirectToAction(nameof(EditQuiz), new { id = question.QuizId });
            }

            return View(model);
        }

        // POST: Delete Quiz Question
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteQuizQuestion(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var question = await _context.QuizQuestions
                .Include(q => q.Quiz)
                .ThenInclude(q => q.Module)
                .FirstOrDefaultAsync(q => q.QuestionId == id);

            if (question == null)
            {
                return NotFound();
            }

            // Check if lecturer is assigned to this module
            var moduleAssignment = await _context.ModuleLecturers
                .FirstOrDefaultAsync(ml => ml.ModuleId == question.Quiz.ModuleId && ml.LecturerId == userId);

            if (moduleAssignment == null)
            {
                return Forbid();
            }

            // Delete image if it exists
            if (!string.IsNullOrEmpty(question.ImageUrl) &&
                question.ImageUrl.StartsWith("/uploads/"))
            {
                var filePath = Path.Combine(_webHostEnvironment.WebRootPath,
                    question.ImageUrl.TrimStart('/'));

                if (System.IO.File.Exists(filePath))
                {
                    System.IO.File.Delete(filePath);
                }
            }

            int quizId = question.QuizId;
            _context.QuizQuestions.Remove(question);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Question deleted successfully.";
            return RedirectToAction(nameof(EditQuiz), new { id = quizId });
        }

        // POST: Delete Quiz
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteQuiz(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var quiz = await _context.Quizzes
                .Include(q => q.Module)
                .Include(q => q.Questions)
                .FirstOrDefaultAsync(q => q.QuizId == id);

            if (quiz == null)
            {
                return NotFound();
            }

            // Check if lecturer is assigned to this module
            var moduleAssignment = await _context.ModuleLecturers
                .FirstOrDefaultAsync(ml => ml.ModuleId == quiz.ModuleId && ml.LecturerId == userId);

            if (moduleAssignment == null)
            {
                return Forbid();
            }

            // Delete all associated questions and their images
            foreach (var question in quiz.Questions)
            {
                if (!string.IsNullOrEmpty(question.ImageUrl) &&
                    question.ImageUrl.StartsWith("/uploads/"))
                {
                    var filePath = Path.Combine(_webHostEnvironment.WebRootPath,
                        question.ImageUrl.TrimStart('/'));

                    if (System.IO.File.Exists(filePath))
                    {
                        System.IO.File.Delete(filePath);
                    }
                }
            }

            int moduleId = quiz.ModuleId;
            _context.Quizzes.Remove(quiz);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Quiz deleted successfully.";
            return RedirectToAction(nameof(ModuleDetails), new { id = moduleId });
        }

        // GET: View Quiz Attempts
        public async Task<IActionResult> QuizAttempts(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var quiz = await _context.Quizzes
                .Include(q => q.Module)
                .Include(q => q.Attempts)
                .ThenInclude(a => a.Student)
                .FirstOrDefaultAsync(q => q.QuizId == id);

            if (quiz == null)
            {
                return NotFound();
            }

            // Check if lecturer is assigned to this module
            var moduleAssignment = await _context.ModuleLecturers
                .FirstOrDefaultAsync(ml => ml.ModuleId == quiz.ModuleId && ml.LecturerId == userId);

            if (moduleAssignment == null)
            {
                return Forbid();
            }

            var viewModel = new QuizAttemptsViewModel
            {
                Quiz = quiz,
                Attempts = quiz.Attempts.ToList()
            };

            return View(viewModel);
        }

        // GET: Grade Quiz Attempt
        public async Task<IActionResult> GradeQuizAttempt(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var attempt = await _context.StudentQuizAttempts
                .Include(a => a.Quiz)
                .ThenInclude(q => q.Module)
                .Include(a => a.Student)
                .Include(a => a.Answers)
                .ThenInclude(a => a.Question)
                .FirstOrDefaultAsync(a => a.AttemptId == id);

            if (attempt == null)
            {
                return NotFound();
            }

            // Check if lecturer is assigned to this module
            var moduleAssignment = await _context.ModuleLecturers
                .FirstOrDefaultAsync(ml => ml.ModuleId == attempt.Quiz.ModuleId && ml.LecturerId == userId);

            if (moduleAssignment == null)
            {
                return Forbid();
            }

            var viewModel = new GradeQuizAttemptViewModel
            {
                AttemptId = attempt.AttemptId,
                QuizId = attempt.QuizId,
                QuizTitle = attempt.Quiz.Title,
                StudentName = attempt.Student.UserName,
                StartTime = attempt.StartTime,
                SubmissionTime = attempt.SubmissionTime,
                FeedbackFromLecturer = attempt.FeedbackFromLecturer,
                Answers = attempt.Answers.ToList()
            };

            return View(viewModel);
        }

        // POST: Submit Quiz Grades
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitQuizGrades(GradeQuizSubmissionViewModel model)
        {
            if (ModelState.IsValid)
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

                var attempt = await _context.StudentQuizAttempts
                    .Include(a => a.Quiz)
                    .ThenInclude(q => q.Module)
                    .Include(a => a.Answers)
                    .FirstOrDefaultAsync(a => a.AttemptId == model.AttemptId);

                if (attempt == null)
                {
                    return NotFound();
                }

                // Check if lecturer is assigned to this module
                var moduleAssignment = await _context.ModuleLecturers
                    .FirstOrDefaultAsync(ml => ml.ModuleId == attempt.Quiz.ModuleId && ml.LecturerId == userId);

                if (moduleAssignment == null)
                {
                    return Forbid();
                }

                // Update attempt feedback
                attempt.FeedbackFromLecturer = model.FeedbackFromLecturer;

                // Update grades for each answer
                int totalPoints = 0;

                foreach (var gradeItem in model.GradedAnswers)
                {
                    var answer = await _context.StudentAnswers.FindAsync(gradeItem.AnswerId);
                    if (answer != null && answer.AttemptId == attempt.AttemptId)
                    {
                        answer.PointsAwarded = gradeItem.PointsAwarded;
                        answer.FeedbackFromLecturer = gradeItem.Feedback;
                        answer.IsCorrect = gradeItem.IsCorrect;
                        totalPoints += gradeItem.PointsAwarded ?? 0;
                    }
                }

                // Update total score
                attempt.Score = totalPoints;

                _context.Update(attempt);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Quiz grades submitted successfully.";
                return RedirectToAction(nameof(QuizAttempts), new { id = attempt.QuizId });
            }

            return RedirectToAction(nameof(GradeQuizAttempt), new { id = model.AttemptId });
        }

        #endregion

        #region Progress Tracking

        // GET: Student Progress
        public async Task<IActionResult> StudentProgress(int moduleId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            // Check if lecturer is assigned to this module
            var moduleAssignment = await _context.ModuleLecturers
                .FirstOrDefaultAsync(ml => ml.ModuleId == moduleId && ml.LecturerId == userId);

            if (moduleAssignment == null)
            {
                return Forbid();
            }

            var module = await _context.Modules
                .Include(m => m.Course)
                .FirstOrDefaultAsync(m => m.ModuleId == moduleId);

            if (module == null)
            {
                return NotFound();
            }

            // Get all quizzes for this module
            var quizzes = await _context.Quizzes
                .Where(q => q.ModuleId == moduleId)
                .ToListAsync();

            // Get all students who have enrolled in this course
            var students = await _context.Applications
                .Where(a => a.Course.Id == module.CourseId &&
                       a.Status == Application.ApplicationStatus.Approved &&
                       a.PaymentId != null)
                .Include(a => a.IdentityUser)
                .Select(a => a.IdentityUser)
                .ToListAsync();

            // Create student progress records
            var studentProgressList = new List<StudentProgressViewModel>();

            foreach (var student in students)
            {
                // Get all quiz attempts by this student for quizzes in this module
                var quizAttempts = await _context.StudentQuizAttempts
                    .Where(a => a.StudentId == student.Id &&
                           quizzes.Select(q => q.QuizId).Contains(a.QuizId))
                    .Include(a => a.Quiz)
                    .ToListAsync();

                // Calculate statistics
                int totalQuizzes = quizzes.Count;
                int attemptedQuizzes = quizAttempts.Select(a => a.QuizId).Distinct().Count();
                int completedQuizzes = quizAttempts.Where(a => a.IsSubmitted).Select(a => a.QuizId).Distinct().Count();
                double averageScore = quizAttempts.Where(a => a.Score.HasValue).Any()
                    ? quizAttempts.Where(a => a.Score.HasValue).Average(a => a.Score.Value)
                    : 0;

                var studentProgress = new StudentProgressViewModel
                {
                    Student = student,
                    TotalQuizzes = totalQuizzes,
                    AttemptedQuizzes = attemptedQuizzes,
                    CompletedQuizzes = completedQuizzes,
                    AverageScore = averageScore,
                    QuizAttempts = quizAttempts
                };

                studentProgressList.Add(studentProgress);
            }

            var viewModel = new ModuleStudentProgressViewModel
            {
                Module = module,
                StudentProgressList = studentProgressList
            };

            return View(viewModel);
        }

        #endregion
    }
}