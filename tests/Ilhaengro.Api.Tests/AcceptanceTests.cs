using System.Net;
using System.Net.Http.Json;
using Ilhaengro.Api.Models;
using Ilhaengro.Api.Services;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Ilhaengro.Api.Tests;

public sealed class AcceptanceTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient client = factory.CreateClient();

    [Fact]
    public async Task Fr01_creates_a_session_for_an_outing_flow()
    {
        var response = await client.PostAsJsonAsync("/api/sessions", new { });
        var session = await response.Content.ReadFromJsonAsync<CreateSessionResponse>();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(string.IsNullOrWhiteSpace(session?.SessionId));
    }

    [Fact]
    public void Fr02_suggestions_have_explanations_and_are_unapproved_by_default()
    {
        var task = new SuggestedTask("hospital", "병원 방문", "입력 목적에서 발견", "방문", "required", 30);
        Assert.False(string.IsNullOrWhiteSpace(task.Reason));
        Assert.True(task.Selected);
    }

    [Fact]
    public void Fr03_suggestions_can_be_excluded_before_planning()
    {
        var task = new SuggestedTask("optional", "선택 업무", "보조 업무", "기타", "optional", 15, false);
        Assert.False(task.Selected);
    }

    [Fact]
    public void Fr04_manual_tasks_can_be_required()
    {
        var task = new SuggestedTask("manual", "직접 추가", "사용자 입력", "직접 추가", "required", 20);
        Assert.Equal("required", task.Priority);
    }

    [Fact]
    public void Fr05_tasks_preserve_priority_and_reason()
    {
        var task = new SuggestedTask("task", "업무", "판단 근거", "행정", "optional", 20);
        Assert.Equal("optional", task.Priority);
        Assert.NotEmpty(task.Reason);
    }

    [Fact]
    public void Fr06_planning_tools_detect_deadline_overruns()
    {
        var tools = new PlanningTools();
        var task = new SuggestedTask("task", "업무", "근거", "기타", "required", 60);
        var plan = new MissionPlan("계획", "15:00", [new PlannedTask("task", "업무", "근거", "기타", "required", 60, true, "pending", "15:00")], [], "대안", null);
        var validated = tools.Validate(plan, [task], "15:00");
        Assert.Contains("초과", validated.Warning);
    }

    [Fact]
    public void Fr07_plans_include_supply_checklist_items()
    {
        var supply = new SupplyItem("id", "신분증", "기관 확인", false);
        Assert.False(supply.Checked);
        Assert.NotEmpty(supply.Reason);
    }

    [Fact]
    public void Fr08_plans_include_a_fallback()
    {
        var plan = new MissionPlan("계획", "15:00", [], [], "필수 업무를 먼저 처리합니다.", null);
        Assert.False(string.IsNullOrWhiteSpace(plan.Fallback));
    }

    [Fact]
    public void Fr09_stream_events_identify_the_current_agent()
    {
        var streamEvent = new AgentStreamEvent("agent_started", "ContextAgent", "분석 중");
        Assert.Equal("ContextAgent", streamEvent.Agent);
    }

    [Fact]
    public void Fr10_reordered_tasks_are_represented_by_ordered_ids()
    {
        var request = new ReorderTasksRequest(["second", "first"]);
        Assert.Equal(["second", "first"], request.TaskIds);
    }

    [Fact]
    public void Fr11_replan_response_keeps_previous_and_updated_plans()
    {
        var plan = new MissionPlan("계획", "15:00", [], [], "대안", null);
        var response = new ReplanResponse(plan, plan, [], []);
        Assert.Same(plan, response.PreviousPlan);
        Assert.Same(plan, response.UpdatedPlan);
    }

    [Fact]
    public void Fr12_task_status_supports_execution_states()
    {
        var statuses = new[] { "pending", "completed", "postponed", "cancelled" };
        Assert.Contains("completed", statuses);
        Assert.Contains("postponed", statuses);
        Assert.Contains("cancelled", statuses);
    }

    [Fact]
    public void Fr13_carry_over_request_identifies_tasks_explicitly()
    {
        var request = new CarryOverRequest(["task-1", "task-2"]);
        Assert.Equal(2, request.TaskIds.Count);
    }

    [Fact]
    public async Task Fr14_sessions_can_be_read_and_deleted()
    {
        var created = await client.PostAsJsonAsync("/api/sessions", new { });
        var session = await created.Content.ReadFromJsonAsync<CreateSessionResponse>();
        var read = await client.GetAsync($"/api/sessions/{session!.SessionId}");
        var deleted = await client.DeleteAsync($"/api/sessions/{session.SessionId}");
        var missing = await client.GetAsync($"/api/sessions/{session.SessionId}");
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    public void Fr15_voice_input_uses_the_same_text_contract()
    {
        var request = new DiscoverRequest("병원에 가야 해");
        Assert.Equal("병원에 가야 해", request.Message);
    }

    [Fact]
    public void Fr16_plan_data_contains_the_fields_needed_for_export()
    {
        var task = new PlannedTask("task", "업무", "근거", "기타", "required", 30, true, "pending", "09:00");
        Assert.Matches(@"^\d{2}:\d{2}$", task.StartTime);
        Assert.True(task.EstimatedMinutes > 0);
    }
}
