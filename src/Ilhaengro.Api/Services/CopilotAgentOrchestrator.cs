using Ilhaengro.Api.Models;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace Ilhaengro.Api.Services;

public sealed class CopilotAgentOrchestrator(
    IConfiguration configuration,
    ILoggerFactory loggerFactory,
    ILogger<CopilotAgentOrchestrator> logger)
{
    private readonly string model = configuration["COPILOT_MODEL"] ?? "gpt-5";

    public async Task<DiscoveryResponse> DiscoverAsync(string message, CancellationToken cancellationToken)
    {
        var context = await RunAgentAsync("ContextAgent", AgentPrompts.ContextInstructions, message, cancellationToken);
        var json = await RunAgentAsync("DiscoveryAgent", AgentPrompts.DiscoveryInstructions, AgentPrompts.Discovery(context), cancellationToken);
        return AiResponseParser.Parse<DiscoveryResponse>(json, "Discovery Agent");
    }

    public async Task<MissionPlan> PlanAsync(
        IReadOnlyList<SuggestedTask> tasks,
        IReadOnlyList<SupplyItem> supplies,
        string deadline,
        string? situation,
        CancellationToken cancellationToken)
    {
        var approvedData = AiResponseParser.Serialize(new { tasks, supplies, deadline });
        var isRecovery = situation is not null;
        var generated = await RunAgentAsync(
            isRecovery ? "RecoveryAgent" : "PlanAgent",
            isRecovery ? AgentPrompts.RecoveryInstructions : AgentPrompts.PlanInstructions,
            AgentPrompts.Plan(approvedData, situation),
            cancellationToken);
        return AiResponseParser.Parse<MissionPlan>(generated, isRecovery ? "Recovery Agent" : "Plan Agent");
    }

    private async Task<string> RunAgentAsync(string name, string instructions, string prompt, CancellationToken cancellationToken)
    {
        var chatClient = new CopilotChatClient(model, loggerFactory.CreateLogger<CopilotChatClient>());
        var agent = new ChatClientAgent(
            chatClient,
            new ChatClientAgentOptions
            {
                Name = name,
                Description = instructions,
            },
            loggerFactory,
            null);
        var session = await agent.CreateSessionAsync(cancellationToken);
        var response = await agent.RunAsync(
            instructions + Environment.NewLine + prompt,
            session,
            new ChatClientAgentRunOptions(new ChatOptions()),
            cancellationToken);
        logger.LogInformation("Agent Framework completed {AgentName}", name);
        return string.IsNullOrWhiteSpace(response.Text)
            ? throw new InvalidOperationException($"{name}가 빈 응답을 반환했습니다.")
            : response.Text;
    }
}
