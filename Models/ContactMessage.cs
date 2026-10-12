using System.ComponentModel.DataAnnotations;
namespace Sahspn.ArtHistoire.Models;
public sealed class ContactMessage
{
    public int Id { get; set; }
    [Required, MaxLength(120)] public required string Name { get; set; }
    [Required, EmailAddress, MaxLength(254)] public required string Email { get; set; }
    [Required, MaxLength(180)] public required string Subject { get; set; }
    [Required, MaxLength(5000)] public required string Message { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
