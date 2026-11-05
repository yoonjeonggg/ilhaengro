namespace Ilhaengro.Api.Models;

public sealed record AgentStreamEvent(string Type, string Agent, string Message, object? Data = null);
