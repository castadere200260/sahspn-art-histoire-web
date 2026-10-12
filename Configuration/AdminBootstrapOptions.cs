namespace Sahspn.ArtHistoire.Configuration;

/// <summary>Optional one-time provisioning settings. Supply the password via an environment variable or secret store.</summary>
public sealed class AdminBootstrapOptions
{
    public const string SectionName = "AdminBootstrap";
    public string Username { get; set; } = "admin";
    public string Password { get; set; } = string.Empty;
}
