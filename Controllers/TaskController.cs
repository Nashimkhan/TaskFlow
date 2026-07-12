using Microsoft.AspNetCore.Mvc;
using TaskFlow.Interfaces;
using TaskFlow.Models;

namespace TaskFlow.Controllers
{
    public class TaskController : Controller
    {
        private readonly ITaskRepository _taskRepo;

        public TaskController(ITaskRepository taskRepo)
        {
            _taskRepo = taskRepo;
        }

        
        private bool IsLoggedIn()
        {
            return HttpContext.Session.GetInt32("UserId") != null;
        }

       
        private DateTime ToUtc(DateTime date)
        {
            return DateTime.SpecifyKind(date, DateTimeKind.Utc);
        }

       
        public IActionResult Index(string? search, string? status, string? priority)
        {
            if (!IsLoggedIn())
                return RedirectToAction("Index", "Home");

            var userId = HttpContext.Session.GetInt32("UserId")!.Value;

            var tasks = _taskRepo.GetAll(userId);

            
            if (!string.IsNullOrWhiteSpace(search))
            {
                var lower = search.ToLower();

                tasks = tasks
                    .Where(t =>
                        (t.Title != null && t.Title.ToLower().Contains(lower)) ||
                        (t.Description != null && t.Description.ToLower().Contains(lower))
                    )
                    .ToList();
            }

           
            if (!string.IsNullOrWhiteSpace(status))
            {
                tasks = tasks
                    .Where(t => t.Status == status)
                    .ToList();
            }

           
            if (!string.IsNullOrWhiteSpace(priority))
            {
                tasks = tasks
                    .Where(t => t.Priority == priority)
                    .ToList();
            }

           
            ViewBag.TotalTasks = tasks.Count;
            ViewBag.CompletedTasks = tasks.Count(t => t.Status == "Done");
            ViewBag.PendingTasks = tasks.Count(t => t.Status != "Done");

            return View(tasks);
        }

       
        public IActionResult Create()
        {
            if (!IsLoggedIn())
                return RedirectToAction("Index", "Home");

            return View();
        }

       
        [HttpPost]
        public IActionResult Create(TaskItem task)
        {
            if (!IsLoggedIn())
                return RedirectToAction("Index", "Home");

            if (!ModelState.IsValid)
                return View(task);

            var userId = HttpContext.Session.GetInt32("UserId")!.Value;

            
            task.CreatedAt = ToUtc(DateTime.UtcNow);
            task.DueDate = ToUtc(task.DueDate);

            task.UserId = userId;

            _taskRepo.Add(task);

            
            TempData["Success"] = "Task created successfully!";

            return RedirectToAction("Index");
        }

        
        public IActionResult Edit(int id)
        {
            if (!IsLoggedIn())
                return RedirectToAction("Index", "Home");

            var userId = HttpContext.Session.GetInt32("UserId")!.Value;

            var task = _taskRepo.GetById(id, userId);

            if (task == null)
                return NotFound();

            return View(task);
        }

        
        [HttpPost]
        public IActionResult Edit(TaskItem task)
        {
            if (!IsLoggedIn())
                return RedirectToAction("Index", "Home");

            if (!ModelState.IsValid)
                return View(task);

            var userId = HttpContext.Session.GetInt32("UserId")!.Value;

            var existingTask = _taskRepo.GetById(task.Id, userId);

            if (existingTask == null)
                return NotFound();

            
            existingTask.Title = task.Title;
            existingTask.Description = task.Description;
            existingTask.Status = task.Status;
            existingTask.Priority = task.Priority;
            existingTask.DueDate = ToUtc(task.DueDate);

            _taskRepo.Update(existingTask);

           
            TempData["Success"] = "Task updated successfully!";

            return RedirectToAction("Index");
        }

        
        [HttpPost]
        public IActionResult Delete(int id)
        {
            if (!IsLoggedIn())
                return RedirectToAction("Index", "Home");

            var userId = HttpContext.Session.GetInt32("UserId")!.Value;

            var task = _taskRepo.GetById(id, userId);

            if (task == null)
                return NotFound();

            _taskRepo.Delete(task);

           
            TempData["Success"] = "Task deleted successfully!";

            return RedirectToAction("Index");
        }
    }
}