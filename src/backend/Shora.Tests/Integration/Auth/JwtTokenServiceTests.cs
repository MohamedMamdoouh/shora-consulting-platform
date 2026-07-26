using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Extensions.DependencyInjection;
using Shora.Application.Abstractions;
using Shora.Domain.Entities;
using Shora.Infrastructure.Services;
using Shora.Tests.Common;

namespace Shora.Tests.Integration.Auth;

[Collection("SqlServer")]
public class JwtTokenServiceTests : IDisposable
{
    private readonly AuthWebApplicationFactory _factory;

    public JwtTokenServiceTests(SqlServerFixture sqlServer)
    {
        _factory = new AuthWebApplicationFactory(sqlServer);
    }

    [Fact]
    public void CreateAccessToken_includes_expected_claims()
    {
        using var scope = _factory.Services.CreateScope();

        var jwtTokenService = scope.ServiceProvider.GetRequiredService<IJwtTokenService>();
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Email = "client@test.local",
            DisplayName = "Client User",
            EmailConfirmed = true
        };

        var token = jwtTokenService.CreateAccessToken(user, "Client");
        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);

        Assert.Equal(user.Id.ToString(), jwt.Claims.First(c => c.Type == JwtRegisteredClaimNames.Sub).Value);
        Assert.Equal("client@test.local", jwt.Claims.First(c => c.Type == ClaimTypes.Email).Value);
        Assert.Equal("Client", jwt.Claims.First(c => c.Type == ClaimTypes.Role).Value);
        Assert.Equal("true", jwt.Claims.First(c => c.Type == "email_verified").Value);
    }

    [Fact]
    public void HashToken_does_not_store_raw_token()
    {
        const string raw = "sample-refresh-token-value";
        var hash = RefreshCookieService.HashToken(raw);
        Assert.NotEqual(raw, hash);
        Assert.Equal(64, hash.Length);
    }

    public void Dispose()
    {
        _factory.Dispose();
    }
}
