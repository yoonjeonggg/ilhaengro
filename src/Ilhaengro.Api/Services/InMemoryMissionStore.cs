using System.Collections.Concurrent;
using Ilhaengro.Api.Models;

namespace Ilhaengro.Api.Services;

public sealed class InMemoryMissionStore
{
    private readonly ConcurrentDictionary<string, MissionSession> sessions = new();

    public MissionSession Create()
    {
        var session = new MissionSession(
            Guid.NewGuid().ToString("N"),
            DateTimeOffset.UtcNow.AddHours(2));
        sessions[session.Id] = session;
        return session;
    }

    public MissionSession Get(string id)
    {
        if (!sessions.TryGetValue(id, out var session) || session.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            sessions.TryRemove(id, out _);
            throw new KeyNotFoundException("세션을 찾을 수 없거나 만료되었습니다.");
        }

        return session;
    }

    public bool Delete(string id) => sessions.TryRemove(id, out _);
}

public sealed class MissionSession(string id, DateTimeOffset expiresAt)
{
    public string Id { get; } = id;
    public DateTimeOffset ExpiresAt { get; } = expiresAt;
    public string? OriginalMessage { get; set; }
    public MissionPlan? Plan { get; set; }
    public DiscoveryResponse? Discovery { get; set; }
}
