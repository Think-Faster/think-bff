using BFF.Contracts.Common;
using BFF.Contracts.Groups;

namespace BFF.Application.Services;

public interface IGroupService
{
    Task<PagedResult<GroupListItemDto>> ListAsync(string? search, int page, int pageSize, CancellationToken ct);

    Task<GroupDto> GetAsync(Guid id, CancellationToken ct);

    Task<GroupDto> CreateAsync(CreateGroupRequest request, CancellationToken ct);

    Task<GroupDto> UpdateAsync(Guid id, UpdateGroupRequest request, CancellationToken ct);

    Task DeleteAsync(Guid id, CancellationToken ct);

    Task AddMemberAsync(Guid id, AddGroupMemberRequest request, CancellationToken ct);

    Task AddMembersBatchAsync(Guid id, AddGroupMembersBatchRequest request, CancellationToken ct);

    Task RemoveMemberAsync(Guid id, string memberType, Guid memberId, CancellationToken ct);
}
