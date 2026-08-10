using Microsoft.EntityFrameworkCore;
using MoodNewsGrid.Core.Interfaces;
using MoodNewsGrid.Core.Models;
using MoodNewsGrid.Infrastructure.Data;

namespace MoodNewsGrid.Infrastructure.Services;

public class NewsService(
    AppDbContext db,
    INewsFetcher fetcher,
    IMoodRewriter rewriter,
    IFactChecker factChecker) : INewsService
{
    public async Task<int> RefreshNewsAsync(CancellationToken ct = default)
    {
        var fetched = await fetcher.FetchLatestAsync(ct);

        var existingUrls = await db.NewsItems
            .Select(n => n.SourceUrl)
            .ToListAsync(ct);

        var newOnes = fetched
            .Where(n => !existingUrls.Contains(n.SourceUrl))
            .ToList();

        if (newOnes.Count > 0)
        {
            foreach (var item in newOnes)
            {
                item.PublishedAt = item.PublishedAt.ToUniversalTime();
                item.FetchedAt = item.FetchedAt.ToUniversalTime();
            }

            db.NewsItems.AddRange(newOnes);
            await db.SaveChangesAsync(ct);
        }

        return newOnes.Count;
    }

    public async Task<IReadOnlyList<NewsItem>> GetAllAsync(
        CancellationToken ct = default)
    {
        return await db.NewsItems
            .Include(n => n.Rewrites)
            .OrderByDescending(n => n.PublishedAt)
            .ToListAsync(ct);
    }

    public Task<NewsItem?> GetByIdAsync(
        Guid id,
        CancellationToken ct = default)
    {
        return db.NewsItems
            .Include(n => n.Rewrites)
            .FirstOrDefaultAsync(n => n.Id == id, ct);
    }

    public async Task<NewsRewrite> GetOrCreateRewriteAsync(
        Guid newsItemId,
        Mood mood,
        CancellationToken ct = default)
    {
        var existing = await db.NewsRewrites
            .FirstOrDefaultAsync(
                r => r.NewsItemId == newsItemId && r.Mood == mood,
                ct);

        if (existing is not null)
            return existing;

        var newsItem = await db.NewsItems
            .FirstOrDefaultAsync(n => n.Id == newsItemId, ct)
            ?? throw new InvalidOperationException("News item not found");

        string rewrittenText;

        // Для нейтрального настроения используем оригинал.
        if (mood == Mood.Neutral)
        {
            rewrittenText = newsItem.OriginalText;
        }
        else
        {
            rewrittenText = await rewriter.RewriteAsync(
                newsItem.OriginalText,
                mood,
                ct);
        }

        var check = factChecker.Check(
            newsItem.OriginalText,
            rewrittenText);

        var rewrite = new NewsRewrite
        {
            NewsItemId = newsItemId,
            Mood = mood,
            RewrittenText = rewrittenText,
            FactsCheckPassed = check.Passed,
            FactsCheckIssues = check.MissingFacts.Count > 0
                ? string.Join("; ", check.MissingFacts)
                : null,
            CreatedAt = DateTimeOffset.UtcNow
        };

        db.NewsRewrites.Add(rewrite);

        await db.SaveChangesAsync(ct);

        return rewrite;
    }
}
