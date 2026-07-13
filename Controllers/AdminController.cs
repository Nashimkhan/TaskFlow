using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaskFlow.Data;

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
                .ToListAsync();

            ViewBag.Username = user.UserName;

            return View(tasks);
        }
    }
}