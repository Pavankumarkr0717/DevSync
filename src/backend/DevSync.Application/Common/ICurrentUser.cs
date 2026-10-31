namespace DevSync.Application.Common;

/// <summary>The authenticated user making the current request.</summary>
public interface ICurrentUser
{
    Guid UserId { get; }
}
