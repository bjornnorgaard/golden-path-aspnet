using System.Diagnostics;
using Goldenpath.Users.V1;
using Grpc.Core;
using WebApi.Features.Users.DeleteUser;
using WebApi.Features.Users.GetUserById;
using WebApi.Features.Users.GetUserList;
using WebApi.Features.Users.UpdateUser;
using WebApi.Services;
using WebApi.Telemetry;
using UserId = WebApi.Database.Models.UserId;

namespace WebApi.Features.Users;

/// <summary>
/// The gRPC adapter over the user handlers (Contracts/Grpc/goldenpath/users/v1/users.proto): type
/// mapping only, see <see cref="WebApi.Features.Todos.TodosGrpcService"/>.
/// </summary>
internal sealed class UsersGrpcService(
    GetUserByIdHandler get,
    GetUserListHandler list,
    UpdateUserHandler update,
    DeleteUserHandler delete) : UsersService.UsersServiceBase
{
    public override async Task<GetUserByIdResponse> GetUserById(GetUserByIdRequest request, ServerCallContext context)
    {
        var userId = ParseId(request.Id);
        var result = await get.HandleAsync(new GetUserByIdHandler.Command { Id = userId }, context.CancellationToken)
                     ?? throw UserNotFound();

        return new GetUserByIdResponse
        {
            User = ToUser(result.Id.Value, result.Email, result.DisplayName, result.GivenName, result.FamilyName, result.AvatarUrl)
        };
    }

    public override async Task<GetUserListResponse> GetUserList(GetUserListRequest request, ServerCallContext context)
    {
        var result = await list.HandleAsync(new GetUserListHandler.Command
        {
            Limit = request.HasLimit ? request.Limit : null,
            Offset = request.HasOffset ? request.Offset : null
        }, context.CancellationToken);

        var response = new GetUserListResponse();
        response.Users.AddRange(result.Users.Select(user =>
            ToUser(user.Id, user.Email, user.DisplayName, user.GivenName, user.FamilyName, user.AvatarUrl)));
        return response;
    }

    public override async Task<UpdateUserResponse> UpdateUser(UpdateUserRequest request, ServerCallContext context)
    {
        var userId = ParseId(request.Id);
        var result = await update.HandleAsync(new UpdateUserHandler.Command
        {
            Id = userId,
            DisplayName = request.HasDisplayName ? request.DisplayName : null,
            GivenName = request.HasGivenName ? request.GivenName : null,
            FamilyName = request.HasFamilyName ? request.FamilyName : null,
            AvatarUrl = request.HasAvatarUrl ? request.AvatarUrl : null
        }, context.CancellationToken) ?? throw UserNotFound();

        return new UpdateUserResponse
        {
            User = ToUser(result.Id.Value, result.Email, result.DisplayName, result.GivenName, result.FamilyName, result.AvatarUrl)
        };
    }

    public override async Task<DeleteUserResponse> DeleteUser(DeleteUserRequest request, ServerCallContext context)
    {
        var userId = ParseId(request.Id);
        var result = await delete.HandleAsync(new DeleteUserHandler.Command { Id = userId }, context.CancellationToken)
                     ?? throw UserNotFound();

        return new DeleteUserResponse { Id = result.Id.Value.ToString() };
    }

    private static UserId ParseId(string id)
    {
        if (!UserId.TryParse(id, out var userId))
        {
            throw GrpcErrors.InvalidId();
        }

        Activity.Current?.SetUserId(userId);
        return userId;
    }

    private static RpcException UserNotFound() => GrpcErrors.NotFound("User was not found.");

    private static User ToUser(Guid id, string email, string? displayName, string? givenName, string? familyName, string? avatarUrl)
    {
        var user = new User { Id = id.ToString(), Email = email };
        if (displayName is not null) user.DisplayName = displayName;
        if (givenName is not null) user.GivenName = givenName;
        if (familyName is not null) user.FamilyName = familyName;
        if (avatarUrl is not null) user.AvatarUrl = avatarUrl;
        return user;
    }
}
