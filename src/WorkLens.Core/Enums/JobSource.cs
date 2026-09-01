namespace WorkLens.Core.Enums;

/// <summary>
/// Upstream provider a job listing was ingested from.
/// </summary>
public enum JobSource
{
    RemoteOk = 0,
    Remotive = 1,
    Greenhouse = 2,
    Manual = 3,
    Dice = 4,
    Jobicy = 5,
    WeWorkRemotely = 6,
    ChatGptWatch = 7
}
