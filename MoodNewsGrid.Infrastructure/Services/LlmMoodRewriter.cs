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

    // GLM-4.5-Air через OpenRouter
    public string Model { get; set; } = "z-ai/glm-4.5-air";
}

/// <summary>
/// Переписывает новости под заданное настроение
/// через OpenRouter + GLM-4.5-Air.
/// </summary>
public class LlmMoodRewriter(
    HttpClient httpClient,
    LlmOptions options) : IMoodRewriter
{
    public async Task<string> RewriteAsync(
        string originalText,
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
            Ты — профессиональный редактор новостей.

            Тебе передаётся оригинальный текст новости.
            Твоя задача — переписать его в указанной эмоциональной тональности.

            КРИТИЧЕСКИ ВАЖНО:

            1. Не придумывай никаких новых фактов.
            2. Не удаляй важные факты из оригинала.
            3. Не меняй имена людей.
            4. Не меняй даты.
            5. Не меняй числа.
            6. Не меняй названия организаций.
            7. Не меняй географические названия.
            8. Не меняй смысл событий.
            9. Не выдумывай цитаты.
            10. Не добавляй информацию, которой нет в оригинале.

            Можно менять только:
            - стиль;
            - эмоциональную окраску;
            - порядок предложений;
            - формулировки;
            - ритм текста.

            Верни ТОЛЬКО готовый текст новости.
            Не добавляй пояснений.
            Не добавляй заголовок.
            Не добавляй кавычки вокруг всего текста.
            Не пиши "Вот переписанный текст:".
            """;

        var userPrompt =
            $"""
            Требуемая тональность:
            {moodInstruction}

            Оригинальный текст новости:

            {originalText}
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

            // Для новостей лучше держать температуру ниже,
            // чтобы модель меньше фантазировала.
            temperature = 0.5,

            // Не позволяем модели генерировать бесконечно длинный текст.
            max_tokens = 2000
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

        // Не обязательны, но рекомендуются OpenRouter.
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

        // Очень важно:
        // вместо безымянного EnsureSuccessStatusCode()
        // показываем реальную ошибку OpenRouter.
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"OpenRouter API error {(int)response.StatusCode} " +
                $"({response.StatusCode}): {json}");
        }

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
                $"OpenRouter response does not contain message.content. " +
                $"Response: {json}");
        }

        var text = content.GetString();

        if (string.IsNullOrWhiteSpace(text))
        {
            throw new InvalidOperationException(
                "OpenRouter returned an empty response.");
        }

        return text.Trim();
    }
}
