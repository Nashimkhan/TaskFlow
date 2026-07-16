using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaskFlow.Data;
using TaskFlow.Models;

namespace TaskFlow.Controllers
{
    [Authorize]
    public class ProjectController : Controller
    {
        private readonly AppDbContext _context;
        private readonly UserManager<IdentityUser> _userManager;

        public ProjectController(AppDbContext context, UserManager<IdentityUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        private string GetUserId() =>
            _userManager.GetUserId(User) ?? throw new UnauthorizedAccessException();

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var userId = GetUserId();

            var projects = await _context.Projects
                .Where(p => p.OwnerId == userId)
                .Include(p => p.Tasks)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();

            return View(projects);
        }

        [HttpGet]
        public IActionResult Create()
        {
            return View(new Project { Deadline = DateTime.Today.AddDays(7) });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Project project)
        {
            if (!ModelState.IsValid)
                return View(project);

            project.OwnerId = GetUserId();
            project.CreatedAt = DateTime.UtcNow;
            project.Deadline = DateTime.SpecifyKind(project.Deadline, DateTimeKind.Utc);

            _context.Projects.Add(project);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Project created successfully.";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var userId = GetUserId();

            var project = await _context.Projects
                .Include(p => p.Tasks)
                .FirstOrDefaultAsync(p => p.Id == id && p.OwnerId == userId);

            if (project == null)
                return NotFound();

            return View(project);
        }

        [HttpGet]
        public async Task<IActionResult> EditTask(int id)
        {
            var userId = GetUserId();

            var task = await _context.Tasks
                .Include(t => t.Project)
                .FirstOrDefaultAsync(t => t.Id == id &&
                                          t.Project != null &&
                                          t.Project.OwnerId == userId);

            if (task == null)
                return NotFound();

            return View(task); 
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditTask(TaskItem model)
        {
            var userId = GetUserId();

            var task = await _context.Tasks
                .Include(t => t.Project)
                .FirstOrDefaultAsync(t => t.Id == model.Id &&
                                          t.Project != null &&
                                          t.Project.OwnerId == userId);

            if (task == null)
                return NotFound();

            task.Title = model.Title;
            task.Description = model.Description;
            task.Status = model.Status;
            task.Priority = model.Priority;
            task.DueDate = DateTime.SpecifyKind(model.DueDate, DateTimeKind.Utc);

            await _context.SaveChangesAsync();

            TempData["Success"] = "Project task updated.";
            return RedirectToAction(nameof(Details), new { id = task.ProjectId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteTask(int id)
        {
            var userId = GetUserId();

            var task = await _context.Tasks
                .Include(t => t.Project)
                .FirstOrDefaultAsync(t => t.Id == id &&
                                          t.Project != null &&
                                          t.Project.OwnerId == userId);

            if (task == null)
                return NotFound();

            var projectId = task.ProjectId;
            _context.Tasks.Remove(task);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Task deleted successfully.";
            return RedirectToAction(nameof(Details), new { id = projectId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CompleteTask(int id)
        {
            var userId = GetUserId();

            var task = await _context.Tasks
                .Include(t => t.Project)
                .FirstOrDefaultAsync(t => t.Id == id &&
                                          t.Project != null &&
                                          t.Project.OwnerId == userId);

            if (task == null)
                return NotFound();

            task.Status = "Done";
            await _context.SaveChangesAsync();

            TempData["Success"] = "Task marked as completed.";
            return RedirectToAction(nameof(Details), new { id = task.ProjectId });
        }
    }
}
