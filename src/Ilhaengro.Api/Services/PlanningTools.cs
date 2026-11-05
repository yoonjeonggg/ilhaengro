using Ilhaengro.Api.Models;

namespace Ilhaengro.Api.Services;

public sealed class PlanningTools
{
    public MissionPlan Validate(MissionPlan plan, IReadOnlyList<SuggestedTask> approvedTasks, string returnDeadline)
    {
        if (!TimeOnly.TryParseExact(returnDeadline, "HH:mm", out var deadline))
        {
            throw new InvalidOperationException("귀가 마감 시간이 올바르지 않습니다.");
        }

        var approvedIds = approvedTasks.Select(task => task.Id).ToHashSet(StringComparer.Ordinal);
        var planIds = plan.Tasks.Select(task => task.Id).ToArray();
        if (planIds.Length != approvedIds.Count ||
            planIds.Length != planIds.Distinct(StringComparer.Ordinal).Count() ||
            approvedIds.Any(id => !planIds.Contains(id, StringComparer.Ordinal)) ||
            planIds.Any(id => !approvedIds.Contains(id)))
        {
            throw new InvalidOperationException("AI 계획에 승인하지 않은 업무가 포함되었거나 중복 업무가 있습니다.");
        }

        var ordered = plan.Tasks
            .OrderBy(task => ParseTime(task.StartTime, "업무 시작 시간"))
            .ToArray();
        var previousEnd = TimeOnly.MinValue;
        foreach (var task in ordered)
        {
            if (task.EstimatedMinutes is < 1 or > 480)
            {
                throw new InvalidOperationException("업무 예상 시간은 1분에서 480분 사이여야 합니다.");
            }

            var start = ParseTime(task.StartTime, "업무 시작 시간");
            var end = start.AddMinutes(task.EstimatedMinutes);
            if (start < previousEnd)
            {
                throw new InvalidOperationException("AI 계획에 업무 시간 충돌이 있습니다.");
            }

            previousEnd = end;
        }

        var warnings = plan.Warning is null ? new List<string>() : [plan.Warning];
        if (previousEnd > deadline)
        {
            warnings.Add("예상 소요 시간이 귀가 마감 시간을 초과합니다.");
        }

        return plan with
        {
            ReturnDeadline = returnDeadline,
            Warning = warnings.Count == 0 ? null : string.Join(" ", warnings.Distinct()),
        };
    }

    private static TimeOnly ParseTime(string value, string label)
    {
        if (!TimeOnly.TryParseExact(value, "HH:mm", out var time))
        {
            throw new InvalidOperationException($"{label}은 HH:mm 형식이어야 합니다.");
        }

        return time;
    }
}
