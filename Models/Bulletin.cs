using System.ComponentModel.DataAnnotations;
namespace Sahspn.ArtHistoire.Models;
public sealed class Bulletin
{
    public int Id { get; set; }
    public int Number { get; set; }
    [MaxLength(180)] public required string Title { get; set; }
    public string Content { get; set; } = "";
    public string? CoverImage { get; set; }
    public string? DocumentUrl { get; set; }
    public DateTime? PublishedAt { get; set; }
}
