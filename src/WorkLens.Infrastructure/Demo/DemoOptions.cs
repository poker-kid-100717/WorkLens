namespace WorkLens.Infrastructure.Demo;

/// <summary>
/// Public read-only demo mode (configuration section "Demo"). When enabled the API uses a
/// throwaway SQLite database seeded with fictional tracker data, rejects every write, and
/// disables the integrations that would touch personal data (Outlook, resume matching).
/// The live job feed keeps running, so visitors see real public listings.
/// </summary>
public class DemoOptions
{
    public const string Section = "Demo";

    public bool Enabled { get; set; }

    /// <summary>SQLite file for the demo database; deleted and recreated on every start.</summary>
    public string DatabasePath { get; set; } = Path.Combine(Path.GetTempPath(), "worklens-demo.db");
}
