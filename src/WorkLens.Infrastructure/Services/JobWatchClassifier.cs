using System.Globalization;
using System.Text.Json;
using WorkLens.Core.Entities;

namespace WorkLens.Infrastructure.Services;

internal static class JobWatchClassifier
{
    public static void ApplyTags(JobListing job)
    {
        if (!job.IsRemote || !LooksLikeTargetIcRole(job.Title))
            return;

        var tags = ReadTags(job.TagsJson);
        Add(tags, "Career Watch");

        var salaryCeiling = ParseSalary(job.SalaryMax) ?? ParseSalary(job.SalaryMin);
        if (salaryCeiling >= 160_000)
            Add(tags, "$160k+ watch");
        else if (salaryCeiling >= 140_000)
            Add(tags, "$140k+ watch");

        job.TagsJson = JsonSerializer.Serialize(tags);
    }

    private static bool LooksLikeTargetIcRole(string? title)
    {
        if (string.IsNullOrWhiteSpace(title)) return false;
        var t = title.ToLowerInvariant();

        if (t.Contains("manager") || t.Contains("director") || t.Contains("vice president") ||
            t.StartsWith("vp ", StringComparison.Ordinal) || t.Contains("head of"))
            return false;

        return t.Contains("software engineer") ||
               t.Contains("software developer") ||
               t.Contains("full stack") ||
               t.Contains("full-stack") ||
               t.Contains(".net") ||
               t.Contains("c#") ||
               t.Contains("backend engineer") ||
               t.Contains("back-end engineer") ||
               t.Contains("platform engineer") ||
               t.Contains("application engineer") ||
               t.Contains("software architect") ||
               t.Contains("solution architect") ||
               t.Contains("solutions architect") ||
               t.Contains("application architect") ||
               t.Contains("technical lead") ||
               t.Contains("tech lead") ||
               t.Contains("lead engineer") ||
               t.Contains("principal engineer") ||
               t.Contains("staff engineer");
    }

    private static List<string> ReadTags(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new List<string>();
        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
        }
        catch (JsonException)
        {
            return new List<string>();
        }
    }

    private static void Add(List<string> tags, string value)
    {
        if (!tags.Contains(value, StringComparer.OrdinalIgnoreCase))
            tags.Add(value);
    }

    private static decimal? ParseSalary(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var digits = new string(value.Where(c => char.IsDigit(c) || c == '.').ToArray());
        return decimal.TryParse(digits, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }
}
