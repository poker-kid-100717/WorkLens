namespace WorkLens.Core.DTOs;

public class AnalyticsSummaryDto
{
    public int TotalApplications { get; set; }
    public int TotalSaved { get; set; }
    public int TotalApplied { get; set; }
    public int TotalInterviewing { get; set; }
    public int TotalOffers { get; set; }
    public int TotalRejected { get; set; }
    public double ResponseRatePercent { get; set; }
    public double InterviewRatePercent { get; set; }
    public double OfferRatePercent { get; set; }
    public int ActiveApplications { get; set; }
    public int DueFollowUps { get; set; }
    public List<FunnelStageDto> Funnel { get; set; } = new();
    public List<WeeklyCountDto> ApplicationsPerWeek { get; set; } = new();
    public List<CompanyCountDto> TopCompanies { get; set; } = new();
}

public class FunnelStageDto
{
    public string Stage { get; set; } = string.Empty;
    public int Count { get; set; }
}

public class WeeklyCountDto
{
    public string WeekStart { get; set; } = string.Empty;
    public int Count { get; set; }
}

public class CompanyCountDto
{
    public string Company { get; set; } = string.Empty;
    public int Count { get; set; }
}
