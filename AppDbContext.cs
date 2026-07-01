using ActionPlanApi.Models;
using Microsoft.EntityFrameworkCore;

namespace ActionPlanApi.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Role> Roles => Set<Role>();
    public DbSet<User> Users => Set<User>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<AppSetting> AppSettings => Set<AppSetting>();
    public DbSet<Year> Years => Set<Year>();
    public DbSet<Period> Periods => Set<Period>();
    public DbSet<Unit> Units => Set<Unit>();
    public DbSet<ApprovalStep> ApprovalSteps => Set<ApprovalStep>();
    public DbSet<Activity> Activities => Set<Activity>();
    public DbSet<ActivityCollaborator> ActivityCollaborators => Set<ActivityCollaborator>();
    public DbSet<Target> Targets => Set<Target>();
    public DbSet<TargetPeriod> TargetPeriods => Set<TargetPeriod>();
    public DbSet<ProgressEntry> ProgressEntries => Set<ProgressEntry>();
    public DbSet<ProgressHistory> ProgressHistory => Set<ProgressHistory>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Role>().HasIndex(x => x.Code).IsUnique();
        b.Entity<User>().HasIndex(x => x.Username).IsUnique();
        b.Entity<AppSetting>().HasIndex(x => x.Key).IsUnique();

        b.Entity<Activity>().HasIndex(x => x.ActivityNumber).IsUnique()
            .HasFilter("[ActivityNumber] IS NOT NULL");

        b.Entity<ProgressEntry>()
            .HasIndex(x => new { x.ActivityId, x.PeriodId }).IsUnique();

        b.Entity<TargetPeriod>()
            .HasIndex(x => new { x.TargetId, x.PeriodId }).IsUnique();

        // روابط با حذف آبشاری
        b.Entity<UserRole>()
            .HasOne(x => x.User).WithMany(u => u.UserRoles)
            .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);

        b.Entity<ActivityCollaborator>()
            .HasOne(x => x.Activity).WithMany(a => a.Collaborators)
            .HasForeignKey(x => x.ActivityId).OnDelete(DeleteBehavior.Cascade);

        b.Entity<TargetPeriod>()
            .HasOne(x => x.Target).WithMany(t => t.Periods)
            .HasForeignKey(x => x.TargetId).OnDelete(DeleteBehavior.Cascade);
    }
}
