using Ilhaengro.Api.Models;
using Ilhaengro.Api.Services;
using Ilhaengro.Api.Services.Planning;

namespace Ilhaengro.Api.Tests;

public sealed class PlanningRulesTests
{
    private static PlannedTask Task(string id, string start, int minutes) =>
        new(id, id, "reason", "cat", "required", minutes, true, "pending", start);

    private static MissionPlan Plan(string? warning, params PlannedTask[] tasks) =>
        new("plan", "15:00", tasks, [], "fallback", warning);

    private static SuggestedTask Approved(string id) => new(id, id, "reason", "cat", "required", 30);

    [Theory]
    [InlineData("09:30", true, 570)]
    [InlineData("24:00", false, 0)]
    [InlineData("9:5", false, 0)]
    [InlineData("", false, 0)]
    public void TryParseMinutes_parses_only_HH_mm(string value, bool ok, int expected)
    {
        Assert.Equal(ok, PlanTimeline.TryParseMinutes(value, out var minutes));
        Assert.Equal(expected, minutes);
    }

    [Fact]
    public void HasConflict_detects_overlap_regardless_of_list_order()
    {
        Assert.True(PlanTimeline.HasConflict([Task("b", "10:00", 30), Task("a", "09:30", 45)]));
        Assert.False(PlanTimeline.HasConflict([Task("b", "10:00", 30), Task("a", "09:30", 30)]));
    }

    [Fact]
    public void ExceedsDeadline_does_not_wrap_past_midnight()
    {
        Assert.True(PlanTimeline.ExceedsDeadline([Task("a", "23:50", 30)], "15:00"));
        Assert.False(PlanTimeline.ExceedsDeadline([Task("a", "14:00", 60)], "15:00"));
    }

    [Fact]
    public void WithDeadlineWarning_is_idempotent_and_keeps_other_warnings()
    {
        var once = PlanTimeline.WithDeadlineWarning("추정값입니다.", true);
        Assert.Equal(once, PlanTimeline.WithDeadlineWarning(once, true));
        Assert.Equal("추정값입니다.", PlanTimeline.WithDeadlineWarning(once, false));
        Assert.Null(PlanTimeline.WithDeadlineWarning(PlanTimeline.DeadlineWarning, false));
    }

    [Theory]
    [InlineData("pending", "completed", true)]
    [InlineData("pending", "pending", true)]
    [InlineData("completed", "postponed", false)]
    [InlineData("postponed", "completed", false)]
    [InlineData("cancelled", "pending", true)]
    public void IsAllowedTransition_follows_status_machine(string from, string to, bool allowed) =>
        Assert.Equal(allowed, TaskRules.IsAllowedTransition(from, to));

    [Fact]
    public void Validate_rejects_conflicting_unapproved_and_duplicate_tasks()
    {
        var tools = new PlanningTools();
        Assert.Throws<InvalidOperationException>(() =>
            tools.Validate(Plan(null, Task("a", "09:00", 60), Task("b", "09:30", 30)), [Approved("a"), Approved("b")], "15:00"));
        Assert.Throws<InvalidOperationException>(() =>
            tools.Validate(Plan(null, Task("a", "09:00", 30), Task("x", "10:00", 30)), [Approved("a"), Approved("b")], "15:00"));
        Assert.Throws<InvalidOperationException>(() =>
            tools.Validate(Plan(null, Task("a", "09:00", 30), Task("a", "10:00", 30)), [Approved("a"), Approved("b")], "15:00"));
    }

    [Fact]
    public void Validate_accepts_valid_plan_without_warning()
    {
        var validated = new PlanningTools().Validate(Plan(null, Task("a", "09:00", 30)), [Approved("a")], "15:00");
        Assert.Null(validated.Warning);
    }
}
