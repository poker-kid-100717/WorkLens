namespace WorkLens.Core.Entities;

/// <summary>
/// A saved keyword/filter profile used to query the external feeds.
/// Lets the user maintain multiple named searches (e.g. ".NET Remote", "Azure Contract")
/// without re-typing filters. The active profile(s) drive the background refresh job.
/// </summary>
public class SearchProfile
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;

    /// <summary>JSON string array of keywords, OR'd together against title/tags/description.</summary>
    public string KeywordsJson { get; set; } = "[]";

    public bool RemoteOnly { get; set; }
    public string? LocationFilter { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
}
