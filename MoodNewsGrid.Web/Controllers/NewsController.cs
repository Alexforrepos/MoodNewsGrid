using Microsoft.AspNetCore.Mvc;
using MoodNewsGrid.Core.Interfaces;
using MoodNewsGrid.Core.Models;

namespace MoodNewsGrid.Web.Controllers;

public class NewsController(INewsService newsService) : Controller
{
    // GET /News?mood=Joyful
    public async Task<IActionResult> Index(
        Mood mood = Mood.Neutral,
        CancellationToken ct = default)
    {
        var news = await newsService.GetAllAsync(ct);

        ViewBag.SelectedMood = mood;
        ViewBag.AllMoods = Enum.GetValues<Mood>();

        // Для каждой новости получаем рерайт выбранного настроения.
        // Если его ещё нет в БД — он будет создан через LLM.
        var rewrites = new Dictionary<Guid, NewsRewrite?>();
        var rateLimited = false;

        foreach (var item in news)
        {
            // Как только один раз словили 429 — дальше по списку LLM не дёргаем,
            // чтобы не усугублять ситуацию. Для оставшихся новостей просто нет рерайта,
            // вьюха покажет оригинальный текст с пометкой.
            if (rateLimited)
            {
                rewrites[item.Id] = null;
                continue;
            }

            try
            {
                rewrites[item.Id] = await newsService.GetOrCreateRewriteAsync(item.Id, mood, ct);
            }
            catch (LlmRateLimitException)
            {
                rateLimited = true;
                rewrites[item.Id] = null;
            }
        }

        ViewBag.Rewrites = rewrites;
        ViewBag.RateLimited = rateLimited;

        return View(news);
    }

    // GET /News/Details/{id}?mood=Ironic
    public async Task<IActionResult> Details(
        Guid id,
        Mood mood = Mood.Neutral,
        CancellationToken ct = default)
    {
        var item = await newsService.GetByIdAsync(id, ct);

        if (item is null)
            return NotFound();

        ViewBag.SelectedMood = mood;
        ViewBag.AllMoods = Enum.GetValues<Mood>();

        try
        {
            ViewBag.Rewrite = await newsService.GetOrCreateRewriteAsync(id, mood, ct);
            ViewBag.RateLimited = false;
        }
        catch (LlmRateLimitException ex)
        {
            // Показываем оригинал вместо переписанного текста и понятное предупреждение,
            // вместо падения страницы в 500-ю ошибку.
            ViewBag.Rewrite = null;
            ViewBag.RateLimited = true;
            ViewBag.RetryAfterSeconds = ex.RetryAfter?.TotalSeconds;
        }

        return View(item);
    }

    // POST /News/Refresh
    [HttpPost]
    public async Task<IActionResult> Refresh(CancellationToken ct)
    {
        await newsService.RefreshNewsAsync(ct);

        return RedirectToAction(nameof(Index));
    }
}
