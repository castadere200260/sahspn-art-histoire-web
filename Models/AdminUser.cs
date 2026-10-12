using System.ComponentModel.DataAnnotations;

namespace Sahspn.ArtHistoire.Models;

/// <summary>Administrator identity persisted by the selected database provider.</summary>
public sealed class AdminUser
{
    public int Id { get; set; }
    [MaxLength(100)] public required string Username { get; set; }
    [MaxLength(500)] public required string PasswordHash { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;
}
