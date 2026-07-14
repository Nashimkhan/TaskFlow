namespace TaskFlow.Models
{
    public class ProjectMember
    {
        public int Id { get; set; }

        public int ProjectId { get; set; }

        public Project Project { get; set; } = null!;

        public string UserId { get; set; } = string.Empty;

        public string Status { get; set; } = "Pending";

        public DateTime JoinedAt { get; set; }
            = DateTime.UtcNow;
    }
}