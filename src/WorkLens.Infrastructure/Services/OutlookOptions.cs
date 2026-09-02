namespace WorkLens.Infrastructure.Services;

public class OutlookOptions
{
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string Tenant { get; set; } = "common";
    public string RedirectUri { get; set; } = "http://localhost:5080/api/outlook/callback";
    public string FrontendRedirectUri { get; set; } = "http://localhost:8080/communications";
    public string StorePath { get; set; } = "/app/data/outlook-state.json";
    public int SyncIntervalMinutes { get; set; } = 5;
    public int LookbackDays { get; set; } = 90;
}
