namespace Sahspn.ArtHistoire.Configuration;

/// <summary>Identifies the single persistence provider selected for this application instance.</summary>
public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    public string Provider { get; set; } = "MySql";
}
