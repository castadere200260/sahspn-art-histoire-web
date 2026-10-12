using System.ComponentModel.DataAnnotations;

namespace Sahspn.ArtHistoire.Models;

/// <summary>Input model for creating and editing a site page in the administration area.</summary>
public sealed class SitePageFormViewModel
{
    [Required, StringLength(180)]
    public string Title { get; set; } = string.Empty;

    [Required, StringLength(100), RegularExpression(@"^[a-z0-9]+(?:-[a-z0-9]+)*$", ErrorMessage = "Utilisez uniquement des minuscules, chiffres et tirets simples.")]
    public string Slug { get; set; } = string.Empty;

    [Required]
    public string Content { get; set; } = string.Empty;

    /// <summary>The persisted slug before editing; null means creation.</summary>
    public string? OriginalSlug { get; set; }

    /// <summary>Display-only state, derived by the server and never trusted for persistence.</summary>
    public bool IsNew => string.IsNullOrWhiteSpace(OriginalSlug);

    public static SitePageFormViewModel FromPage(SitePage page, string? originalSlug) => new()
    {
        Title = page.Title,
        Slug = page.Slug,
        Content = page.Content,
        OriginalSlug = originalSlug
    };
}
