using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using TaskFlow.Models;

namespace TaskFlow.Data
{
    public class AppDbContext
        : IdentityDbContext<IdentityUser>
    {
        public AppDbContext(
            DbContextOptions<AppDbContext> options)
            : base(options)
        {
        }

        public DbSet<TaskItem> Tasks { get; set; }

        public DbSet<PendingRegistration>
            PendingRegistrations
        { get; set; }

        protected override void OnModelCreating(
            ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<PendingRegistration>()
                .HasIndex(p => p.Email)
                .IsUnique();

            builder.Entity<PendingRegistration>()
                .HasIndex(p => p.Username)
                .IsUnique();
        }
    }
}