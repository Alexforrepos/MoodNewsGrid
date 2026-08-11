using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using MoodNewsGrid.Core.Interfaces;
using MoodNewsGrid.Core.Models;

namespace MoodNewsGrid.Infrastructure.Services;

public class LlmOptions
{
    public string ApiKey { get; set; } = string.Empty;

    // OpenRouter OpenAI-compatible API
    public string BaseUrl { get; set; } = "https://openrouter.ai/api/v1";

    public string Model { get; set; } = "inclusionai/ling-3.0-tiny:free";
}

/// <summary>
/// Переписывает новости под заданное настроение
/// через OpenRouter.
/// </summary>
public class LlmMoodRewriter(
    HttpClient httpClient,
    LlmOptions options) : IMoodRewriter
{
    public async Task<string> RewriteAsync(
        string title,
        string? originalText,
        Mood mood,
        CancellationToken ct = default)
    {
        var moodInstruction = mood switch
        {
            Mood.Joyful =>
                "радостно, позитивно и с оптимизмом, но без изменения фактов",

            Mood.Sad =>
                "грустно и меланхолично, подчёркивая печальную сторону события, но без изменения фактов",

            Mood.Ironic =>
                "иронично, с лёгким сарказмом, но без изменения фактов",

            Mood.Dramatic =>
                "драматично и напряжённо, подчёркивая важность события, но без изменения фактов",

            _ =>
                "нейтрально и сухо, в обычном новостном стиле"
        };

        var systemPrompt = """
            Ты переписываешь новостные тексты согласно указанному настроению.
            newsItem.Title,
            newsItem.OriginalText,
            mood,
            ct);
            Верни только готовый переписанный текст.
            Не объясняй свои действия.
            Не показывай рассуждения.
            Не предлагай несколько вариантов.

            Очень важно:
            - сохраняй все факты исходной новости;
            - не добавляй новые факты;
            - не придумывай цитаты;
            - не придумывай имена;
            - не придумывай даты;
            - не придумывай числа;
            - не придумывай названия организаций;
            - не придумывай географические названия;
            - не меняй смысл события;
            - не утверждай то, чего нет в исходных данных.

            У новости есть название и, возможно, основной текст.

            Если основной текст отсутствует или пустой:
            - работай только с названием;
            - не выдумывай подробности;
            - сохрани информацию из названия;
            - верни только переработанное название.

            Если основной текст присутствует:
            - используй название и основной текст вместе;
            - сохрани факты из обоих;
            - можешь изменить формулировки и структуру;
            - не добавляй информацию, которой нет в исходных данных.
            """;

        var userPrompt =
            $"""
            Требуемая тональность:
            {moodInstruction}

            Название новости:
            {title}

            Основной текст новости:
            {(string.IsNullOrWhiteSpace(originalText)
                ? "(текст отсутствует)"
                : originalText)}

            Перепиши новость в требуемой тональности.
            """;

        var requestBody = new
        {
            model = options.Model,

            messages = new object[]
            {
                new
                {
                    role = "system",
                    content = systemPrompt
                },
                new
                {
                    role = "user",
                    content = userPrompt
                }
            },

            temperature = 0.5,
            max_tokens = 1500
        };

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"{options.BaseUrl.TrimEnd('/')}/chat/completions");

        request.Content = new StringContent(
            JsonSerializer.Serialize(requestBody),
            Encoding.UTF8,
            "application/json");

        request.Headers.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                options.ApiKey);

        request.Headers.TryAddWithoutValidation(
            "HTTP-Referer",
            "http://localhost");

        request.Headers.TryAddWithoutValidation(
            "X-Title",
            "MoodNewsGrid");

        using var response = await httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            ct);

        var json = await response.Content.ReadAsStringAsync(ct);

        if (response.StatusCode ==
            System.Net.HttpStatusCode.TooManyRequests)
        {
            TimeSpan? retryAfter =
                response.Headers.RetryAfter?.Delta;

            throw new LlmRateLimitException(
                $"OpenRouter вернул 429 Too Many Requests — " +
                $"превышен лимит запросов к модели {options.Model}. " +
                $"Ответ: {json}",
                retryAfter);
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"OpenRouter API error {(int)response.StatusCode} " +
                $"({response.StatusCode}): {json}");
        }

        Console.WriteLine("=== OPENROUTER RESPONSE ===");
        Console.WriteLine(json);
        Console.WriteLine("===========================");

        using var doc = JsonDocument.Parse(json);

        if (!doc.RootElement.TryGetProperty(
                "choices",
                out var choices) ||
            choices.GetArrayLength() == 0)
        {
            throw new InvalidOperationException(
                $"OpenRouter returned no choices. Response: {json}");
        }

        var message = choices[0].GetProperty("message");

        if (!message.TryGetProperty(
                "content",
                out var content))
        {
            throw new InvalidOperationException(
                $"OpenRouter response does not contain " +
                $"message.content. Response: {json}");
        }

        var text = content.GetString();

        if (string.IsNullOrWhiteSpace(text))
        {
            var finishReason =
                choices[0].TryGetProperty(
                    "finish_reason",
                    out var finishReasonElement)
                    ? finishReasonElement.GetString()
                    : null;

            var nativeFinishReason =
                choices[0].TryGetProperty(
                    "native_finish_reason",
                    out var nativeFinishReasonElement)
                    ? nativeFinishReasonElement.GetString()
                    : null;

            Console.WriteLine(
                $"OpenRouter returned no final content. " +
                $"FinishReason={finishReason}, " +
                $"NativeFinishReason={nativeFinishReason}");

            throw new InvalidOperationException(
                $"OpenRouter returned no final content. " +
                $"FinishReason={finishReason}, " +
                $"NativeFinishReason={nativeFinishReason}");
        }

        return text.Trim();
    }

}
