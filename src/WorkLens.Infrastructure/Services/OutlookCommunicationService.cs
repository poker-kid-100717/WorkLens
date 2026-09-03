using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using System.Text.Json;
using System.Text.Json.Serialization;
using WorkLens.Core.Entities;
using WorkLens.Core.Enums;
using WorkLens.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace WorkLens.Infrastructure.Services;

public sealed class OutlookCommunicationService
{
    private static readonly string[] JobSignals =
    {
        "application", "applied", "recruiter", "talent", "interview", "phone screen",
        "technical screen", "hiring", "candidate", "position", "role", "offer", "rejected",
        "thank you for applying", "next steps", "assessment", "coding challenge"
    };

    private static readonly HashSet<string> GenericMailDomains = new(StringComparer.OrdinalIgnoreCase)
    {
        "greenhouse.io", "lever.co", "workday.com", "myworkdayjobs.com", "smartrecruiters.com",
        "ashbyhq.com", "icims.com", "jobvite.com", "indeed.com", "linkedin.com"
    };

    private static readonly SemaphoreSlim StoreLock = new(1, 1);
    private static readonly SemaphoreSlim SyncLock = new(1, 1);

    private readonly HttpClient _http;
    private readonly IJobApplicationRepository _applications;
    private readonly OutlookOptions _options;
    private readonly ILogger<OutlookCommunicationService> _logger;

    public OutlookCommunicationService(
        HttpClient http,
        IJobApplicationRepository applications,
        IOptions<OutlookOptions> options,
        ILogger<OutlookCommunicationService> logger)
    {
        _http = http;
        _applications = applications;
        _options = options.Value;
        _logger = logger;
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_options.ClientId);

    public async Task<OutlookConnectionStatus> GetStatusAsync(CancellationToken ct)
    {
        var state = await ReadStateAsync(ct);
        return new OutlookConnectionStatus(
            IsConfigured,
            !string.IsNullOrWhiteSpace(state.RefreshToken) ||
            (!string.IsNullOrWhiteSpace(state.AccessToken) && state.ExpiresAt > DateTimeOffset.UtcNow),
            state.AccountEmail,
            state.LastSyncedAt,
            state.Communications.Count);
    }

    public async Task<string> BeginAuthorizationAsync(CancellationToken ct)
    {
        if (!IsConfigured)
            throw new InvalidOperationException("Outlook integration is not configured.");

        var authState = Guid.NewGuid().ToString("N");
        var state = await ReadStateAsync(ct);
        state.PendingAuthState = authState;
        state.PendingAuthStateExpiresAt = DateTimeOffset.UtcNow.AddMinutes(10);
        await WriteStateAsync(state, ct);
        return BuildAuthorizeUrl(authState);
    }

    private string BuildAuthorizeUrl(string state)
    {
        var scopes = Uri.EscapeDataString("openid profile offline_access User.Read Mail.Read");
        return $"https://login.microsoftonline.com/{Uri.EscapeDataString(_options.Tenant)}/oauth2/v2.0/authorize" +
               $"?client_id={Uri.EscapeDataString(_options.ClientId)}" +
               "&response_type=code" +
               $"&redirect_uri={Uri.EscapeDataString(_options.RedirectUri)}" +
               "&response_mode=query" +
               $"&scope={scopes}" +
               $"&state={Uri.EscapeDataString(state)}";
    }

