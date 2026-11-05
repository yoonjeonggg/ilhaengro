using System.Globalization;
using Ilhaengro.Api.Models;

namespace Ilhaengro.Api.Services.Planning;

// Deterministic time arithmetic on minutes-of-day; no clock, I/O or AI involved.
public static class PlanTimeline
{
    public const string DeadlineWarning = "예상 소요 시간이 귀가 마감 시간을 초과합니다.";
    public const int MinTaskMinutes = 1;
    public const int MaxTaskMinutes = 480;

    public static bool TryParseMinutes(string? value, out int minutes)
    {
        minutes = 0;
        if (!TimeOnly.TryParseExact(value, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var time))
        {
            return false;
        }

        minutes = time.Hour * 60 + time.Minute;
        return true;
    }

    public static bool IsValidDuration(int minutes) => minutes is >= MinTaskMinutes and <= MaxTaskMinutes;

    public static int StartMinutes(PlannedTask task) =>
        TryParseMinutes(task.StartTime, out var minutes)
            ? minutes
            : throw new FormatException("업무 시작 시간은 HH:mm 형식이어야 합니다.");

    public static int EndMinutes(PlannedTask task) => StartMinutes(task) + task.EstimatedMinutes;

    public static int LatestEnd(IEnumerable<PlannedTask> tasks) =>
        tasks.Select(EndMinutes).DefaultIfEmpty(0).Max();

    public static bool HasConflict(IEnumerable<PlannedTask> tasks)
    {
        var previousEnd = 0;
        foreach (var task in tasks.OrderBy(StartMinutes))
        {
            if (StartMinutes(task) < previousEnd)
            {
                return true;
            }

            previousEnd = EndMinutes(task);
        }

        return false;
    }

    public static bool ExceedsDeadline(IEnumerable<PlannedTask> tasks, string returnDeadline) =>
        TryParseMinutes(returnDeadline, out var deadline) && LatestEnd(tasks) > deadline;

    public static string? WithDeadlineWarning(string? warning, bool exceedsDeadline)
    {
        var rest = (warning ?? string.Empty).Replace(DeadlineWarning, string.Empty).Trim();
        var parts = new List<string>();
        if (rest.Length > 0)
        {
            parts.Add(rest);
        }

        if (exceedsDeadline)
        {
            parts.Add(DeadlineWarning);
        }

        return parts.Count == 0 ? null : string.Join(" ", parts);
    }
}
