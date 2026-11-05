using GitHub.Copilot;
using Microsoft.Extensions.AI;

namespace Ilhaengro.Api.Services;

public sealed class CopilotChatClient(string model, ILogger<CopilotChatClient> logger) : IChatClient
{
    public void Dispose()
    {
    }

    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var response = await CompleteAsync(messages, cancellationToken);
        return new ChatResponse(new ChatMessage(ChatRole.Assistant, response));
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var response = await CompleteAsync(messages, cancellationToken);
        yield return new ChatResponseUpdate(ChatRole.Assistant, response);
    }

    public object? GetService(Type serviceType, object? serviceKey = null)
    {
        return serviceType == typeof(IChatClient) ? this : null;
    }

    private async Task<string> CompleteAsync(IEnumerable<ChatMessage> messages, CancellationToken cancellationToken)
    {
        await using var client = new CopilotClient();
        await client.StartAsync();
        await using var session = await client.CreateSessionAsync(new SessionConfig
        {
            Model = model,
            OnPermissionRequest = PermissionHandler.ApproveAll,
        }, cancellationToken);

        var output = new List<string>();
        var idle = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        session.On<SessionEvent>(eventData =>
        {
            if (eventData is AssistantMessageEvent assistantMessage)
            {
                output.Add(assistantMessage.Data.Content);
            }
            else if (eventData is SessionIdleEvent)
            {
                idle.TrySetResult();
            }
        });

        var prompt = string.Join(
            Environment.NewLine,
            messages.Select(message => $"{message.Role.Value}: {message.Text}"));
        await session.SendAsync(new MessageOptions { Prompt = prompt }, cancellationToken);
        await idle.Task.WaitAsync(TimeSpan.FromSeconds(60), cancellationToken);

        var response = string.Join(Environment.NewLine, output);
        if (string.IsNullOrWhiteSpace(response))
        {
            throw new InvalidOperationException("Copilot SDK가 빈 응답을 반환했습니다.");
        }

        logger.LogInformation("Copilot chat response completed using model {Model}", model);
        return response;
    }
}
