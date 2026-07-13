using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaskFlow.Data;
using TaskFlow.Models;

namespace TaskFlow.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly AppDbContext _context;
        private readonly UserManager<IdentityUser> _userManager;

        public AdminController(
            AppDbContext context,
            UserManager<IdentityUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            var users = await _userManager.Users.ToListAsync();
            var tasks = await _context.Tasks.ToListAsync();

            ViewBag.TotalUsers = users.Count;
            ViewBag.TotalTasks = tasks.Count;
            ViewBag.CompletedTasks =
                tasks.Count(t => t.Status == "Done");

            ViewBag.PendingTasks =
                tasks.Count(t => t.Status != "Done");

            return View(users);
        }

        public async Task<IActionResult> UserTasks(string id)
        {
            var user = await _userManager.FindByIdAsync(id);

            if (user == null)
            {
                return NotFound();
            }

            var tasks = await _context.Tasks
                .Where(t => t.UserId == id)
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync();

            ViewBag.Username = user.UserName;
            ViewBag.UserId = user.Id;

            return View(tasks);
        }

        public async Task<IActionResult> EditTask(int id)
        {
            var task = await _context.Tasks
                .FirstOrDefaultAsync(t => t.Id == id);

            if (task == null)
            {
                return NotFound();
            }

            return View(task);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditTask(TaskItem task)
        {
            var existingTask = await _context.Tasks
                .FirstOrDefaultAsync(t => t.Id == task.Id);

            if (existingTask == null)
            {
                return NotFound();
            }

            if (string.IsNullOrWhiteSpace(task.Title))
            {
                ViewBag.Error = "Task title is required";
                return View(task);
            }

            existingTask.Title = task.Title.Trim();
            existingTask.Description =
                task.Description?.Trim() ?? string.Empty;

            existingTask.Status = task.Status;
            existingTask.Priority = task.Priority;

            existingTask.DueDate = DateTime.SpecifyKind(
                task.DueDate,
                DateTimeKind.Utc);

            await _context.SaveChangesAsync();

            TempData["Success"] =
                "Task updated successfully by admin";

            return RedirectToAction(
                "UserTasks",
                new { id = existingTask.UserId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteTask(int id)
        {
            var task = await _context.Tasks
                .FirstOrDefaultAsync(t => t.Id == id);

            if (task == null)
            {
                return NotFound();
            }

            var userId = task.UserId;

            _context.Tasks.Remove(task);

            await _context.SaveChangesAsync();

            TempData["Success"] =
                "Task deleted successfully by admin";

            return RedirectToAction(
                "UserTasks",
                new { id = userId });
        }
    }
}