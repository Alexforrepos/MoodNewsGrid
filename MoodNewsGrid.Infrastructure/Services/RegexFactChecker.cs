using System.Text.RegularExpressions;
using MoodNewsGrid.Core.Interfaces;

namespace MoodNewsGrid.Infrastructure.Services;

/// <summary>
/// Эвристическая проверка сохранения фактов без LLM (быстро, детерминированно, дёшево).
/// Извлекает из оригинала "факт-токены" — числа, даты, слова с заглавной буквы (имена/места),
/// куски в кавычках (цитаты) — и проверяет, что каждый из них присутствует в переписанном тексте.
/// Это не 100% гарантия, а страховочная сетка: если LLM что-то "потерял" или заменил число,
/// проверка это подсветит и новость будет помечена как требующую ручной проверки.
/// </summary>
public partial class RegexFactChecker : IFactChecker
{
    [GeneratedRegex(@"\d[\d\s.,:%-]*\d|\d+")]
    private static partial Regex NumberRegex();

    [GeneratedRegex(@"[«""]([^»""]{3,120})[»""]")]
    private static partial Regex QuoteRegex();

    // Слово с заглавной буквы длиной от 3 символов, не в начале предложения (эвристика имени/места).
    // Полностью надёжно это сделать регэкспом нельзя — для продакшена стоило бы NER,
    // но для тестового задания этого достаточно и прозрачно объяснимо в README.
    [GeneratedRegex(@"(?<!^)(?<![.!?]\s)\b[А-ЯЁA-Z][а-яёa-z]{2,}\b")]
    private static partial Regex ProperNounRegex();

    public FactCheckResult Check(string originalText, string rewrittenText)
    {
        var missing = new List<string>();

        var numbers = NumberRegex().Matches(originalText).Select(m => m.Value.Trim()).Distinct();
        foreach (var number in numbers)
        {
            if (!rewrittenText.Contains(number, StringComparison.Ordinal))
                missing.Add($"число: {number}");
        }

        var quotes = QuoteRegex().Matches(originalText).Select(m => m.Groups[1].Value.Trim()).Distinct();
        foreach (var quote in quotes)
        {
            if (!rewrittenText.Contains(quote, StringComparison.OrdinalIgnoreCase))
                missing.Add($"цитата: «{quote}»");
        }

        var properNouns = ProperNounRegex().Matches(originalText)
            .Select(m => m.Value)
            .Distinct()
            .Where(w => w.Length >= 3);
        foreach (var noun in properNouns)
        {
            if (!rewrittenText.Contains(noun, StringComparison.OrdinalIgnoreCase))
                missing.Add($"имя/место: {noun}");
        }

        return new FactCheckResult(missing.Count == 0, missing);
    }
}
