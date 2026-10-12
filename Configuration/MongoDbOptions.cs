namespace Sahspn.ArtHistoire.Configuration;

/// <summary>Connection settings used only when the MongoDb provider is selected.</summary>
public sealed class MongoDbOptions
{
    public const string SectionName = "MongoDb";

    public string ConnectionString { get; set; } = string.Empty;
    public string DatabaseName { get; set; } = "sahspn";
}
