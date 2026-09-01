namespace WorkLens.Core.Entities;

/// <summary>
/// The user's resume, stored as plain text (extracted client-side from a PDF via
/// PDF.js, or pasted directly) so the backend never needs a PDF-parsing dependency.
/// Single-row-ish usage: a self-hosted, single-user app, so the latest uploaded
/// resume is what match scoring uses. Keeping it as its own table (rather than a
/// config value) leaves room for multiple named resumes later without a schema change.
/// </summary>
public class Resume
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string RawText { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTimeOffset UploadedAt { get; set; }
}
