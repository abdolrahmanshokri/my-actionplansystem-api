namespace ActionPlanApi.Models;

public class Role
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public string Title { get; set; } = "";
}

public class User
{
    public int Id { get; set; }
    public string Username { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string FullName { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public List<UserRole> UserRoles { get; set; } = new();
}

public class UserRole
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User? User { get; set; }
    public int RoleId { get; set; }
    public Role? Role { get; set; }
}

public class AppSetting
{
    public int Id { get; set; }
    public string Key { get; set; } = "";
    public string Value { get; set; } = "";
}

public class Year
{
    public int Id { get; set; }
    public int YearValue { get; set; }
    public string Title { get; set; } = "";
    public int StartJy { get; set; }
    public int StartJm { get; set; }
    public int StartJd { get; set; }
    public int EndJy { get; set; }
    public int EndJm { get; set; }
    public int EndJd { get; set; }
    public bool IsClosed { get; set; }
}

public class Period
{
    public int Id { get; set; }
    public int YearId { get; set; }
    public Year? Year { get; set; }
    public int MonthNumber { get; set; }
    public string MonthName { get; set; } = "";
    public string Title { get; set; } = "";
    public bool IsOpen { get; set; } = true;
}

public class Unit
{
    public int Id { get; set; }
    public int? ParentId { get; set; }
    public string Name { get; set; } = "";
    public int SortOrder { get; set; }
}

public class ApprovalStep
{
    public int Id { get; set; }
    public int UnitId { get; set; }
    public int StepOrder { get; set; }
    public string ApprovalType { get; set; } = "or"; // or / and_sum / and_alone
    public int? RoleId { get; set; }
    public int? UserId { get; set; }
}

public class Activity
{
    public int Id { get; set; }
    public int UnitId { get; set; }
    public string? ActivityNumber { get; set; }
    public string Name { get; set; } = "";
    public string ActivityType { get; set; } = "project"; // project / kpi
    public double Weight { get; set; }
    public int? OwnerUserId { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public List<ActivityCollaborator> Collaborators { get; set; } = new();
}

public class ActivityCollaborator
{
    public int Id { get; set; }
    public int ActivityId { get; set; }
    public Activity? Activity { get; set; }
    public int UnitId { get; set; }
    public double Weight { get; set; }
}

public class Target
{
    public int Id { get; set; }
    public int ActivityId { get; set; }
    public int YearId { get; set; }
    public double StartValue { get; set; }
    public string DistributionType { get; set; } = "uniform"; // uniform / manual / kpi
    public List<TargetPeriod> Periods { get; set; } = new();
}

public class TargetPeriod
{
    public int Id { get; set; }
    public int TargetId { get; set; }
    public Target? Target { get; set; }
    public int PeriodId { get; set; }
    public double TargetValue { get; set; }
    public bool IsActive { get; set; } = true;
}

public class ProgressEntry
{
    public int Id { get; set; }
    public int ActivityId { get; set; }
    public int PeriodId { get; set; }
    public double ProgressValue { get; set; }
    public string? Note { get; set; }
    public string Status { get; set; } = "draft"; // draft / submitted / final
    public int CurrentStepIndex { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class ProgressHistory
{
    public int Id { get; set; }
    public int ProgressEntryId { get; set; }
    public int UserId { get; set; }
    public string UserName { get; set; } = "";
    public string Action { get; set; } = "";
    public double? OldValue { get; set; }
    public double? NewValue { get; set; }
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class AuditLog
{
    public int Id { get; set; }
    public int? UserId { get; set; }
    public string Action { get; set; } = "";
    public string? Detail { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
