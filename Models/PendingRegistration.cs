using System.ComponentModel.DataAnnotations;

namespace TaskFlow.Models
{
    public class PendingRegistration
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(20)]
        public string Username { get; set; } = string.Empty;

        [Required]
        [MaxLength(256)]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string PasswordHash { get; set; } = string.Empty;

        [Required]
        public string VerificationTokenHash { get; set; } = string.Empty;

        public DateTime TokenExpiresAt { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}