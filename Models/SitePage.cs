using System.ComponentModel.DataAnnotations;
namespace Sahspn.ArtHistoire.Models;
public sealed class SitePage
{
    public int Id { get; set; }
    [MaxLength(100)] public required string Slug { get; set; }
    [MaxLength(180)] public required string Title { get; set; }
    public required string Content { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
