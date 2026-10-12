using System.ComponentModel.DataAnnotations;
namespace Sahspn.ArtHistoire.Models;
public sealed class AdminUserInput
{
    [Required, StringLength(100, MinimumLength = 3)] public string Username { get; set; } = "";
    [StringLength(20, MinimumLength = 10)] public string? Password { get; set; }
    public bool IsActive { get; set; } = true;
}
