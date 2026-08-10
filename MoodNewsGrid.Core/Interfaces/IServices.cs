using MoodNewsGrid.Core.Models;

namespace MoodNewsGrid.Core.Interfaces;

/// <summary>Получает новости из внешнего RSS-источника (без сохранения тона, факты как есть).</summary>
public interface INewsFetcher
{
    Task<IReadOnlyList<NewsItem>> FetchLatestAsync(CancellationToken ct = default);
}

/// <summary>Переписывает текст новости под заданное настроение через LLM.</summary>
public interface IMoodRewriter
{
    Task<string> RewriteAsync(string originalText, Mood mood, CancellationToken ct = default);
}

/// <summary>Проверяет, что ключевые факты (числа, даты, имена, места, цитаты) из оригинала сохранились в переписанном тексте.</summary>
public interface IFactChecker
{
    FactCheckResult Check(string originalText, string rewrittenText);
}

public record FactCheckResult(bool Passed, IReadOnlyList<string> MissingFacts);

/// <summary>Оркестрирует: fetch -> save -> (по запросу) rewrite+check -> cache.</summary>
public interface INewsService
{
    Task<int> RefreshNewsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<NewsItem>> GetAllAsync(CancellationToken ct = default);
    Task<NewsItem?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<NewsRewrite> GetOrCreateRewriteAsync(Guid newsItemId, Mood mood, CancellationToken ct = default);
}
