namespace WorkLens.Infrastructure.Demo;

/// <summary>
/// Public read-only demo mode (configuration section "Demo"). When enabled the API uses a
/// throwaway SQLite database (or SQL Server, when <see cref="ConnectionString"/> is set)
/// seeded with fictional tracker data, rejects every write, and
/// disables the integrations that would touch personal data (Outlook, resume matching).
/// The live job feed keeps running, so visitors see real public listings.
/// </summary>
public class DemoOptions
{
    public const string Section = "Demo";

    public bool Enabled { get; set; }

    /// <summary>SQLite file for the demo database; deleted and recreated on every start.</summary>
    public string DatabasePath { get; set; } = Path.Combine(Path.GetTempPath(), "worklens-demo.db");

    /// <summary>
    /// Optional SQL Server connection string for the demo (for example Azure SQL Database).
    /// When set, the demo runs on SQL Server instead of SQLite: migrations are applied and the
    /// fictional tracker data is reseeded on every start.
    /// </summary>
    public string? ConnectionString { get; set; }

    public bool UsesSqlServer => !string.IsNullOrWhiteSpace(ConnectionString);
}
