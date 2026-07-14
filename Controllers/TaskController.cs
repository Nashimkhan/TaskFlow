using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using TaskFlow.Models;
using TaskFlow.Services.Interfaces;
using TaskFlow.Services.Implementations;
using TaskFlow.Data;
using Microsoft.EntityFrameworkCore;

namespace TaskFlow.Controllers
{
    [Authorize]
    public class TaskController : Controller
    {
        private readonly ITaskService _taskService;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly GroqAiService _groqAiService;
        private readonly AppDbContext _context;


        public TaskController(
              ITaskService taskService,
            UserManager<IdentityUser> userManager,
            GroqAiService groqAiService,
            AppDbContext context)
        {
            _taskService = taskService;
            _userManager = userManager;
            _groqAiService = groqAiService;
            _context = context;
        }

        private string GetUserId()
        {
            return _userManager.GetUserId(User)
                ?? throw new UnauthorizedAccessException();
        }

        public IActionResult Index(
            string? search,
            string? status,
            string? priority,
            int page = 1)
        {
            const int pageSize = 6;

            if (page < 1)
            {
                page = 1;
            }

            var tasks = _taskService.GetUserTasks(
                GetUserId(),
                search,
                status,
                priority);

            ViewBag.TotalTasks = tasks.Count;

            ViewBag.CompletedTasks =
                tasks.Count(t => t.Status == "Done");

            ViewBag.PendingTasks =
                tasks.Count(t => t.Status != "Done");

            ViewBag.OverdueTasks =
                tasks.Count(t =>
                    t.Status != "Done" &&
                    t.DueDate.Date < DateTime.UtcNow.Date);

            ViewBag.TotalPages =
                (int)Math.Ceiling(
                    tasks.Count / (double)pageSize);

            if (ViewBag.TotalPages > 0 &&
                page > ViewBag.TotalPages)
            {
                page = ViewBag.TotalPages;
            }

            ViewBag.CurrentPage = page;

            ViewBag.Search = search;
            ViewBag.Status = status;
            ViewBag.Priority = priority;

            var pagedTasks = _taskService.GetPagedTasks(
                tasks,
                page,
                pageSize);

            return View(pagedTasks);
        }

