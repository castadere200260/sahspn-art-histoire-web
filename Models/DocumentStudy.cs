using System.ComponentModel.DataAnnotations;

namespace Sahspn.ArtHistoire.Models;

/// <summary>A searchable public document or historical study.</summary>
public sealed class DocumentStudy
{
    public int Id { get; set; }
    [Required, StringLength(180)] public string Title { get; set; } = string.Empty;
    [Required] public DocumentStudyType Type { get; set; } = DocumentStudyType.Etude;
    [StringLength(3000)] public string? Summary { get; set; }
    public string Content { get; set; } = string.Empty;
    [StringLength(500)] public string? CoverImage { get; set; }
    [StringLength(500)] public string? DocumentUrl { get; set; }
    public DateTime? PublishedAt { get; set; }
    public bool IsPublished { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

public enum DocumentStudyType { Document = 0, Etude = 1 }