    public async Task ConnectAsync(string code, string returnedState, CancellationToken ct)
    {
        if (!IsConfigured)
            throw new InvalidOperationException("Outlook integration is not configured.");

        var state = await ReadStateAsync(ct);
        if (string.IsNullOrWhiteSpace(returnedState) ||
            string.IsNullOrWhiteSpace(state.PendingAuthState) ||
            state.PendingAuthStateExpiresAt <= DateTimeOffset.UtcNow ||
            !CryptographicEquals(state.PendingAuthState, returnedState))
        {
            throw new InvalidOperationException("Outlook sign-in state was invalid or expired. Start the connection again.");
        }

        var form = new Dictionary<string, string>
        {
            ["client_id"] = _options.ClientId,
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = _options.RedirectUri,
            ["scope"] = "openid profile offline_access User.Read Mail.Read"
        };
        if (!string.IsNullOrWhiteSpace(_options.ClientSecret))
            form["client_secret"] = _options.ClientSecret;

        using var response = await _http.PostAsync(
            $"https://login.microsoftonline.com/{Uri.EscapeDataString(_options.Tenant)}/oauth2/v2.0/token",
            new FormUrlEncodedContent(form), ct);
        response.EnsureSuccessStatusCode();

        var token = await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken: ct)
            ?? throw new InvalidOperationException("Microsoft returned an empty token response.");

