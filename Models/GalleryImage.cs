using System.ComponentModel.DataAnnotations;

namespace Sahspn.ArtHistoire.Models;

public sealed class GalleryImage
{
    [StringLength(36)] public string Id { get; set; } = string.Empty;
    [Required, StringLength(160)] public string Title { get; set; } = string.Empty;
    [StringLength(500)] public string? Description { get; set; }
    [StringLength(500)] public string ImageUrl { get; set; } = string.Empty;
    [StringLength(180)] public string? AltText { get; set; }
    public bool IsPublished { get; set; } = true;
    public int DisplayOrder { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
