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

        public DbSet<Project> Projects { get; set; }

        public DbSet<ProjectMember> ProjectMembers { get; set; }

        public DbSet<PendingRegistration>
            PendingRegistrations
        { get; set; }

        protected override void OnModelCreating(
            ModelBuilder builder)
        {
            base.OnModelCreating(builder);
            builder.Entity<Project>()
    .HasMany(p => p.Members)
    .WithOne(pm => pm.Project)
    .HasForeignKey(pm => pm.ProjectId)
    .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<Project>()
                .HasMany(p => p.Tasks)
                .WithOne(t => t.Project)
                .HasForeignKey(t => t.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<ProjectMember>()
                .HasIndex(pm => new
                {
                    pm.ProjectId,
                    pm.UserId
                })
                .IsUnique();

            builder.Entity<PendingRegistration>()
                .HasIndex(p => p.Email)
                .IsUnique();

            builder.Entity<PendingRegistration>()
                .HasIndex(p => p.Username)
                .IsUnique();
        }
    }
}