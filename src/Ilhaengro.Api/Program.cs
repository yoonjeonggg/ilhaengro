using Ilhaengro.Api.Models;
using Ilhaengro.Api.Services;
using Ilhaengro.Api.Services.Planning;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.Services.AddProblemDetails();
builder.Services.AddSingleton<InMemoryMissionStore>();
builder.Services.AddSingleton<CopilotAgentOrchestrator>();
builder.Services.AddSingleton<PlanningTools>();

var app = builder.Build();
app.UseExceptionHandler();

app.MapGet("/health", () => Results.Ok(new { status = "Healthy" }));

var api = app.MapGroup("/api");
api.MapPost("/sessions", (InMemoryMissionStore store) =>
{
    var session = store.Create();
    return Results.Ok(new CreateSessionResponse(session.Id, session.ExpiresAt));
});

api.MapGet("/sessions/{sessionId}", (string sessionId, InMemoryMissionStore store) =>
{
    try
    {
        var session = store.Get(sessionId);
        return Results.Ok(session);
    }
    catch (KeyNotFoundException exception)
    {
        return Results.NotFound(new { error = new { code = "SESSION_NOT_FOUND", message = exception.Message } });
    }
});

api.MapDelete("/sessions/{sessionId}", (string sessionId, InMemoryMissionStore store) =>
{
    return store.Delete(sessionId)
        ? Results.NoContent()
        : Results.NotFound(new { error = new { code = "SESSION_NOT_FOUND", message = "세션을 찾지 못했습니다." } });
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

api.MapPost("/sessions/{sessionId}/discover/stream", async (
    string sessionId,
    DiscoverRequest request,
    InMemoryMissionStore store,
    CopilotAgentOrchestrator agents,
    HttpResponse response,
    CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.Message) || request.Message.Length > 4000)
    {
        return Results.BadRequest(new { error = new { code = "INVALID_REQUEST", message = "외출 목적을 입력해 주세요." } });
    }

    response.ContentType = "text/event-stream";
    response.Headers.CacheControl = "no-cache";
    async Task Send(AgentStreamEvent item)
    {
        await response.WriteAsync($"event: {item.Type}\ndata: {System.Text.Json.JsonSerializer.Serialize(item)}\n\n", cancellationToken);
        await response.Body.FlushAsync(cancellationToken);
    }

    try
    {
        var session = store.Get(sessionId);
        await Send(new AgentStreamEvent("agent_started", "ContextAgent", "입력에서 목적과 시간 제약을 분석하고 있습니다."));
        var discovery = await agents.DiscoverAsync(request.Message, cancellationToken);
        session.OriginalMessage = request.Message;
        session.Discovery = discovery;
        await Send(new AgentStreamEvent("agent_completed", "DiscoveryAgent", "업무와 준비물 제안을 완료했습니다.", discovery));
        await Send(new AgentStreamEvent("completed", "DiscoveryAgent", "제안 결과를 확인해 주세요.", discovery));
    }
    catch (KeyNotFoundException exception)
    {
        await Send(new AgentStreamEvent("error", "DiscoveryAgent", exception.Message));
    }
    catch (Exception exception) when (exception is InvalidOperationException or TimeoutException)
    {
        await Send(new AgentStreamEvent("error", "DiscoveryAgent", "AI 제안을 생성하지 못했습니다."));
    }

    return Results.Empty;
});

