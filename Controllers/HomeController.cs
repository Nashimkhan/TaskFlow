using System.Security.Cryptography;
using Microsoft.AspNetCore.WebUtilities;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaskFlow.Data;
using TaskFlow.Models;
using TaskFlow.Services.Implementations;

namespace TaskFlow.Controllers
{
    public class HomeController : Controller
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly SignInManager<IdentityUser> _signInManager;
        private readonly EmailService _emailService;
        private readonly IConfiguration _configuration;
        private readonly AppDbContext _context;
        private readonly IPasswordHasher<IdentityUser> _passwordHasher;

        public HomeController(
            UserManager<IdentityUser> userManager,
            SignInManager<IdentityUser> signInManager,
            EmailService emailService,
            IConfiguration configuration,
            AppDbContext context,
            IPasswordHasher<IdentityUser> passwordHasher)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _emailService = emailService;
            _configuration = configuration;
            _context = context;
            _passwordHasher = passwordHasher;
        }

        public IActionResult Index(string? section)
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                return RedirectToAction("Index", "Task");
            }

            ViewBag.Section = section;

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
            email = email?.Trim().ToLowerInvariant();

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

            if (username.Length < 3 ||
                username.Length > 20)
            {
                return RegisterError(
                    "Username must be between 3 and 20 characters",
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
                    "Invalid email format. Please enter a valid email address.",
                    username,
                    email);
            }

            if (!email.EndsWith(
                    "@gmail.com",
                    StringComparison.OrdinalIgnoreCase))
            {
                return RegisterError(
                    "Only Gmail addresses are allowed. Please use an @gmail.com email.",
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

            var oldPending =
                await _context.PendingRegistrations
                    .FirstOrDefaultAsync(p =>
                        p.Email == email ||
                        p.Username == username);

            if (oldPending != null)
            {
                _context.PendingRegistrations.Remove(oldPending);

                await _context.SaveChangesAsync();
            }

            var token =
                Convert.ToHexString(
                    RandomNumberGenerator.GetBytes(32));

            var temporaryUser = new IdentityUser
            {
                UserName = username,
                Email = email
            };

            var passwordHash =
                _passwordHasher.HashPassword(
                    temporaryUser,
                    password);

            var pendingRegistration =
                new PendingRegistration
                {
                    Username = username,
                    Email = email,
                    PasswordHash = passwordHash,
                    VerificationTokenHash = HashToken(token),
                    CreatedAt = DateTime.UtcNow,
                    TokenExpiresAt =
                        DateTime.UtcNow.AddMinutes(30)
                };

            _context.PendingRegistrations.Add(
                pendingRegistration);

            await _context.SaveChangesAsync();

            try
            {
                var verificationLink =
                    CreateApplicationLink(
                        "ConfirmEmail",
                        new
                        {
                            id = pendingRegistration.Id,
                            token
                        });

                await _emailService
                    .SendVerificationEmailAsync(
                        email,
                        verificationLink);
            }
            catch
            {
                _context.PendingRegistrations.Remove(
                    pendingRegistration);

                await _context.SaveChangesAsync();

                return RegisterError(
                    "Verification email could not be sent. Please try again.",
                    username,
                    email);
            }

            return RedirectToAction(
                "VerifyEmailNotice",
                new
                {
                    email,
                    username
                });
        }

        [HttpGet]
        public IActionResult VerifyEmailNotice(
            string? email,
            string? username)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                return RedirectToAction("Index");
            }

            ViewBag.Email = email;
            ViewBag.Username = username;

            return View();
        }

        [HttpGet]
        public async Task<IActionResult> CheckEmailVerification(
    string? email)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                return Json(new
                {
                    verified = false
                });
            }

            email = email.Trim().ToLowerInvariant();

            var user =
                await _userManager.FindByEmailAsync(email);

            if (user != null &&
                user.EmailConfirmed)
            {
                return Json(new
                {
                    verified = true
                });
            }

            return Json(new
            {
                verified = false
            });
        }

        [HttpGet]
        [HttpGet]
        public async Task<IActionResult> ConfirmEmail(
    int? id,
    string? token)
        {
            if (!id.HasValue ||
                string.IsNullOrWhiteSpace(token))
            {
                TempData["Error"] =
                    "Invalid verification link.";

                return RedirectToAction(
                    "Index",
                    new { section = "login" });
            }

            var pendingRegistration =
                await _context.PendingRegistrations
                    .FirstOrDefaultAsync(p => p.Id == id.Value);

            if (pendingRegistration == null)
            {
                TempData["Error"] =
                    "Verification link is invalid or has already been used.";

                return RedirectToAction(
                    "Index",
                    new { section = "login" });
            }

            if (pendingRegistration.TokenExpiresAt <
                DateTime.UtcNow)
            {
                _context.PendingRegistrations.Remove(
                    pendingRegistration);

                await _context.SaveChangesAsync();

                TempData["Error"] =
                    "Verification link has expired. Please register again.";

                return RedirectToAction(
                    "Index",
                    new { section = "register" });
            }

            var tokenHash = HashToken(token);

            if (!string.Equals(
                    pendingRegistration.VerificationTokenHash,
                    tokenHash,
                    StringComparison.Ordinal))
            {
                TempData["Error"] =
                    "Invalid verification link.";

                return RedirectToAction(
                    "Index",
                    new { section = "login" });
            }

            var existingUsername =
                await _userManager.FindByNameAsync(
                    pendingRegistration.Username);

            var existingEmail =
                await _userManager.FindByEmailAsync(
                    pendingRegistration.Email);

            if (existingUsername != null ||
                existingEmail != null)
            {
                _context.PendingRegistrations.Remove(
                    pendingRegistration);

                await _context.SaveChangesAsync();

                TempData["Error"] =
                    "This account is already registered.";

                return RedirectToAction(
                    "Index",
                    new { section = "login" });
            }

            var user = new IdentityUser
            {
                UserName = pendingRegistration.Username,
                Email = pendingRegistration.Email,
                EmailConfirmed = true,
                PasswordHash = pendingRegistration.PasswordHash
            };

            var createResult =
                await _userManager.CreateAsync(user);

            if (!createResult.Succeeded)
            {
                TempData["Error"] =
                    string.Join(
                        ", ",
                        createResult.Errors.Select(
                            e => e.Description));

                return RedirectToAction(
                    "Index",
                    new { section = "login" });
            }

            _context.PendingRegistrations.Remove(
                pendingRegistration);

            await _context.SaveChangesAsync();

            TempData["Success"] =
                "Email verified successfully! Please login.";

            return RedirectToAction(
                "Index",
                new { section = "login" });
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
                    "Username and password are required.";

                ViewBag.Section = "login";

                return View("Index");
            }

            var user =
                await _userManager.FindByNameAsync(username);

            if (user == null)
            {
                ViewBag.Error =
                    "Invalid username or password.";

                ViewBag.Section = "login";

                return View("Index");
            }

            if (!user.EmailConfirmed)
            {
                ViewBag.Error =
                    "Please verify your email before logging in.";

                ViewBag.Section = "login";

                return View("Index");
            }

            var result =
                await _signInManager.PasswordSignInAsync(
                    user,
                    password,
                    isPersistent: false,
                    lockoutOnFailure: false);

            if (!result.Succeeded)
            {
                ViewBag.Error =
                    "Invalid username or password.";

                ViewBag.Section = "login";

                return View("Index");
            }

            if (await _userManager.IsInRoleAsync(
                user,
                "Admin"))
            {
                return RedirectToAction(
                    "Index",
                    "Admin");
            }

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
                    "Check your inbox! If an active account exists, reset instructions have been sent.";

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
                await _userManager.FindByIdAsync(userId!);

            if (user == null)
            {
                TempData["Error"] =
                    "Invalid password reset link";

                return RedirectToAction("Index");
            }

            var result =
                await _userManager.ResetPasswordAsync(
                    user,
                    token!,
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

            var relativeLink =
                Url.Action(
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

        private static string HashToken(string token)
        {
            var bytes =
                SHA256.HashData(
                    Encoding.UTF8.GetBytes(token));

            return Convert.ToHexString(bytes);
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