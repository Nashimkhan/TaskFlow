using Microsoft.AspNetCore.Authorization;
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
            string? password,
            string? confirmPassword)
        {
            username = username?.Trim();
            email = email?.Trim();

            if (string.IsNullOrWhiteSpace(username) ||
                string.IsNullOrWhiteSpace(email) ||
                string.IsNullOrWhiteSpace(password) ||
                string.IsNullOrWhiteSpace(confirmPassword))
            {
                return RegisterError(
                    "All fields are required",
                    username,
                    email);
            }

            if (username.Length < 3)
            {
                return RegisterError(
                    "Username must be at least 3 characters",
                    username,
                    email);
            }

            if (username.Length > 20)
            {
                return RegisterError(
                    "Username cannot exceed 20 characters",
                    username,
                    email);
            }

            if (!username.All(char.IsLetterOrDigit))
            {
                return RegisterError(
                    "Username can only contain letters and numbers",
                    username,
                    email);
            }

            if (!IsValidEmail(email))
            {
                return RegisterError(
                    "Please enter a valid email address",
                    username,
                    email);
            }

            if (password.Length < 6)
            {
                return RegisterError(
                    "Password must be at least 6 characters",
                    username,
                    email);
            }

            if (password != confirmPassword)
            {
                return RegisterError(
                    "Password and Confirm Password do not match",
                    username,
                    email);
            }

            var existingUsername =
                await _userManager.FindByNameAsync(username);

            if (existingUsername != null)
            {
                return RegisterError(
                    "Username already exists",
                    username,
                    email);
            }

            var existingEmail =
                await _userManager.FindByEmailAsync(email);

            if (existingEmail != null)
            {
                return RegisterError(
                    "Email is already registered",
                    username,
                    email);
            }

            var user = new IdentityUser
            {
                UserName = username,
                Email = email,
                EmailConfirmed = false
            };

            var result =
                await _userManager.CreateAsync(
                    user,
                    password);

            if (!result.Succeeded)
            {
                var errors = string.Join(
                    ", ",
                    result.Errors.Select(
                        e => e.Description));

                return RegisterError(
                    errors,
                    username,
                    email);
            }

            var roleResult =
                await _userManager.AddToRoleAsync(
                    user,
                    "User");

            if (!roleResult.Succeeded)
            {
                await _userManager.DeleteAsync(user);

                return RegisterError(
                    "Account could not be created. Please try again.",
                    username,
                    email);
            }

            try
            {
                var token =
                    await _userManager
                        .GenerateEmailConfirmationTokenAsync(user);

                var verificationLink =
                    CreateApplicationLink(
                        "ConfirmEmail",
                        new
                        {
                            userId = user.Id,
                            token
                        });

                await _emailService
                    .SendVerificationEmailAsync(
                        email,
                        verificationLink);
            }
            catch
            {
                await _userManager.DeleteAsync(user);

                return RegisterError(
                    "Verification email could not be sent. Please try again.",
                    username,
                    email);
            }

            TempData["Success"] =
                "Account created! Please check your email and verify your email address.";

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
                    "Your email is already verified. Please login.";

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
                    "Please verify your email address before logging in";

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

        [HttpGet]
        public IActionResult ForgotPassword()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForgotPassword(
            string? email)
        {
            email = email?.Trim();

            if (string.IsNullOrWhiteSpace(email) ||
                !IsValidEmail(email))
            {
                ViewBag.Error =
                    "Please enter a valid email address";

                return View();
            }

            var user =
                await _userManager.FindByEmailAsync(email);

            if (user == null)
            {
                TempData["Success"] =
    "Check your inbox! We've sent password reset instructions to your email.";

                return RedirectToAction("Index");
            }

            try
            {
                var token =
                    await _userManager
                        .GeneratePasswordResetTokenAsync(user);

                var resetLink =
                    CreateApplicationLink(
                        "ResetPassword",
                        new
                        {
                            userId = user.Id,
                            token
                        });

                await _emailService
                  .SendPasswordResetEmailAsync(
                   email,
                  user.UserName ?? "TaskFlow User",
                  resetLink);
            }
            catch
            {
                ViewBag.Error =
                    "Password reset email could not be sent. Please try again.";

                return View();
            }

            TempData["Success"] =
                "Check your inbox! We've sent password reset instructions to your email.";

            return RedirectToAction("Index");
        }

        [HttpGet]
        public async Task<IActionResult> ResetPassword(
            string? userId,
            string? token)
        {
            if (string.IsNullOrWhiteSpace(userId) ||
                string.IsNullOrWhiteSpace(token))
            {
                TempData["Error"] =
                    "Invalid password reset link";

                return RedirectToAction("Index");
            }

            var user =
                await _userManager.FindByIdAsync(userId);

            if (user == null)
            {
                TempData["Error"] =
                    "Invalid password reset link";

                return RedirectToAction("Index");
            }

            ViewBag.UserId = userId;
            ViewBag.Token = token;

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(
            string? userId,
            string? token,
            string? password,
            string? confirmPassword)
        {
            ViewBag.UserId = userId;
            ViewBag.Token = token;

            if (string.IsNullOrWhiteSpace(userId) ||
                string.IsNullOrWhiteSpace(token))
            {
                TempData["Error"] =
                    "Invalid password reset link";

                return RedirectToAction("Index");
            }

            if (string.IsNullOrWhiteSpace(password) ||
                string.IsNullOrWhiteSpace(confirmPassword))
            {
                ViewBag.Error =
                    "All fields are required";

                return View();
            }

            if (password.Length < 6)
            {
                ViewBag.Error =
                    "Password must be at least 6 characters";

                return View();
            }

            if (password != confirmPassword)
            {
                ViewBag.Error =
                    "Password and Confirm Password do not match";

                return View();
            }

            var user =
                await _userManager.FindByIdAsync(userId);

            if (user == null)
            {
                TempData["Error"] =
                    "Invalid password reset link";

                return RedirectToAction("Index");
            }

            var result =
                await _userManager.ResetPasswordAsync(
                    user,
                    token,
                    password);

            if (!result.Succeeded)
            {
                ViewBag.Error = string.Join(
                    ", ",
                    result.Errors.Select(
                        e => e.Description));

                return View();
            }

            TempData["Success"] =
                "Password reset successfully! You can now login.";

            return RedirectToAction("Index");
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult CheckAuth()
        {
            return Json(new
            {
                IsAuthenticated = User.Identity?.IsAuthenticated,
                Username = User.Identity?.Name,
                IsAdmin = User.IsInRole("Admin"),
                Claims = User.Claims.Select(c => new
                {
                    c.Type,
                    c.Value
                })
            });
        }

        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();

            TempData["Success"] =
                "Logged out successfully";

            return RedirectToAction("Index");
        }

        private string CreateApplicationLink(
            string action,
            object routeValues)
        {
            var baseUrl =
                _configuration["App:BaseUrl"];

            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                baseUrl =
                    $"{Request.Scheme}://{Request.Host}";
            }

            var relativeLink = Url.Action(
                action,
                "Home",
                routeValues);

            if (string.IsNullOrWhiteSpace(relativeLink))
            {
                throw new InvalidOperationException(
                    "Application link could not be created.");
            }

            return
                $"{baseUrl.TrimEnd('/')}{relativeLink}";
        }

        private IActionResult RegisterError(
            string message,
            string? username,
            string? email)
        {
            ViewBag.Error = message;
            ViewBag.ShowRegister = true;
            ViewBag.RegisterUsername = username;
            ViewBag.RegisterEmail = email;

            return View("Index");
        }

        private static bool IsValidEmail(
            string email)
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

                var domain = address.Host;

                if (string.IsNullOrWhiteSpace(domain) ||
                    !domain.Contains('.') ||
                    domain.StartsWith('.') ||
                    domain.EndsWith('.'))
                {
                    return false;
                }

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