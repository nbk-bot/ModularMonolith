using Microsoft.AspNetCore.Identity;

namespace Identity.Domain;

public class ApplicationUser : IdentityUser<Guid>
{
    public string? FullName { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? RefreshTokenExpiresAt { get; set; }
    public string? RefreshToken { get; set; }
}

public class ApplicationRole : IdentityRole<Guid>
{
    public string? Description { get; set; }
}
