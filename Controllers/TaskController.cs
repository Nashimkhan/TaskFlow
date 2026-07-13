using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using TaskFlow.Models;
using TaskFlow.Services.Interfaces;

namespace TaskFlow.Controllers
{
    [Authorize]
    public class TaskController : Controller
    {
        private readonly ITaskService _taskService;
        private readonly UserManager<IdentityUser> _userManager;

        public TaskController(
            ITaskService taskService,
            UserManager<IdentityUser> userManager)
        {
            _taskService = taskService;
            _userManager = userManager;
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

        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(TaskItem task)
        {
            if (!ModelState.IsValid)
            {
                return View(task);
            }

            _taskService.CreateTask(
                task,
                GetUserId());

            TempData["Success"] =
                "Task created successfully!";

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
    }
}