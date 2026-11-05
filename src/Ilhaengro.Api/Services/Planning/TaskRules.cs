using Ilhaengro.Api.Models;

namespace Ilhaengro.Api.Services.Planning;

public static class TaskRules
{
    public static bool IsAllowedTransition(string current, string requested)
    {
        if (string.Equals(current, requested, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return current.ToLowerInvariant() switch
        {
            TaskStatuses.Pending => requested is TaskStatuses.Completed or TaskStatuses.Postponed or TaskStatuses.Cancelled,
            TaskStatuses.Postponed => requested is TaskStatuses.Pending or TaskStatuses.Cancelled,
            TaskStatuses.Completed => requested is TaskStatuses.Pending,
            TaskStatuses.Cancelled => requested is TaskStatuses.Pending,
            _ => false,
        };
    }
}
