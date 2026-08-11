namespace MoodNewsGrid.Core.Interfaces;

/// <summary>
/// Выбрасывается, когда LLM-провайдер (OpenRouter) вернул 429 Too Many Requests —
/// исчерпан лимит запросов (rate limit) или бесплатная квота.
/// </summary>
public class LlmRateLimitException(string message, TimeSpan? retryAfter = null)
    : Exception(message)
{
    /// <summary>Через сколько рекомендуется повторить запрос, если провайдер прислал Retry-After.</summary>
    public TimeSpan? RetryAfter { get; } = retryAfter;
}
