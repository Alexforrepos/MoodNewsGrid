namespace MoodNewsGrid.Core.Models;

public class NewsItem
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Title { get; set; } = string.Empty;

    /// <summary>Оригинальный текст/summary новости, как пришёл из источника. Не редактируется.</summary>
    public string OriginalText { get; set; } = string.Empty;

    public string SourceUrl { get; set; } = string.Empty;

    public string SourceName { get; set; } = string.Empty;

    public DateTimeOffset PublishedAt { get; set; }

    public DateTimeOffset FetchedAt { get; set; } = DateTimeOffset.UtcNow;

    public List<NewsRewrite> Rewrites { get; set; } = new();
}
