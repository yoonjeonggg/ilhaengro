using System.Text.Json;

namespace Ilhaengro.Api.Services;

internal static class AiResponseParser
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, JsonOptions);

    public static T Parse<T>(string response, string agentName)
    {
        try
        {
            return JsonSerializer.Deserialize<T>(ExtractJson(response), JsonOptions)
                ?? throw new InvalidOperationException($"{agentName}가 빈 JSON을 반환했습니다.");
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException($"{agentName} 응답 형식이 올바르지 않습니다.", exception);
        }
    }

    private static string ExtractJson(string response)
    {
        var start = response.IndexOf('{');
        var end = response.LastIndexOf('}');
        if (start < 0 || end <= start)
        {
            throw new InvalidOperationException("AI 응답에서 JSON 객체를 찾지 못했습니다.");
        }

        return response[start..(end + 1)];
    }
}
