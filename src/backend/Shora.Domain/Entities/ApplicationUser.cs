using Microsoft.AspNetCore.Identity;
using Shora.Domain.Enums;

namespace Shora.Domain.Entities;

public class ApplicationUser : IdentityUser<Guid>
{
    public string DisplayName { get; set; } = string.Empty;

    public UserRole Role { get; set; } = UserRole.Client;

    public ICollection<Booking> Bookings { get; set; } = [];

    public ICollection<RefreshToken> RefreshTokens { get; set; } = [];
}
