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
            "pending" => requested is "completed" or "postponed" or "cancelled",
            "postponed" => requested is "pending" or "cancelled",
            "completed" => requested is "pending",
            "cancelled" => requested is "pending",
            _ => false,
        };
    }
}
