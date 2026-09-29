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

    /// <summary>Resolves each requested userId to its email (for Notifications' email-by-userId
    /// recipients) — one result per requested id, in any order, always present even for a missing
    /// user (Found=false).</summary>
    Task<IReadOnlyList<UserEmailDto>> ResolveEmailsAsync(IReadOnlyList<Guid> userIds, CancellationToken ct);

    /// <summary>То же для Telegram: userId → имя в Telegram (users.telegram) — для POST /notifications/telegram.</summary>
    Task<IReadOnlyList<UserTelegramDto>> ResolveTelegramsAsync(IReadOnlyList<Guid> userIds, CancellationToken ct);

    /// <summary>Своё имя в Telegram — для профиля (GET /users/me/telegram).</summary>
    Task<string?> GetTelegramAsync(Guid id, CancellationToken ct);

    /// <summary>Сохраняет имя в Telegram (нормализованное, без @); null или пусто — убрать. Возвращает сохранённое.</summary>
    Task<string?> SetTelegramAsync(Guid id, string? telegram, CancellationToken ct);
}
