using Ilhaengro.Api.Models;
using Ilhaengro.Api.Services;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.Services.AddProblemDetails();
builder.Services.AddSingleton<InMemoryMissionStore>();
builder.Services.AddSingleton<CopilotAgentOrchestrator>();

var app = builder.Build();
app.UseExceptionHandler();

app.MapGet("/health", () => Results.Ok(new { status = "Healthy" }));

var api = app.MapGroup("/api");
api.MapPost("/sessions", (InMemoryMissionStore store) =>
{
    var session = store.Create();
    return Results.Ok(new CreateSessionResponse(session.Id, session.ExpiresAt));
});

api.MapPost("/sessions/{sessionId}/discover", async (
    string sessionId,
    DiscoverRequest request,
    InMemoryMissionStore store,
    CopilotAgentOrchestrator agents,
    CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.Message) || request.Message.Length > 4000)
    {
        return Results.BadRequest(new { error = new { code = "INVALID_REQUEST", message = "외출 목적을 입력해 주세요." } });
    }

    try
    {
        var session = store.Get(sessionId);
        var discovery = await agents.DiscoverAsync(request.Message, cancellationToken);
        session.OriginalMessage = request.Message;
        session.Discovery = discovery;
        return Results.Ok(discovery);
    }
    catch (KeyNotFoundException exception)
    {
        return Results.NotFound(new { error = new { code = "SESSION_NOT_FOUND", message = exception.Message } });
    }
    catch (Exception exception) when (exception is InvalidOperationException or TimeoutException)
    {
        return Results.Problem("AI 제안을 생성하지 못했습니다. 입력을 보존했으니 다시 시도해 주세요.", statusCode: 502, extensions: new Dictionary<string, object?> { ["code"] = "AI_REQUEST_FAILED" });
    }
});

api.MapPost("/sessions/{sessionId}/plan", async (
    string sessionId,
    PlanRequest request,
    InMemoryMissionStore store,
    CopilotAgentOrchestrator agents,
    CancellationToken cancellationToken) =>
{
    if (request.Tasks is null || request.Tasks.Count == 0)
    {
        return Results.BadRequest(new { error = new { code = "INVALID_REQUEST", message = "승인한 업무가 없습니다." } });
    }

    try
    {
        var session = store.Get(sessionId);
        var plan = await agents.PlanAsync(request.Tasks.Where(task => task.Selected).ToArray(), request.Supplies, request.ReturnDeadline, cancellationToken);
        session.Plan = plan;
        return Results.Ok(plan);
    }
    catch (KeyNotFoundException exception)
    {
        return Results.NotFound(new { error = new { code = "SESSION_NOT_FOUND", message = exception.Message } });
    }
    catch (Exception exception) when (exception is InvalidOperationException or TimeoutException)
    {
        return Results.Problem("AI 계획을 생성하지 못했습니다. 승인 상태는 유지됩니다.", statusCode: 502, extensions: new Dictionary<string, object?> { ["code"] = "AI_REQUEST_FAILED" });
    }
});

api.MapPatch("/sessions/{sessionId}/tasks/{taskId}", (
    string sessionId,
    string taskId,
    UpdateTaskRequest request,
    InMemoryMissionStore store) =>
{
    try
    {
        var session = store.Get(sessionId);
        if (session.Plan is null)
        {
            return Results.Conflict(new { error = new { code = "PLAN_NOT_FOUND", message = "먼저 계획을 생성해 주세요." } });
        }

        var allowed = new[] { "pending", "completed", "postponed", "cancelled" };
        if (!allowed.Contains(request.Status, StringComparer.OrdinalIgnoreCase))
        {
            return Results.BadRequest(new { error = new { code = "INVALID_REQUEST", message = "허용되지 않은 업무 상태입니다." } });
        }

        var task = session.Plan.Tasks.FirstOrDefault(item => item.Id == taskId);
        if (task is null)
        {
            return Results.NotFound(new { error = new { code = "TASK_NOT_FOUND", message = "업무를 찾지 못했습니다." } });
        }

        session.Plan = session.Plan with
        {
            Tasks = session.Plan.Tasks.Select(item => item.Id == taskId ? item with { Status = request.Status.ToLowerInvariant() } : item).ToArray(),
        };
        return Results.Ok(new { taskId, status = request.Status.ToLowerInvariant() });
    }
    catch (KeyNotFoundException exception)
    {
        return Results.NotFound(new { error = new { code = "SESSION_NOT_FOUND", message = exception.Message } });
    }
});

api.MapPost("/sessions/{sessionId}/replan", async (
    string sessionId,
    ReplanRequest request,
    InMemoryMissionStore store,
    CopilotAgentOrchestrator agents,
    CancellationToken cancellationToken) =>
{
    try
    {
        var session = store.Get(sessionId);
        if (session.Plan is null)
        {
            return Results.Conflict(new { error = new { code = "PLAN_NOT_FOUND", message = "먼저 계획을 생성해 주세요." } });
        }

        var previousPlan = session.Plan;
        var remaining = previousPlan.Tasks
            .Where(task => task.Status is not "completed" and not "cancelled")
            .Select(task => new SuggestedTask(task.Id, task.Title, task.Reason, task.Category, task.Priority, task.EstimatedMinutes, true))
            .ToArray();
        var updated = await agents.PlanAsync(remaining, previousPlan.Supplies, previousPlan.ReturnDeadline, cancellationToken);
        session.Plan = updated;
        return Results.Ok(new ReplanResponse(previousPlan, updated, Array.Empty<string>(), Array.Empty<string>()));
    }
    catch (KeyNotFoundException exception)
    {
        return Results.NotFound(new { error = new { code = "SESSION_NOT_FOUND", message = exception.Message } });
    }
    catch (Exception exception) when (exception is InvalidOperationException or TimeoutException)
    {
        return Results.Problem("재계획을 생성하지 못했습니다. 기존 계획은 유지됩니다.", statusCode: 502, extensions: new Dictionary<string, object?> { ["code"] = "AI_REQUEST_FAILED" });
    }
});

app.MapDefaultEndpoints();
app.Run();
