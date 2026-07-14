using Microsoft.AspNetCore.Identity;

namespace TaskFlow.Models
{
    public class TaskItem
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime DueDate { get; set; }
        public string Status { get; set; } = "Pending";
        public string Priority { get; set; } = "Medium";
        public string UserId { get; set; } = string.Empty;
        public int? ProjectId { get; set; }
        public Project? Project { get; set; }
        public IdentityUser? User { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
