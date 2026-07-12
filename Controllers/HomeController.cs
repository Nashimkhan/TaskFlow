using Microsoft.AspNetCore.Mvc;
using TaskFlow.Interfaces;
using TaskFlow.Models;

namespace TaskFlow.Controllers
{
    public class HomeController : Controller
    {
        private readonly IUserRepository _userRepo;

        public HomeController(IUserRepository userRepo)
        {
            _userRepo = userRepo;
        }

        
        public IActionResult Index()
        {
            return View();
        }

        
        private string Clean(string? input)
        {
            return input?.Trim() ?? string.Empty;
        }

        
        private bool IsInvalid(string username, string password)
        {
            return string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password);
        }

        
        [HttpPost]
        public IActionResult Register(string? username, string? password)
        {
            username = Clean(username);
            password = Clean(password);

           
            if (IsInvalid(username, password))
            {
                ViewBag.Error = "All fields are required";
                return View("Index");
            }

            
            if (_userRepo.GetByUsername(username) != null)
            {
                ViewBag.Error = "Username already exists";
                return View("Index");
            }

           
            var newUser = new User
            {
                Username = username,
                Password = BCrypt.Net.BCrypt.HashPassword(password),
                CreatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc)
            };

            _userRepo.Add(newUser);

            
            TempData["Success"] = "Account created successfully! Please login.";

            return RedirectToAction("Index");
        }

       
        [HttpPost]
        public IActionResult Login(string? username, string? password)
        {
            username = Clean(username);
            password = Clean(password);

            
            if (IsInvalid(username, password))
            {
                ViewBag.Error = "Please enter username and password";
                return View("Index");
            }

            var dbUser = _userRepo.GetByUsername(username);

            
            if (dbUser == null || !BCrypt.Net.BCrypt.Verify(password, dbUser.Password))
            {
                ViewBag.Error = "Invalid username or password";
                return View("Index");
            }

            
            HttpContext.Session.SetInt32("UserId", dbUser.Id);
            HttpContext.Session.SetString("Username", dbUser.Username);

            
            TempData["Success"] = "Welcome back, " + dbUser.Username + "!";

            return RedirectToAction("Index", "Task");
        }

        
        public IActionResult Logout()
        {
            HttpContext.Session.Clear();

            TempData["Success"] = "Logged out successfully";

            return RedirectToAction("Index");
        }
    }
}