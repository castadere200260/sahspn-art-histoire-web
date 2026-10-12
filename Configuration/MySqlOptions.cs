namespace Sahspn.ArtHistoire.Configuration;

/// <summary>Connection settings used only when the MySql provider is selected.</summary>
public sealed class MySqlOptions
{
    public const string SectionName = "MySql";

    public string ConnectionString { get; set; } = string.Empty;
}
