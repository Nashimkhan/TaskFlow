using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaskFlow.Data;
using TaskFlow.Models;

namespace TaskFlow.Controllers
{
    [Authorize]
    public class ProjectController : Controller
    {
        private readonly AppDbContext _context;
        private readonly UserManager<IdentityUser> _userManager;

        public ProjectController(
            AppDbContext context,
            UserManager<IdentityUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        private string GetUserId()
        {
            return _userManager.GetUserId(User)
                ?? throw new UnauthorizedAccessException();
        }

        // PROJECT LIST

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var userId = GetUserId();

            var projects = await _context.Projects
                .Where(p =>
                    p.OwnerId == userId ||
                    p.Members.Any(m =>
                        m.UserId == userId &&
                        m.Status == "Accepted"))
                .Include(p => p.Members)
                .Include(p => p.Tasks)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();

            return View(projects);
        }

        // CREATE PROJECT

        [HttpGet]
        public IActionResult Create()
        {
            return View(new Project
            {
                Deadline = DateTime.Today.AddDays(7)
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Project project)
        {
            if (!ModelState.IsValid)
            {
                return View(project);
            }

            var userId = GetUserId();

            project.OwnerId = userId;
            project.CreatedAt = DateTime.UtcNow;

            if (project.Deadline.Kind == DateTimeKind.Unspecified)
            {
                project.Deadline = DateTime.SpecifyKind(
                    project.Deadline,
                    DateTimeKind.Utc);
            }

            _context.Projects.Add(project);

            await _context.SaveChangesAsync();

            TempData["Success"] =
                "Project created successfully.";

            return RedirectToAction(nameof(Index));
        }

        // PROJECT DETAILS

        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var userId = GetUserId();

            var project = await _context.Projects
                .Include(p => p.Members)
                .Include(p => p.Tasks)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (project == null)
            {
                return NotFound();
            }

            var hasAccess =
                project.OwnerId == userId ||
                project.Members.Any(m =>
                    m.UserId == userId &&
                    m.Status == "Accepted");

            if (!hasAccess)
            {
                return Forbid();
            }

            var userIds = project.Members
                .Select(m => m.UserId)
                .Append(project.OwnerId)
                .Distinct()
                .ToList();

            var users = await _userManager.Users
                .Where(u => userIds.Contains(u.Id))
                .ToListAsync();

            ViewBag.Users = users.ToDictionary(
                u => u.Id,
                u => u.UserName ?? u.Email ?? "Unknown User");

            ViewBag.IsOwner =
                project.OwnerId == userId;

            return View(project);
        }

        // INVITE USER

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Invite(
            int projectId,
            string userSearch)
        {
            var userId = GetUserId();

            var project = await _context.Projects
                .Include(p => p.Members)
                .FirstOrDefaultAsync(p => p.Id == projectId);

            if (project == null)
            {
                return NotFound();
            }

            if (project.OwnerId != userId)
            {
                return Forbid();
            }

            if (string.IsNullOrWhiteSpace(userSearch))
            {
                TempData["Error"] =
                    "Enter a username or email.";

                return RedirectToAction(
                    nameof(Details),
                    new { id = projectId });
            }

            var search = userSearch.Trim();

            var invitedUser = await _userManager.Users
                .FirstOrDefaultAsync(u =>
                    u.UserName == search ||
                    u.Email == search);

            if (invitedUser == null)
            {
                TempData["Error"] =
                    "TaskFlow user not found.";

                return RedirectToAction(
                    nameof(Details),
                    new { id = projectId });
            }

            var invitedUserIsAdmin =
                await _userManager.IsInRoleAsync(
                invitedUser,
                  "Admin");

            if (invitedUserIsAdmin)
            {
                TempData["Error"] =
                    "Admin accounts cannot be added to projects.";

                return RedirectToAction(
                    nameof(Details),
                    new { id = projectId });
            }

            if (invitedUser.Id == userId)
            {
                TempData["Error"] =
                    "You are already the project owner.";

                return RedirectToAction(
                    nameof(Details),
                    new { id = projectId });
            }

            var existingMember = project.Members
                .FirstOrDefault(m =>
                    m.UserId == invitedUser.Id);

            if (existingMember != null)
            {
                TempData["Error"] =
                    existingMember.Status == "Accepted"
                        ? "This user is already a project member."
                        : "This user has already been invited.";

                return RedirectToAction(
                    nameof(Details),
                    new { id = projectId });
            }

            var projectMember = new ProjectMember
            {
                ProjectId = project.Id,
                UserId = invitedUser.Id,
                Status = "Pending",
                JoinedAt = DateTime.UtcNow
            };

            _context.ProjectMembers.Add(projectMember);

            await _context.SaveChangesAsync();

            TempData["Success"] =
                $"{invitedUser.UserName} has been invited.";

            return RedirectToAction(
                nameof(Details),
                new { id = projectId });
        }

        // INVITATION INBOX

        [HttpGet]
        public async Task<IActionResult> Invitations()
        {
            if (User.IsInRole("Admin"))
            {
                return Forbid();
            }

            var userId = GetUserId();

            var invitations = await _context.ProjectMembers
                .Include(m => m.Project)
                .Where(m =>
                    m.UserId == userId &&
                    m.Status == "Pending")
                .OrderByDescending(m => m.JoinedAt)
                .ToListAsync();

            return View(invitations);
        }


        // ACCEPT INVITATION

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AcceptInvitation(int id)
        {
            if (User.IsInRole("Admin"))
            {
                return Forbid();
            }

            var userId = GetUserId();

            var invitation = await _context.ProjectMembers
                .FirstOrDefaultAsync(m =>
                    m.Id == id &&
                    m.UserId == userId &&
                    m.Status == "Pending");

            if (invitation == null)
            {
                return NotFound();
            }

            invitation.Status = "Accepted";
            invitation.JoinedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            TempData["Success"] =
                "Project invitation accepted. Welcome to the team!";

            return RedirectToAction(
                nameof(Details),
                new { id = invitation.ProjectId });
        }


        // REJECT INVITATION

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectInvitation(int id)
        {
            if (User.IsInRole("Admin"))
            {
                return Forbid();
            }

            var userId = GetUserId();

            var invitation = await _context.ProjectMembers
                .FirstOrDefaultAsync(m =>
                    m.Id == id &&
                    m.UserId == userId &&
                    m.Status == "Pending");

            if (invitation == null)
            {
                return NotFound();
            }

            _context.ProjectMembers.Remove(invitation);

            await _context.SaveChangesAsync();

            TempData["Success"] =
                "Project invitation declined.";

            return RedirectToAction(nameof(Invitations));
        }

        // REMOVE PROJECT MEMBER

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveMember(
            int projectId,
            int memberId)
        {
            var userId = GetUserId();

            var project = await _context.Projects
                .FirstOrDefaultAsync(p => p.Id == projectId);

            if (project == null)
            {
                return NotFound();
            }

            // Only project owner can remove members
            if (project.OwnerId != userId)
            {
                return Forbid();
            }

            var member = await _context.ProjectMembers
                .FirstOrDefaultAsync(m =>
                    m.Id == memberId &&
                    m.ProjectId == projectId);

            if (member == null)
            {
                return NotFound();
            }

            // Unassign tasks before removing the member
            var assignedTasks = await _context.Tasks
                .Where(t =>
                    t.ProjectId == projectId &&
                    t.AssignedUserId == member.UserId)
                .ToListAsync();

            foreach (var task in assignedTasks)
            {
                task.AssignedUserId = null;
            }

            _context.ProjectMembers.Remove(member);

            await _context.SaveChangesAsync();

            TempData["Success"] =
                "Team member removed from the project.";

            return RedirectToAction(
                nameof(Details),
                new { id = projectId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelInvitation(
    int projectId,
    int memberId)
        {
            var userId = GetUserId();

            var project = await _context.Projects
                .FirstOrDefaultAsync(p => p.Id == projectId);

            if (project == null)
                return NotFound();

            if (project.OwnerId != userId)
                return Forbid();

            var invitation = await _context.ProjectMembers
                .FirstOrDefaultAsync(m =>
                    m.Id == memberId &&
                    m.ProjectId == projectId &&
                    m.Status == "Pending");

            if (invitation == null)
                return NotFound();

            _context.ProjectMembers.Remove(invitation);

            await _context.SaveChangesAsync();

            TempData["Success"] =
                "Invitation cancelled.";

            return RedirectToAction(
                nameof(Details),
                new { id = projectId });
        }

        [HttpGet]
        public async Task<IActionResult> EditTask(int id)
        {
            var userId = GetUserId();

            var task = await _context.Tasks
                .FirstOrDefaultAsync(t => t.Id == id);

            if (task == null)
                return NotFound();

            var project = await _context.Projects
                .Include(p => p.Members)
                .FirstOrDefaultAsync(p => p.Id == task.ProjectId);

            if (project == null)
                return NotFound();

            if (project.OwnerId != userId)
                return Forbid();

            ViewBag.ProjectMembers =
                await GetAssignableProjectUsers(project);

            return View(task);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditTask(TaskItem model)
        {
            var userId = GetUserId();

            var task = await _context.Tasks
                .FirstOrDefaultAsync(t => t.Id == model.Id);

            if (task == null)
                return NotFound();

            var project = await _context.Projects
                .Include(p => p.Members)
                .FirstOrDefaultAsync(p => p.Id == task.ProjectId);

            if (project == null)
                return NotFound();

            if (project.OwnerId != userId)
                return Forbid();

            var assignableUserIds = GetAssignableUserIds(project);

            if (!string.IsNullOrWhiteSpace(model.AssignedUserId) &&
                !assignableUserIds.Contains(model.AssignedUserId))
            {
                ModelState.AddModelError(
                    nameof(model.AssignedUserId),
                    "Choose the project owner or an accepted project member.");
            }

            if (!ModelState.IsValid)
            {
                ViewBag.ProjectMembers =
                    await GetAssignableProjectUsers(project);

                return View(model);
            }

            task.Title = model.Title;
            task.Description = model.Description;
            task.Status = model.Status;
            task.Priority = model.Priority;
            task.DueDate = ToUtc(model.DueDate);
            task.AssignedUserId =
                string.IsNullOrWhiteSpace(model.AssignedUserId)
                    ? null
                    : model.AssignedUserId;

            await _context.SaveChangesAsync();

            TempData["Success"] = "Project task updated.";

            return RedirectToAction(nameof(Details),
                new { id = task.ProjectId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteTask(int id)
        {
            var userId = GetUserId();

            var task = await _context.Tasks
                .FirstOrDefaultAsync(t => t.Id == id);

            if (task == null)
                return NotFound();

            var project = await _context.Projects
                .FirstOrDefaultAsync(p => p.Id == task.ProjectId);

            if (project == null)
                return NotFound();

            if (project.OwnerId != userId)
                return Forbid();

            var projectId = task.ProjectId;

            _context.Tasks.Remove(task);

            await _context.SaveChangesAsync();

            TempData["Success"] =
                "Task deleted successfully.";

            return RedirectToAction(
                nameof(Details),
                new { id = projectId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CompleteTask(int id)
        {
            var currentUserId = GetUserId();

            var task = await _context.Tasks
                .Include(t => t.Project)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (task == null)
                return NotFound();

            if (string.IsNullOrWhiteSpace(task.AssignedUserId))
            {
                TempData["Error"] = "This task has not been assigned yet.";

                return RedirectToAction(nameof(Details),
                    new { id = task.ProjectId });
            }

            if (task.AssignedUserId != currentUserId)
            {
                TempData["Error"] = "This task is assigned to another member.";

                return RedirectToAction(nameof(Details),
                    new { id = task.ProjectId });
            }

            task.Status = "Done";

            await _context.SaveChangesAsync();

            TempData["Success"] = "Task marked as completed.";

            return RedirectToAction(nameof(Details),
                new { id = task.ProjectId });
        }

        private async Task<List<IdentityUser>> GetAssignableProjectUsers(
            Project project)
        {
            var userIds = GetAssignableUserIds(project);

            return await _userManager.Users
                .Where(x => userIds.Contains(x.Id))
                .OrderBy(x => x.UserName)
                .ToListAsync();
        }

        private static List<string> GetAssignableUserIds(Project project)
        {
            return project.Members
                .Where(x => x.Status == "Accepted")
                .Select(x => x.UserId)
                .Append(project.OwnerId)
                .Distinct()
                .ToList();
        }

        private static DateTime ToUtc(DateTime date)
        {
            return DateTime.SpecifyKind(
                date,
                DateTimeKind.Utc);
        }
    }
}
