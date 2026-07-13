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
        public async Task<IActionResult> Register(
            string? username,
            string? password)
        {
            username = username?.Trim();
            password = password?.Trim();

            if (string.IsNullOrWhiteSpace(username) ||
                string.IsNullOrWhiteSpace(password))
            {
                ViewBag.Error = "All fields are required";
                return View("Index");
            }

            var existingUser = await _userManager.FindByNameAsync(username);

            if (existingUser != null)
            {
                ViewBag.Error = "Username already exists";
                return View("Index");
            }

            var user = new IdentityUser
            {
                UserName = username
            };

            var result = await _userManager.CreateAsync(user, password);

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
        public async Task<IActionResult> Login(
            string? username,
            string? password)
        {
            username = username?.Trim();
            password = password?.Trim();

            if (string.IsNullOrWhiteSpace(username) ||
                string.IsNullOrWhiteSpace(password))
            {
                ViewBag.Error = "Please enter username and password";
                return View("Index");
            }

            var result = await _signInManager.PasswordSignInAsync(
                username,
                password,
                isPersistent: false,
                lockoutOnFailure: false);

            if (!result.Succeeded)
            {
                ViewBag.Error = "Invalid username or password";
                return View("Index");
            }

            TempData["Success"] = "Welcome back, " + username + "!";

            return RedirectToAction("Index", "Task");
        }

        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();

            TempData["Success"] = "Logged out successfully";

            return RedirectToAction("Index");
        }
    }
}