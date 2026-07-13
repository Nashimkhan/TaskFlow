using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace TaskFlow.Controllers
{
    public class HomeController : Controller
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly SignInManager<IdentityUser> _signInManager;

        public HomeController(
            UserManager<IdentityUser> userManager,
            SignInManager<IdentityUser> signInManager)
        {
            _userManager = userManager;
            _signInManager = signInManager;
        }

        public IActionResult Index()
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                return RedirectToAction("Index", "Task");
            }

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(
            string? username,
            string? email,
            string? password,
            string? confirmPassword)
        {
            username = username?.Trim();
            email = email?.Trim();

            ViewBag.ShowRegister = true;
            ViewBag.RegisterUsername = username;
            ViewBag.RegisterEmail = email;

            if (string.IsNullOrWhiteSpace(username) ||
                string.IsNullOrWhiteSpace(email) ||
                string.IsNullOrWhiteSpace(password) ||
                string.IsNullOrWhiteSpace(confirmPassword))
            {
                ViewBag.Error = "All fields are required";
                return View("Index");
            }

            if (username.Length < 3 || username.Length > 20)
            {
                ViewBag.Error =
                    "Username must be between 3 and 20 characters";

                return View("Index");
            }

            if (!Regex.IsMatch(username, @"^[a-zA-Z0-9]+$"))
            {
                ViewBag.Error =
                    "Username can only contain letters and numbers";

                return View("Index");
            }

            if (!Regex.IsMatch(
                    email,
                    @"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$"))
            {
                ViewBag.Error =
                    "Please enter a valid email address";

                return View("Index");
            }

            if (password.Length < 6)
            {
                ViewBag.Error =
                    "Password must be at least 6 characters";

                return View("Index");
            }

            if (password != confirmPassword)
            {
                ViewBag.Error = "Passwords do not match";

                return View("Index");
            }

            var existingUser =
                await _userManager.FindByNameAsync(username);

            if (existingUser != null)
            {
                ViewBag.Error = "Username already exists";

                return View("Index");
            }

            var existingEmail =
                await _userManager.FindByEmailAsync(email);

            if (existingEmail != null)
            {
                ViewBag.Error = "Email is already registered";

                return View("Index");
            }

            var user = new IdentityUser
            {
                UserName = username,
                Email = email
            };

            var result =
                await _userManager.CreateAsync(user, password);

            if (!result.Succeeded)
            {
                ViewBag.Error = string.Join(
                    ", ",
                    result.Errors.Select(e => e.Description));

                return View("Index");
            }

            await _userManager.AddToRoleAsync(user, "User");

            TempData["Success"] =
                "Account created successfully! Please login.";

            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(
            string? username,
            string? password)
        {
            username = username?.Trim();

            if (string.IsNullOrWhiteSpace(username) ||
                string.IsNullOrWhiteSpace(password))
            {
                ViewBag.Error =
                    "Please enter username and password";

                return View("Index");
            }

            var result =
                await _signInManager.PasswordSignInAsync(
                    username,
                    password,
                    isPersistent: false,
                    lockoutOnFailure: false);

            if (!result.Succeeded)
            {
                ViewBag.Error =
                    "Invalid username or password";

                return View("Index");
            }

            TempData["Success"] =
                "Welcome back, " + username + "!";

            return RedirectToAction("Index", "Task");
        }

        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();

            TempData["Success"] =
                "Logged out successfully";

            return RedirectToAction("Index");
        }
    }
}