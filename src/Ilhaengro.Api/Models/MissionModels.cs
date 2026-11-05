namespace Ilhaengro.Api.Models;

public sealed record CreateSessionResponse(string SessionId, DateTimeOffset ExpiresAt);

public sealed record DiscoverRequest(string Message);

public sealed record DiscoveryResponse(
    IReadOnlyList<SuggestedTask> Tasks,
    IReadOnlyList<SupplyItem> Supplies,
    IReadOnlyList<string> ClarifyingQuestions);

public sealed record SuggestedTask(
    string Id,
    string Title,
    string Reason,
    string Category,
    string Priority,
    int EstimatedMinutes,
    bool Selected = true);

public sealed record SupplyItem(
    string Id,
    string Title,
    string Reason,
    bool Checked = false);

public sealed record PlanRequest(
    IReadOnlyList<SuggestedTask> Tasks,
    IReadOnlyList<SupplyItem> Supplies,
    string ReturnDeadline = "15:00");

public sealed record PlannedTask(
    string Id,
    string Title,
    string Reason,
    string Category,
    string Priority,
    int EstimatedMinutes,
    bool Selected,
    string Status,
    string StartTime);

public sealed record MissionPlan(
    string Title,
    string ReturnDeadline,
    IReadOnlyList<PlannedTask> Tasks,
    IReadOnlyList<SupplyItem> Supplies,
    string Fallback,
    string? Warning);

public sealed record UpdateTaskRequest(string Status);
public sealed record ReplanRequest(string Message);
public sealed record ReplanResponse(MissionPlan PreviousPlan, MissionPlan UpdatedPlan, IReadOnlyList<string> MovedTasks, IReadOnlyList<string> Warnings);
