using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using TaskFlow.Services.Implementations;

namespace TaskFlow.Controllers
{
    public class HomeController : Controller
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly SignInManager<IdentityUser> _signInManager;
        private readonly EmailService _emailService;
        private readonly IConfiguration _configuration;

        public HomeController(
            UserManager<IdentityUser> userManager,
            SignInManager<IdentityUser> signInManager,
            EmailService emailService,
            IConfiguration configuration)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _emailService = emailService;
            _configuration = configuration;
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
            string? password)
        {
            username = username?.Trim();
            email = email?.Trim();
            password = password?.Trim();

            if (string.IsNullOrWhiteSpace(username) ||
                string.IsNullOrWhiteSpace(email) ||
                string.IsNullOrWhiteSpace(password))
            {
                return RegisterError("All fields are required");
            }

            if (username.Length < 3)
            {
                return RegisterError(
                    "Username must be at least 3 characters");
            }

            if (username.Length > 30)
            {
                return RegisterError(
                    "Username cannot exceed 30 characters");
            }

            if (!IsValidEmail(email))
            {
                return RegisterError(
                    "Please enter a valid email address");
            }

            if (password.Length < 6)
            {
                return RegisterError(
                    "Password must be at least 6 characters");
            }

            var existingUsername =
                await _userManager.FindByNameAsync(username);

            if (existingUsername != null)
            {
                return RegisterError(
                    "Username already exists");
            }

            var existingEmail =
                await _userManager.FindByEmailAsync(email);

            if (existingEmail != null)
            {
                return RegisterError(
                    "Email already registered");
            }

            var user = new IdentityUser
            {
                UserName = username,
                Email = email,
                EmailConfirmed = false
            };

            var result = await _userManager.CreateAsync(
                user,
                password);

            if (!result.Succeeded)
            {
                var errors = string.Join(
                    ", ",
                    result.Errors.Select(e => e.Description));

                return RegisterError(errors);
            }

            var roleResult =
                await _userManager.AddToRoleAsync(
                    user,
                    "User");

            if (!roleResult.Succeeded)
            {
                await _userManager.DeleteAsync(user);

                return RegisterError(
                    "Account could not be created. Please try again.");
            }

            try
            {
                var token =
                    await _userManager
                        .GenerateEmailConfirmationTokenAsync(user);

                var baseUrl =
                    _configuration["App:BaseUrl"];

                if (string.IsNullOrWhiteSpace(baseUrl))
                {
                    baseUrl =
                        $"{Request.Scheme}://{Request.Host}";
                }

                var relativeLink = Url.Action(
                    "ConfirmEmail",
                    "Home",
                    new
                    {
                        userId = user.Id,
                        token = token
                    });

                if (string.IsNullOrWhiteSpace(relativeLink))
                {
                    throw new InvalidOperationException(
                        "Verification link could not be created.");
                }

                var verificationLink =
                    $"{baseUrl.TrimEnd('/')}{relativeLink}";

                await _emailService
                    .SendVerificationEmailAsync(
                        email,
                        verificationLink);
            }
            catch
            {
                await _userManager.DeleteAsync(user);

                return RegisterError(
                    "Verification email could not be sent. Please try again.");
            }

            TempData["Success"] =
                "Account created! Check your email and verify your account.";

            return RedirectToAction("Index");
        }

        [HttpGet]
        public async Task<IActionResult> ConfirmEmail(
            string? userId,
            string? token)
        {
            if (string.IsNullOrWhiteSpace(userId) ||
                string.IsNullOrWhiteSpace(token))
            {
                TempData["Error"] =
                    "Invalid verification link";

                return RedirectToAction("Index");
            }

            var user =
                await _userManager.FindByIdAsync(userId);

            if (user == null)
            {
                TempData["Error"] =
                    "User account was not found";

                return RedirectToAction("Index");
            }

            if (user.EmailConfirmed)
            {
                TempData["Success"] =
                    "Email is already verified. Please login.";

                return RedirectToAction("Index");
            }

            var result =
                await _userManager.ConfirmEmailAsync(
                    user,
                    token);

            if (!result.Succeeded)
            {
                TempData["Error"] =
                    "Email verification failed or the link is invalid.";

                return RedirectToAction("Index");
            }

            TempData["Success"] =
                "Email verified successfully! You can now login.";

            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(
            string? username,
            string? password)
        {
            username = username?.Trim();
            password = password?.Trim();

            if (string.IsNullOrWhiteSpace(username) ||
                string.IsNullOrWhiteSpace(password))
            {
                ViewBag.Error =
                    "Please enter username and password";

                return View("Index");
            }

            var user =
                await _userManager.FindByNameAsync(username);

            if (user == null)
            {
                ViewBag.Error =
                    "Invalid username or password";

                return View("Index");
            }

            if (!user.EmailConfirmed)
            {
                ViewBag.Error =
                    "Please verify your email before logging in";

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

            return RedirectToAction(
                "Index",
                "Task");
        }

        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();

            TempData["Success"] =
                "Logged out successfully";

            return RedirectToAction("Index");
        }

        private IActionResult RegisterError(string message)
        {
            ViewBag.Error = message;
            ViewBag.ShowRegister = true;

            return View("Index");
        }

        private static bool IsValidEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                return false;
            }

            if (email.Count(c => c == '@') != 1)
            {
                return false;
            }

            try
            {
                var address =
                    new System.Net.Mail.MailAddress(email);

                return address.Address.Equals(
                    email,
                    StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }
    }
}