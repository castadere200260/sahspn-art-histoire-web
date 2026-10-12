using System.ComponentModel.DataAnnotations;

namespace Sahspn.ArtHistoire.Models;

/// <summary>A searchable item in the association's public library.</summary>
public sealed class LibraryItem
{
    public int Id { get; set; }
    [Required, StringLength(180)] public string Title { get; set; } = string.Empty;
    [StringLength(180)] public string? Author { get; set; }
    [StringLength(120)] public string? Category { get; set; }
    [Range(1000, 3000)] public int? PublicationYear { get; set; }
    [StringLength(3000)] public string? Summary { get; set; }
    public string Content { get; set; } = string.Empty;
    [StringLength(500)] public string? CoverImage { get; set; }
    [StringLength(500)] public string? DocumentUrl { get; set; }
    public bool IsPublished { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
