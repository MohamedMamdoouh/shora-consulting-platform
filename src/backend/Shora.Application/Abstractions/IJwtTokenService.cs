using Shora.Domain.Entities;

namespace Shora.Application.Abstractions;

public interface IJwtTokenService
{
    string CreateAccessToken(ApplicationUser user, string role);
}