api.MapPost("/sessions/{sessionId}/plan", async (
    string sessionId,
    PlanRequest request,
    InMemoryMissionStore store,
    CopilotAgentOrchestrator agents,
    PlanningTools planningTools,
    CancellationToken cancellationToken) =>
{
    if (request.Tasks is null || request.Tasks.Count == 0)
    {
        return Results.BadRequest(new { error = new { code = "INVALID_REQUEST", message = "승인한 업무가 없습니다." } });
    }

    if (!TimeOnly.TryParseExact(request.ReturnDeadline, "HH:mm", out _))
    {
        return Results.BadRequest(new { error = new { code = "INVALID_REQUEST", message = "귀가 마감 시간은 HH:mm 형식이어야 합니다." } });
    }

    try
    {
        var session = store.Get(sessionId);
        var approvedTasks = request.Tasks.Where(task => task.Selected).ToArray();
        if (approvedTasks.Length == 0)
        {
            return Results.BadRequest(new { error = new { code = "INVALID_REQUEST", message = "승인한 업무가 없습니다." } });
        }

        var plan = await agents.PlanAsync(approvedTasks, request.Supplies, request.ReturnDeadline, null, cancellationToken);
        plan = planningTools.Validate(plan, approvedTasks, request.ReturnDeadline);
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

        var status = request.Status?.ToLowerInvariant();
        var allowed = new[] { "pending", "completed", "postponed", "cancelled" };
        if (status is not null && !allowed.Contains(status, StringComparer.OrdinalIgnoreCase))
        {
            return Results.BadRequest(new { error = new { code = "INVALID_REQUEST", message = "허용되지 않은 업무 상태입니다." } });
        }

        var task = session.Plan.Tasks.FirstOrDefault(item => item.Id == taskId);
        if (task is null)
        {
            return Results.NotFound(new { error = new { code = "TASK_NOT_FOUND", message = "업무를 찾지 못했습니다." } });
        }

        if (status is not null && !TaskRules.IsAllowedTransition(task.Status, status))
        {
            return Results.Conflict(new { error = new { code = "INVALID_STATUS_TRANSITION", message = $"현재 상태 {task.Status}에서는 {status}로 변경할 수 없습니다." } });
        }

        if (request.Title is not null && string.IsNullOrWhiteSpace(request.Title))
        {
            return Results.BadRequest(new { error = new { code = "INVALID_REQUEST", message = "업무 제목을 입력해 주세요." } });
        }

        if (request.EstimatedMinutes is < 1 or > 480)
        {
            return Results.BadRequest(new { error = new { code = "INVALID_REQUEST", message = "예상 시간은 1분에서 480분 사이여야 합니다." } });
        }

        if (request.Priority is not null && request.Priority is not ("required" or "optional"))
        {
            return Results.BadRequest(new { error = new { code = "INVALID_REQUEST", message = "업무 우선순위가 올바르지 않습니다." } });
        }

        session.Plan = session.Plan with
        {
            Tasks = session.Plan.Tasks.Select(item => item.Id == taskId ? item with
            {
                Status = status ?? item.Status,
                Title = request.Title ?? item.Title,
                Priority = request.Priority ?? item.Priority,
                EstimatedMinutes = request.EstimatedMinutes ?? item.EstimatedMinutes,
            } : item).ToArray(),
        };
        return Results.Ok(new { taskId, status = status ?? task.Status });
    }
    catch (KeyNotFoundException exception)
    {
        return Results.NotFound(new { error = new { code = "SESSION_NOT_FOUND", message = exception.Message } });
    }
});

api.MapPost("/sessions/{sessionId}/tasks", (
    string sessionId,
    AddTaskRequest request,
    InMemoryMissionStore store) =>
{
    try
    {
        var session = store.Get(sessionId);
        if (session.Plan is null || string.IsNullOrWhiteSpace(request.Title) || request.EstimatedMinutes is < 1 or > 480)
        {
            return Results.BadRequest(new { error = new { code = "INVALID_REQUEST", message = "계획, 업무 제목, 1~480분의 예상 시간이 필요합니다." } });
        }

        if (request.Priority is not ("required" or "optional"))
        {
            return Results.BadRequest(new { error = new { code = "INVALID_REQUEST", message = "업무 우선순위가 올바르지 않습니다." } });
        }

        if (session.Plan.Tasks.Any(task => task.Id == request.Id))
        {
            return Results.Conflict(new { error = new { code = "DUPLICATE_TASK", message = "이미 존재하는 업무 ID입니다." } });
        }

        var start = session.Plan.Tasks.LastOrDefault()?.StartTime ?? "09:00";
        var newTask = new PlannedTask(request.Id, request.Title.Trim(), request.Reason, request.Category, request.Priority, request.EstimatedMinutes, request.Selected, "pending", start);
        session.Plan = session.Plan with { Tasks = session.Plan.Tasks.Append(newTask).ToArray() };
        return Results.Ok(session.Plan);
    }
    catch (KeyNotFoundException exception)
    {
        return Results.NotFound(new { error = new { code = "SESSION_NOT_FOUND", message = exception.Message } });
    }
});

