namespace Shora.Application.Abstractions;

public interface ICurrentUser
{
    Guid? UserId { get; }

    string? Role { get; }

    bool IsAuthenticated { get; }
}