        state.AccessToken = token.AccessToken;
        state.RefreshToken = string.IsNullOrWhiteSpace(token.RefreshToken) ? state.RefreshToken : token.RefreshToken;
        state.ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(Math.Max(60, token.ExpiresIn - 60));
        state.PendingAuthState = null;
        state.PendingAuthStateExpiresAt = null;
        await PopulateAccountAsync(state, ct);
        await WriteStateAsync(state, ct);
    }

    public async Task DisconnectAsync(bool clearCommunications, CancellationToken ct)
    {
        var state = await ReadStateAsync(ct);
        state.AccessToken = null;
        state.RefreshToken = null;
        state.ExpiresAt = default;
        state.PendingAuthState = null;
        state.PendingAuthStateExpiresAt = null;
        state.AccountEmail = null;
        state.LastSyncedAt = null;
        if (clearCommunications)
            state.Communications.Clear();
        await WriteStateAsync(state, ct);
    }

    public async Task<IReadOnlyList<OutlookCommunication>> GetCommunicationsAsync(int? applicationId, CancellationToken ct)
    {
        var state = await ReadStateAsync(ct);
        var query = state.Communications.AsEnumerable();
        if (applicationId.HasValue)
            query = query.Where(x => x.ApplicationId == applicationId.Value);
        return query.OrderByDescending(x => x.ReceivedAt).ToList();
    }

    public async Task<OutlookCommunication> MatchCommunicationAsync(string messageId, int? applicationId, CancellationToken ct)
    {
        var state = await ReadStateAsync(ct);
        var communication = state.Communications.FirstOrDefault(x => x.MessageId == messageId)
            ?? throw new KeyNotFoundException("Outlook communication was not found.");

        if (applicationId.HasValue)
        {
            var application = await _applications.GetByIdAsync(applicationId.Value, ct)
                ?? throw new KeyNotFoundException("Application was not found.");
            communication.ApplicationId = application.Id;
            communication.MatchedCompany = application.Company;
            communication.MatchedTitle = application.Title;
        }
        else
        {
            communication.ApplicationId = null;
            communication.MatchedCompany = null;
            communication.MatchedTitle = null;
        }

        await WriteStateAsync(state, ct);
        return communication;
    }

    public async Task<JobApplication> TrackCommunicationAsApplicationAsync(
        string messageId,
        string title,
        string company,
        string? location,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(company))
            throw new ArgumentException("Title and company are required.");

        var state = await ReadStateAsync(ct);
        var communication = state.Communications.FirstOrDefault(x => x.MessageId == messageId)
            ?? throw new KeyNotFoundException("Outlook communication was not found.");

        if (communication.ApplicationId.HasValue)
        {
            var existing = await _applications.GetByIdAsync(communication.ApplicationId.Value, ct);
            if (existing is not null)
                return existing;
        }

        var eventTime = communication.ReceivedAt == default ? DateTimeOffset.UtcNow : communication.ReceivedAt;
        var application = new JobApplication
        {
            ManualEntry = true,
            Title = title.Trim(),
            Company = company.Trim(),
            Location = string.IsNullOrWhiteSpace(location) ? null : location.Trim(),
            Status = ApplicationStatus.Applied,
            SavedAt = eventTime,
            AppliedAt = eventTime,
            LastStatusChangeAt = eventTime,
            ContactName = communication.FromName,
            ContactEmail = communication.FromEmail,
            Notes = $"Created from Outlook communication: {communication.Subject}"
        };
        application.StatusHistory.Add(new ApplicationStatusHistory
        {
            FromStatus = ApplicationStatus.Saved,
            ToStatus = ApplicationStatus.Applied,
            ChangedAt = eventTime,
            Note = "Imported from Outlook communication"
        });

        await _applications.AddAsync(application, ct);
        await _applications.SaveChangesAsync(ct);

        communication.ApplicationId = application.Id;
        communication.MatchedCompany = application.Company;
        communication.MatchedTitle = application.Title;
        await WriteStateAsync(state, ct);
        return application;
    }

    public async Task<int> SyncAsync(CancellationToken ct)
    {
        await SyncLock.WaitAsync(ct);
        try
        {
            var state = await ReadStateAsync(ct);
            await EnsureAccessTokenAsync(state, ct);
            if (string.IsNullOrWhiteSpace(state.AccessToken))
                throw new InvalidOperationException("Outlook is not connected.");

            var applications = await _applications.GetAllAsync(ct);
            var since = DateTimeOffset.UtcNow.AddDays(-Math.Max(1, _options.LookbackDays));
            var filter = Uri.EscapeDataString($"receivedDateTime ge {since.UtcDateTime:O}");
            var url = "https://graph.microsoft.com/v1.0/me/messages" +
                      $"?$top=100&$orderby=receivedDateTime%20desc&$filter={filter}" +
                      "&$select=id,subject,from,receivedDateTime,sentDateTime,bodyPreview,webLink,isRead";

            var added = 0;
            for (var page = 0; page < 10 && !string.IsNullOrWhiteSpace(url); page++)
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", state.AccessToken);
                using var response = await _http.SendAsync(request, ct);
                response.EnsureSuccessStatusCode();

                var payload = await response.Content.ReadFromJsonAsync<GraphMessagesResponse>(cancellationToken: ct);
                if (payload?.Value is null)
                    break;

                foreach (var message in payload.Value)
                {
                    if (string.IsNullOrWhiteSpace(message.Id) || state.Communications.Any(x => x.MessageId == message.Id))
                        continue;

                    var fromAddress = message.From?.EmailAddress?.Address ?? string.Empty;
                    var fromName = message.From?.EmailAddress?.Name ?? string.Empty;
                    var text = string.Join(' ', message.Subject, message.BodyPreview, fromName, fromAddress);
                    var match = MatchApplication(text, fromAddress, applications);

                    if (match is null && !LooksJobRelated(text))
                        continue;

                    var kind = Classify(text);
                    state.Communications.Add(new OutlookCommunication
                    {
                        MessageId = message.Id,
                        ApplicationId = match?.Id,
                        Direction = IsFromMe(fromAddress, state.AccountEmail) ? "Outbound" : "Inbound",
                        Subject = message.Subject ?? "(no subject)",
                        FromName = fromName,
                        FromEmail = fromAddress,
                        ReceivedAt = message.ReceivedDateTime ?? message.SentDateTime ?? DateTimeOffset.UtcNow,
                        Preview = message.BodyPreview,
                        WebLink = message.WebLink,
                        IsRead = message.IsRead,
                        Kind = kind,
                        SuggestedStatus = SuggestedStatus(kind),
                        MatchedCompany = match?.Company,
                        MatchedTitle = match?.Title,
                        SuggestedCompany = match?.Company ?? InferCompany(fromName, fromAddress, message.Subject),
                        SuggestedTitle = match?.Title ?? InferTitle(message.Subject)
                    });
                    added++;
                }

                url = payload.NextLink;
            }

            state.LastSyncedAt = DateTimeOffset.UtcNow;
            state.Communications = state.Communications
                .OrderByDescending(x => x.ReceivedAt)
                .Take(2000)
                .ToList();
            await WriteStateAsync(state, ct);
            return added;
        }
        finally
        {
            SyncLock.Release();
        }
    }

    private async Task EnsureAccessTokenAsync(OutlookState state, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(state.AccessToken) && state.ExpiresAt > DateTimeOffset.UtcNow)
            return;
        if (string.IsNullOrWhiteSpace(state.RefreshToken))
            return;

        var form = new Dictionary<string, string>
        {
            ["client_id"] = _options.ClientId,
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = state.RefreshToken,
            ["redirect_uri"] = _options.RedirectUri,
            ["scope"] = "openid profile offline_access User.Read Mail.Read"
        };
        if (!string.IsNullOrWhiteSpace(_options.ClientSecret))
            form["client_secret"] = _options.ClientSecret;

        using var response = await _http.PostAsync(
            $"https://login.microsoftonline.com/{Uri.EscapeDataString(_options.Tenant)}/oauth2/v2.0/token",
            new FormUrlEncodedContent(form), ct);
        response.EnsureSuccessStatusCode();

        var token = await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken: ct)
            ?? throw new InvalidOperationException("Microsoft returned an empty refresh response.");
        state.AccessToken = token.AccessToken;
        if (!string.IsNullOrWhiteSpace(token.RefreshToken))
            state.RefreshToken = token.RefreshToken;
        state.ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(Math.Max(60, token.ExpiresIn - 60));
        await WriteStateAsync(state, ct);
    }

    private async Task PopulateAccountAsync(OutlookState state, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get,
            "https://graph.microsoft.com/v1.0/me?$select=mail,userPrincipalName,displayName");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", state.AccessToken);
        using var response = await _http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        var me = await response.Content.ReadFromJsonAsync<GraphMe>(cancellationToken: ct);
        state.AccountEmail = me?.Mail ?? me?.UserPrincipalName;
    }

    private static bool LooksJobRelated(string text) =>
        JobSignals.Any(x => text.Contains(x, StringComparison.OrdinalIgnoreCase));

    private static string Classify(string text)
    {
        if (ContainsAny(text, "offer letter", "job offer", "offer of employment")) return "Offer";
        if (ContainsAny(text, "unfortunately", "not moving forward", "other candidates", "rejected")) return "Rejection";
        if (ContainsAny(text, "interview", "phone screen", "technical screen", "schedule a call", "calendar")) return "Interview";
        if (ContainsAny(text, "assessment", "coding challenge", "take-home", "hackerrank", "codility")) return "Assessment";
        if (ContainsAny(text, "thank you for applying", "application received", "we received your application")) return "Application confirmation";
        if (ContainsAny(text, "recruiter", "talent acquisition", "hiring team", "next steps")) return "Recruiter";
        return "Job communication";
    }

    private static string? SuggestedStatus(string kind) => kind switch
    {
        "Offer" => "Offer",
        "Rejection" => "Rejected",
        "Interview" => "Interviewing",
        "Application confirmation" => "Applied",
        _ => null
    };

    private static bool ContainsAny(string text, params string[] values) =>
        values.Any(v => text.Contains(v, StringComparison.OrdinalIgnoreCase));

    private static bool IsFromMe(string from, string? accountEmail) =>
        !string.IsNullOrWhiteSpace(accountEmail) && from.Equals(accountEmail, StringComparison.OrdinalIgnoreCase);

    private static JobApplication? MatchApplication(
        string text,
        string fromEmail,
        IReadOnlyList<JobApplication> applications)
    {
        var ranked = applications.Select(app =>
        {
            var score = 0;
            if (!string.IsNullOrWhiteSpace(app.ContactEmail) && fromEmail.Equals(app.ContactEmail, StringComparison.OrdinalIgnoreCase))
                score += 100;
            if (!string.IsNullOrWhiteSpace(app.Company) && text.Contains(app.Company, StringComparison.OrdinalIgnoreCase))
                score += 40;
            if (!string.IsNullOrWhiteSpace(app.Title))
            {
                var titleWords = app.Title.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                    .Where(w => w.Length >= 4)
                    .Take(4);
                score += titleWords.Count(w => text.Contains(w, StringComparison.OrdinalIgnoreCase)) * 8;
            }
            return (app, score);
        }).OrderByDescending(x => x.score).FirstOrDefault();

        return ranked.score >= 24 ? ranked.app : null;
    }

    private static string? InferTitle(string? subject)
    {
        if (string.IsNullOrWhiteSpace(subject)) return null;

        var patterns = new[]
        {
            @"application\s+(?:for|to)\s+(?<title>.+?)(?:\s+at\s+|\s+with\s+|$)",
            @"applied\s+(?:for|to)\s+(?<title>.+?)(?:\s+at\s+|\s+with\s+|$)",
            @"interview\s+(?:for|regarding)\s+(?<title>.+?)(?:\s+at\s+|\s+with\s+|$)"
        };
        foreach (var pattern in patterns)
        {
            var match = Regex.Match(subject, pattern, RegexOptions.IgnoreCase);
            if (match.Success)
            {
                var title = match.Groups["title"].Value.Trim(' ', '-', ':', '|');
                if (title.Length is >= 3 and <= 160) return title;
            }
        }
        return null;
    }

    private static string? InferCompany(string? fromName, string? fromEmail, string? subject)
    {
        if (!string.IsNullOrWhiteSpace(subject))
        {
            var match = Regex.Match(subject, @"\s(?:at|with)\s+(?<company>[^|:\-]+)$", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                var company = match.Groups["company"].Value.Trim();
                if (company.Length is >= 2 and <= 120) return company;
            }
        }

        if (!string.IsNullOrWhiteSpace(fromName))
        {
            var cleaned = Regex.Replace(fromName,
                @"\b(recruiting|recruitment|talent|talent acquisition|careers?|hiring|team|hr|human resources)\b",
                string.Empty,
                RegexOptions.IgnoreCase).Trim(' ', '-', '|');
            if (cleaned.Length is >= 2 and <= 120) return cleaned;
        }

        if (!string.IsNullOrWhiteSpace(fromEmail))
        {
            var at = fromEmail.LastIndexOf('@');
            if (at >= 0 && at < fromEmail.Length - 1)
            {
                var domain = fromEmail[(at + 1)..].ToLowerInvariant();
                if (!GenericMailDomains.Any(x => domain.Equals(x, StringComparison.OrdinalIgnoreCase) || domain.EndsWith('.' + x, StringComparison.OrdinalIgnoreCase)))
                {
                    var first = domain.Split('.')[0].Replace('-', ' ').Replace('_', ' ');
                    if (!string.IsNullOrWhiteSpace(first))
                        return System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(first);
                }
            }
        }

        return null;
    }

    private static bool CryptographicEquals(string expected, string actual)
    {
        var left = System.Text.Encoding.UTF8.GetBytes(expected);
        var right = System.Text.Encoding.UTF8.GetBytes(actual);
        return left.Length == right.Length && System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(left, right);
    }

    private async Task<OutlookState> ReadStateAsync(CancellationToken ct)
    {
        await StoreLock.WaitAsync(ct);
        try
        {
            if (!File.Exists(_options.StorePath)) return new OutlookState();
            await using var stream = File.OpenRead(_options.StorePath);
            return await JsonSerializer.DeserializeAsync<OutlookState>(stream, cancellationToken: ct) ?? new OutlookState();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not read Outlook state store; starting with empty state.");
            return new OutlookState();
        }
        finally
        {
            StoreLock.Release();
        }
    }

    private async Task WriteStateAsync(OutlookState state, CancellationToken ct)
    {
        await StoreLock.WaitAsync(ct);
        try
        {
            var dir = Path.GetDirectoryName(_options.StorePath);
            if (!string.IsNullOrWhiteSpace(dir)) Directory.CreateDirectory(dir);
            await using var stream = File.Create(_options.StorePath);
            await JsonSerializer.SerializeAsync(stream, state, new JsonSerializerOptions { WriteIndented = true }, ct);
        }
        finally
        {
            StoreLock.Release();
        }
    }

    private sealed class OutlookState
    {
        public string? AccessToken { get; set; }
        public string? RefreshToken { get; set; }
        public DateTimeOffset ExpiresAt { get; set; }
        public string? AccountEmail { get; set; }
        public DateTimeOffset? LastSyncedAt { get; set; }
        public string? PendingAuthState { get; set; }
        public DateTimeOffset? PendingAuthStateExpiresAt { get; set; }
        public List<OutlookCommunication> Communications { get; set; } = new();
    }

    private sealed class TokenResponse
    {
        [JsonPropertyName("access_token")] public string AccessToken { get; set; } = string.Empty;
        [JsonPropertyName("refresh_token")] public string? RefreshToken { get; set; }
        [JsonPropertyName("expires_in")] public int ExpiresIn { get; set; } = 3600;
    }

    private sealed class GraphMe
    {
        [JsonPropertyName("mail")] public string? Mail { get; set; }
        [JsonPropertyName("userPrincipalName")] public string? UserPrincipalName { get; set; }
    }

    private sealed class GraphMessagesResponse
    {
        [JsonPropertyName("value")] public List<GraphMessage>? Value { get; set; }
        [JsonPropertyName("@odata.nextLink")] public string? NextLink { get; set; }
    }

    private sealed class GraphMessage
    {
        [JsonPropertyName("id")] public string? Id { get; set; }
        [JsonPropertyName("subject")] public string? Subject { get; set; }
        [JsonPropertyName("from")] public GraphRecipient? From { get; set; }
        [JsonPropertyName("receivedDateTime")] public DateTimeOffset? ReceivedDateTime { get; set; }
        [JsonPropertyName("sentDateTime")] public DateTimeOffset? SentDateTime { get; set; }
        [JsonPropertyName("bodyPreview")] public string? BodyPreview { get; set; }
        [JsonPropertyName("webLink")] public string? WebLink { get; set; }
        [JsonPropertyName("isRead")] public bool IsRead { get; set; }
    }

    private sealed class GraphRecipient
    {
        [JsonPropertyName("emailAddress")] public GraphEmailAddress? EmailAddress { get; set; }
    }

    private sealed class GraphEmailAddress
    {
        [JsonPropertyName("name")] public string? Name { get; set; }
        [JsonPropertyName("address")] public string? Address { get; set; }
    }
}

public sealed record OutlookConnectionStatus(
    bool IsConfigured,
    bool IsConnected,
    string? AccountEmail,
    DateTimeOffset? LastSyncedAt,
    int CommunicationCount);

public sealed class OutlookCommunication
{
    public string MessageId { get; set; } = string.Empty;
    public int? ApplicationId { get; set; }
    public string Direction { get; set; } = "Inbound";
    public string Subject { get; set; } = string.Empty;
    public string? FromName { get; set; }
    public string? FromEmail { get; set; }
    public DateTimeOffset ReceivedAt { get; set; }
    public string? Preview { get; set; }
    public string? WebLink { get; set; }
    public bool IsRead { get; set; }
    public string Kind { get; set; } = "Job communication";
    public string? SuggestedStatus { get; set; }
    public string? MatchedCompany { get; set; }
    public string? MatchedTitle { get; set; }
    public string? SuggestedCompany { get; set; }
    public string? SuggestedTitle { get; set; }
}
