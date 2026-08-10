namespace MoodNewsGrid.Core.Models;

public class NewsRewrite
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid NewsItemId { get; set; }
    public NewsItem NewsItem { get; set; } = null!;

    public Mood Mood { get; set; }

    public string RewrittenText { get; set; } = string.Empty;

    /// <summary>Прошла ли проверка сохранения фактов (числа/даты/имена/места из оригинала присутствуют в тексте).</summary>
    public bool FactsCheckPassed { get; set; }

    /// <summary>Какие именно факт-токены не нашлись в переписанном тексте (для отладки/README-прозрачности).</summary>
    public string? FactsCheckIssues { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
