using System.ComponentModel.DataAnnotations;

namespace TaskFlow.Models
{
    public class Project
    {
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [StringLength(1000)]
        public string? Description { get; set; }

        [Required]
        public DateTime Deadline { get; set; }

        public string OwnerId { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }
            = DateTime.UtcNow;

        public List<ProjectMember> Members { get; set; }
            = new();

        public List<TaskItem> Tasks { get; set; }
            = new();
    }
}