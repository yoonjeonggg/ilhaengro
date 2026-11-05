using Ilhaengro.Api.Models;
using Ilhaengro.Api.Services.Planning;

namespace Ilhaengro.Api.Services;

public sealed class PlanningTools
{
    public MissionPlan Validate(MissionPlan plan, IReadOnlyList<SuggestedTask> approvedTasks, string returnDeadline)
    {
        if (!PlanTimeline.TryParseMinutes(returnDeadline, out _))
        {
            throw new InvalidOperationException("귀가 마감 시간이 올바르지 않습니다.");
        }

        EnsureSameTaskSet(plan, approvedTasks);
        EnsureValidSchedule(plan);

        return plan with
        {
            ReturnDeadline = returnDeadline,
            Warning = PlanTimeline.WithDeadlineWarning(plan.Warning, PlanTimeline.ExceedsDeadline(plan.Tasks, returnDeadline)),
        };
    }

    private static void EnsureSameTaskSet(MissionPlan plan, IReadOnlyList<SuggestedTask> approvedTasks)
    {
        var approvedIds = approvedTasks.Select(task => task.Id).ToHashSet(StringComparer.Ordinal);
        var planIds = plan.Tasks.Select(task => task.Id).ToArray();
        if (planIds.Length != approvedIds.Count ||
            planIds.Length != planIds.Distinct(StringComparer.Ordinal).Count() ||
            planIds.Any(id => !approvedIds.Contains(id)))
        {
            throw new InvalidOperationException("AI 계획에 승인하지 않은 업무가 포함되었거나 중복 업무가 있습니다.");
        }
    }

    private static void EnsureValidSchedule(MissionPlan plan)
    {
        foreach (var task in plan.Tasks)
        {
            if (!PlanTimeline.TryParseMinutes(task.StartTime, out _))
            {
                throw new InvalidOperationException("업무 시작 시간은 HH:mm 형식이어야 합니다.");
            }

            if (!PlanTimeline.IsValidDuration(task.EstimatedMinutes))
            {
                throw new InvalidOperationException("업무 예상 시간은 1분에서 480분 사이여야 합니다.");
            }
        }

        if (PlanTimeline.HasConflict(plan.Tasks))
        {
            throw new InvalidOperationException("AI 계획에 업무 시간 충돌이 있습니다.");
        }
    }
}
