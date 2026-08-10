using Microsoft.EntityFrameworkCore;
using MoodNewsGrid.Core.Interfaces;
using MoodNewsGrid.Infrastructure.Data;
using MoodNewsGrid.Infrastructure.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

// Публичные RSS-источники реальных новостей.
// Можно дополнить/заменить любыми другими открытыми RSS без изменения кода.
builder.Services.AddSingleton<IEnumerable<RssFeedSource>>(_ => new List<RssFeedSource>
{
    new() { Name = "Lenta.ru", Url = "https://lenta.ru/rss/news" },
    new() { Name = "РИА Новости", Url = "https://ria.ru/export/rss2/archive/index.xml" },
    new() { Name = "Habr", Url = "https://habr.com/ru/rss/all/all/" }
});

builder.Services.AddHttpClient<INewsFetcher, RssNewsFetcher>();

builder.Services.Configure<LlmOptions>(builder.Configuration.GetSection("Llm"));
builder.Services.AddSingleton(sp => sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<LlmOptions>>().Value);
builder.Services.AddHttpClient<IMoodRewriter, LlmMoodRewriter>();

builder.Services.AddScoped<IFactChecker, RegexFactChecker>();
builder.Services.AddScoped<INewsService, NewsService>();

var app = builder.Build();

// Применяем миграции автоматически при старте — удобно для тестового задания и docker compose up.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}

app.UseStaticFiles();
app.UseRouting();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=News}/{action=Index}/{id?}");

app.Run();