        [HttpGet]
        public async Task<IActionResult> Create(int? projectId)
        {
            ViewBag.ProjectId = projectId;

            if (projectId.HasValue)
            {
                var project = await _context.Projects
                    .Include(p => p.Members)
                    .FirstOrDefaultAsync(p => p.Id == projectId.Value);

                if (project == null)
                {
                    return NotFound();
                }

                if (project.OwnerId != GetUserId())
                {
                    return Forbid();
                }

                ViewBag.ProjectMembers =
                    await GetAssignableProjectUsers(project);
            }

            return View(new TaskItem
            {
                ProjectId = projectId
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(TaskItem task)
        {
            if (!await PrepareProjectTaskForCreate(task))
            {
                return Forbid();
            }

            if (!ModelState.IsValid)
            {
                await PopulateCreateViewData(task.ProjectId);

                return View(task);
            }

            _taskService.CreateTask(
                task,
                GetUserId());

            TempData["Success"] =
                "Task created successfully!";

            if (task.ProjectId.HasValue)
            {
                return RedirectToAction(
                    "Details",
                    "Project",
                    new { id = task.ProjectId.Value });
            }

            return RedirectToAction("Index");
        }

        public IActionResult Edit(int id)
        {
            var task = _taskService.GetTaskById(
                id,
                GetUserId());

            if (task == null)
            {
                return NotFound();
            }

            return View(task);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(TaskItem task)
        {
            if (!ModelState.IsValid)
            {
                return View(task);
            }

            var updated = _taskService.UpdateTask(
                task,
                GetUserId());

            if (!updated)
            {
                return NotFound();
            }

            TempData["Success"] =
                "Task updated successfully!";

            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Delete(int id)
        {
            var deleted = _taskService.DeleteTask(
                id,
                GetUserId());

            if (!deleted)
            {
                return NotFound();
            }

            TempData["Success"] =
                "Task deleted successfully!";

            return RedirectToAction("Index");
        }

        [HttpGet]
        public async Task<IActionResult> AiBreakdown(int? projectId)
        {
            if (!await PopulateProjectContext(projectId))
            {
                return Forbid();
            }

            return View();
        }

        // ==============================
        // AI TASK ASSISTANT
        // ==============================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AiBreakdown(
            string goal,
            int? projectId)
        {
            if (!await PopulateProjectContext(projectId))
            {
                return Forbid();
            }

            if (string.IsNullOrWhiteSpace(goal))
            {
                ViewBag.Error = "Please enter your goal.";

                return View();
            }

            try
            {
                var suggestions =
                    await _groqAiService.GenerateTasksAsync(goal);

                ViewBag.Goal = goal;

                return View(suggestions);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);

                ViewBag.Error =
                    "Unable to contact AI service.";

                return View();
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddAiTasks(
            List<AiSuggestedTask> tasks,
            List<int>? selectedTasks,
            int? projectId)
        {
            if (!await PopulateProjectContext(projectId))
            {
                return Forbid();
            }

            if (selectedTasks == null || selectedTasks.Count == 0)
            {
                TempData["Error"] =
                    "Please select at least one task.";

                return RedirectToAction(
                    "AiBreakdown",
                    new { projectId });
            }

            foreach (var index in selectedTasks)
            {
                if (index < 0 || index >= tasks.Count)
                    continue;

                var aiTask = tasks[index];

                _taskService.CreateTask(
                    new TaskItem
                    {
                        Title = aiTask.Title,
                        Description = aiTask.Description,
                        Priority = aiTask.Priority,
                        Status = "Pending",
                        DueDate = aiTask.SuggestedDueDate,
                        ProjectId = projectId
                    },
                    GetUserId());
            }

            TempData["Success"] =
                "AI tasks added successfully!";

            if (projectId.HasValue)
            {
                return RedirectToAction(
                    "Details",
                    "Project",
                    new { id = projectId.Value });
            }

            return RedirectToAction("Index");
        }

        private async Task<bool> PrepareProjectTaskForCreate(TaskItem task)
        {
            if (!task.ProjectId.HasValue)
            {
                task.AssignedUserId = null;

                return true;
            }

            var project = await _context.Projects
                .Include(p => p.Members)
                .FirstOrDefaultAsync(p => p.Id == task.ProjectId.Value);

            if (project == null)
            {
                ModelState.AddModelError(
                    nameof(task.ProjectId),
                    "Project was not found.");

                return true;
            }

            if (project.OwnerId != GetUserId())
            {
                return false;
            }

            var assignableUserIds = GetAssignableUserIds(project);

            if (!string.IsNullOrWhiteSpace(task.AssignedUserId) &&
                !assignableUserIds.Contains(task.AssignedUserId))
            {
                ModelState.AddModelError(
                    nameof(task.AssignedUserId),
                    "Choose the project owner or an accepted project member.");
            }

            ViewBag.ProjectMembers =
                await GetUsersByIds(assignableUserIds);

            return true;
        }

        private async Task PopulateCreateViewData(int? projectId)
        {
            ViewBag.ProjectId = projectId;

            if (!projectId.HasValue)
            {
                return;
            }

            var project = await _context.Projects
                .Include(p => p.Members)
                .FirstOrDefaultAsync(p => p.Id == projectId.Value);

            if (project != null)
            {
                ViewBag.ProjectMembers =
                    await GetAssignableProjectUsers(project);
            }
        }

        private async Task<List<IdentityUser>> GetAssignableProjectUsers(
            Project project)
        {
            return await GetUsersByIds(GetAssignableUserIds(project));
        }

        private static List<string> GetAssignableUserIds(Project project)
        {
            return project.Members
                .Where(m => m.Status == "Accepted")
                .Select(m => m.UserId)
                .Append(project.OwnerId)
                .Distinct()
                .ToList();
        }

        private async Task<List<IdentityUser>> GetUsersByIds(
            List<string> userIds)
        {
            return await _userManager.Users
                .Where(u => userIds.Contains(u.Id))
                .OrderBy(u => u.UserName)
                .ToListAsync();
        }

        private async Task<bool> PopulateProjectContext(int? projectId)
        {
            ViewBag.ProjectId = projectId;

            if (!projectId.HasValue)
            {
                return true;
            }

            var project = await _context.Projects
                .FirstOrDefaultAsync(p => p.Id == projectId.Value);

            if (project == null)
            {
                return false;
            }

            ViewBag.ProjectName = project.Name;

            return project.OwnerId == GetUserId();
        }

    }


}
