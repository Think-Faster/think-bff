using BFF.Contracts.Common;
using BFF.Contracts.Groups;
using BFF.Contracts.Users;

namespace BFF.Application.Services;

public interface IUserService
{
    Task<PagedResult<UserListItemDto>> ListAsync(string? search, int page, int pageSize, CancellationToken ct);

    Task<UserDto> GetAsync(Guid id, CancellationToken ct);

    Task<UserDto> CreateAsync(CreateUserRequest request, CancellationToken ct);

    Task<UserDto> UpdateAsync(Guid id, UpdateUserRequest request, CancellationToken ct);

    Task DeleteAsync(Guid id, bool soft, CancellationToken ct);

    Task<IReadOnlyList<GroupRefDto>> AddGroupsAsync(Guid id, AddUserGroupsRequest request, CancellationToken ct);

    Task RemoveGroupAsync(Guid id, Guid groupId, CancellationToken ct);

    /// <summary>Resolves a JWT `sub` claim to a provisioned BFF user, for TokenAuthenticationMiddleware
    /// (section 8.2). Returns null when no such user exists.</summary>
    Task<AuthenticatedUserDto?> FindByAuthUserIdAsync(string authUserId, CancellationToken ct);
}