api.MapPost("/sessions/{sessionId}/tasks/reorder", (
    string sessionId,
    ReorderTasksRequest request,
    InMemoryMissionStore store) =>
{
    try
    {
        var session = store.Get(sessionId);
        if (session.Plan is null || request.TaskIds.Count != session.Plan.Tasks.Count || request.TaskIds.Distinct().Count() != request.TaskIds.Count)
        {
            return Results.BadRequest(new { error = new { code = "INVALID_REQUEST", message = "전체 업무를 중복 없이 전달해 주세요." } });
        }

        var tasks = request.TaskIds.Select(id => session.Plan.Tasks.FirstOrDefault(task => task.Id == id)).ToArray();
        if (tasks.Any(task => task is null))
        {
            return Results.BadRequest(new { error = new { code = "INVALID_REQUEST", message = "존재하지 않는 업무가 포함되었습니다." } });
        }

        session.Plan = session.Plan with { Tasks = tasks! };
        return Results.Ok(session.Plan);
    }
    catch (KeyNotFoundException exception)
    {
        return Results.NotFound(new { error = new { code = "SESSION_NOT_FOUND", message = exception.Message } });
    }
});

api.MapPost("/sessions/{sessionId}/carry-over", (
    string sessionId,
    CarryOverRequest request,
    InMemoryMissionStore store) =>
{
    try
    {
        var session = store.Get(sessionId);
        var ids = request.TaskIds.Distinct().ToArray();
        var found = session.Plan?.Tasks.Where(task => ids.Contains(task.Id) && (task.Status is "postponed" or "cancelled")).Select(task => task.Id).ToArray() ?? [];
        if (found.Length != ids.Length)
        {
            return Results.BadRequest(new { error = new { code = "INVALID_REQUEST", message = "보류 또는 취소된 업무만 이월할 수 있습니다." } });
        }

        var next = store.Create();
        var carriedTasks = session.Plan!.Tasks
            .Where(task => found.Contains(task.Id))
            .Select((task, index) => task with
            {
                Status = "pending",
                StartTime = index == 0 ? "09:00" : task.StartTime,
            })
            .ToArray();
        next.OriginalMessage = session.OriginalMessage;
        next.Plan = session.Plan with
        {
            Title = "이월된 외출 계획",
            Tasks = carriedTasks,
            Warning = "이전 계획에서 이월된 업무입니다. 시간과 순서를 다시 확인해 주세요.",
        };
        return Results.Ok(new { sessionId = next.Id, taskIds = found, plan = next.Plan });
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
    PlanningTools planningTools,
    CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.Message) || request.Message.Length > 2000)
    {
        return Results.BadRequest(new { error = new { code = "INVALID_REQUEST", message = "재계획 상황을 입력해 주세요." } });
    }

    try
    {
        var session = store.Get(sessionId);
        if (session.Plan is null)
        {
            return Results.Conflict(new { error = new { code = "PLAN_NOT_FOUND", message = "먼저 계획을 생성해 주세요." } });
        }

        var previousPlan = session.Plan;
        var completedTasks = previousPlan.Tasks.Where(task => task.Status is "completed" or "cancelled").ToArray();
        var remaining = previousPlan.Tasks
            .Where(task => task.Status is not "completed" and not "cancelled")
            .Select(task => new SuggestedTask(task.Id, task.Title, task.Reason, task.Category, task.Priority, task.EstimatedMinutes, true))
            .ToArray();
        var replanned = await agents.PlanAsync(remaining, previousPlan.Supplies, previousPlan.ReturnDeadline, request.Message, cancellationToken);
        replanned = planningTools.Validate(replanned, remaining, previousPlan.ReturnDeadline);
        var updated = replanned with { Tasks = completedTasks.Concat(replanned.Tasks).ToArray() };
        session.Plan = updated;
        var movedTasks = remaining.Where(task => !replanned.Tasks.Any(updatedTask => updatedTask.Id == task.Id)).Select(task => task.Id).ToArray();
        return Results.Ok(new ReplanResponse(previousPlan, updated, movedTasks, Array.Empty<string>()));
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

public partial class Program
{
}
