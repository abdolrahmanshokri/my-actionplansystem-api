namespace ActionPlanApi.Dtos;

// ===== سال و دوره =====
public class YearDto
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

public class PeriodDto
{
    public int Id { get; set; }
    public int YearId { get; set; }
    public int MonthNumber { get; set; }
    public string MonthName { get; set; } = "";
    public string Title { get; set; } = "";
    public bool IsOpen { get; set; }
}

// ===== واحد =====
public class UnitDto
{
    public int Id { get; set; }
    public int? ParentId { get; set; }
    public string Name { get; set; } = "";
    public int SortOrder { get; set; }
}

// ===== مرحله‌ی تأیید =====
public class ApprovalStepDto
{
    public int Id { get; set; }
    public int UnitId { get; set; }
    public int StepOrder { get; set; }
    public string ApprovalType { get; set; } = "or";
    public int? RoleId { get; set; }
    public int? UserId { get; set; }
}

// ===== فعالیت =====
public class CollaboratorDto
{
    public int UnitId { get; set; }
    public double Weight { get; set; }
}

public class ActivityDto
{
    public int Id { get; set; }
    public int UnitId { get; set; }
    public string? ActivityNumber { get; set; }
    public string Name { get; set; } = "";
    public string ActivityType { get; set; } = "project";
    public double Weight { get; set; }
    public int? OwnerUserId { get; set; }
    public string? OwnerName { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public List<CollaboratorDto> Collaborators { get; set; } = new();
}

// ===== تارگت =====
public class TargetPeriodDto
{
    public int PeriodId { get; set; }
    public int MonthNumber { get; set; }
    public string MonthName { get; set; } = "";
    public double TargetValue { get; set; }
    public bool IsActive { get; set; } = true;
}

public class TargetDto
{
    public int Id { get; set; }
    public int ActivityId { get; set; }
    public int YearId { get; set; }
    public double StartValue { get; set; }
    public string DistributionType { get; set; } = "uniform";
    public List<TargetPeriodDto> Periods { get; set; } = new();
}

public class SaveTargetRequest
{
    public int ActivityId { get; set; }
    public int YearId { get; set; }
    public double StartValue { get; set; }
    public string DistributionType { get; set; } = "uniform";
    public Dictionary<int, double> PeriodValues { get; set; } = new();
    public Dictionary<int, bool>? ActiveValues { get; set; }
}

// ===== پیشرفت =====
public class ProgressEntryDto
{
    public int Id { get; set; }
    public int ActivityId { get; set; }
    public int PeriodId { get; set; }
    public double ProgressValue { get; set; }
    public string? Note { get; set; }
    public string Status { get; set; } = "draft";
    public int CurrentStepIndex { get; set; }
}

public class SaveProgressRequest
{
    public int ActivityId { get; set; }
    public int PeriodId { get; set; }
    public double ProgressValue { get; set; }
    public string? Note { get; set; }
}

public class ProgressActionRequest
{
    public int ActivityId { get; set; }
    public int PeriodId { get; set; }
    public double? NewValue { get; set; }
    public string? Note { get; set; }
    public int StepIndex { get; set; }
    public bool ToOwner { get; set; }
}

public class ProgressHistoryDto
{
    public int Id { get; set; }
    public string UserName { get; set; } = "";
    public string Action { get; set; } = "";
    public double? OldValue { get; set; }
    public double? NewValue { get; set; }
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class ApprovalQueueItemDto
{
    public int ProgressEntryId { get; set; }
    public int ActivityId { get; set; }
    public string ActivityName { get; set; } = "";
    public string? ActivityNumber { get; set; }
    public int UnitId { get; set; }
    public double ProgressValue { get; set; }
    public double? TargetThis { get; set; }
    public double Weight { get; set; }
    public string Status { get; set; } = "";
    public int CurrentStepIndex { get; set; }
    public string? CurrentHolderName { get; set; }
    public bool IsMyTurn { get; set; }
}

public class ApproveRequest
{
    public int ProgressEntryId { get; set; }
    public int StepIndex { get; set; }
    public double NewValue { get; set; }
    public string? Note { get; set; }
}

public class RejectRequest
{
    public int ProgressEntryId { get; set; }
    public int StepIndex { get; set; }
    public bool ToOwner { get; set; }
    public string? Note { get; set; }
}

public class ReopenRequest
{
    public int ProgressEntryId { get; set; }
    public string? Note { get; set; }
}

public class SettingValueDto
{
    public string Value { get; set; } = "";
}

public class DashboardActivityRowDto
{
    public int ActivityId { get; set; }
    public string ActivityName { get; set; } = "";
    public string? ActivityNumber { get; set; }
    public int UnitId { get; set; }
    public string UnitName { get; set; } = "";
    public double Progress { get; set; }
    public double Target { get; set; }
    public double Weight { get; set; }
    public string Status { get; set; } = "";
}

public class DashboardUnitRowDto
{
    public int UnitId { get; set; }
    public string UnitName { get; set; } = "";
    public int ActivityCount { get; set; }
    public double AvgProgress { get; set; }
    public double AvgTarget { get; set; }
    public double WeightedProgress { get; set; }
    public double WeightedTarget { get; set; }
}

public class DashboardStatsDto
{
    public int TotalActivities { get; set; }
    public int DraftCount { get; set; }
    public int SubmittedCount { get; set; }
    public int FinalCount { get; set; }
    public int NotStartedCount { get; set; }
    public double AvgProgress { get; set; }
    public List<DashboardUnitRowDto> UnitRows { get; set; } = new();
    public List<DashboardActivityRowDto> ActivityRows { get; set; } = new();
}

// ===== گزارش =====
public class ReportRowDto
{
    public string ActivityNumber { get; set; } = "";
    public string ActivityName { get; set; } = "";
    public string UnitRoot { get; set; } = "";
    public string UnitMid { get; set; } = "";
    public string UnitLeaf { get; set; } = "";
    public string Type { get; set; } = "";
    public double Weight { get; set; }
    public double Target { get; set; }
    public double Progress { get; set; }
    public double Achievement { get; set; }
    public string Status { get; set; } = "";
    public bool IsActive { get; set; }
}
