// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Application.Account;
using Authora.Core.Application.Account.Sessions;
using Authora.Core.Application.Account.SignIn;
using Authora.Core.Application.Account.SignOut;
using Authora.Core.Application.AdminUi.GetSettings;
using Authora.Core.Application.Audit.Chain;
using Authora.Core.Application.Audit.Search;
using Authora.Core.Application.Audit.Verify;
using Authora.Core.Application.Authorization;
using Authora.Core.Application.Clients;
using Authora.Core.Application.Clients.AddRedirectUri;
using Authora.Core.Application.Clients.AllowScope;
using Authora.Core.Application.Clients.CreateSecret;
using Authora.Core.Application.Clients.Delete;
using Authora.Core.Application.Clients.Disable;
using Authora.Core.Application.Clients.DisallowScope;
using Authora.Core.Application.Clients.Enable;
using Authora.Core.Application.Clients.Get;
using Authora.Core.Application.Clients.GetAll;
using Authora.Core.Application.Clients.Register;
using Authora.Core.Application.Clients.RemoveRedirectUri;
using Authora.Core.Application.Clients.RevokeSecret;
using Authora.Core.Application.Clients.SetFirstParty;
using Authora.Core.Application.Clients.ServeScope;
using Authora.Core.Application.Clients.StopServingScope;
using Authora.Core.Application.Clients.Update;
using Authora.Core.Application.OAuth;
using Authora.Core.Application.OAuth.Authorize;
using Authora.Core.Application.OAuth.ClientAuthentication;
using Authora.Core.Application.OAuth.Introspect;
using Authora.Core.Application.OAuth.Revoke;
using Authora.Core.Application.OAuth.Token;
using Authora.Core.Application.Groups.AddMember;
using Authora.Core.Application.Groups.AssignRole;
using Authora.Core.Application.Groups.Create;
using Authora.Core.Application.Groups.Delete;
using Authora.Core.Application.Groups.Get;
using Authora.Core.Application.Groups.GetAll;
using Authora.Core.Application.Groups.GetMembers;
using Authora.Core.Application.Groups.RemoveMember;
using Authora.Core.Application.Groups.RemoveRole;
using Authora.Core.Application.Groups.Update;
using Authora.Core.Application.Oidc;
using Authora.Core.Application.Oidc.UserInfo;
using Authora.Core.Application.Permissions.GetAll;
using Authora.Core.Application.Roles.Create;
using Authora.Core.Application.Roles.Delete;
using Authora.Core.Application.Roles.Get;
using Authora.Core.Application.Roles.GetAll;
using Authora.Core.Application.Roles.GrantPermission;
using Authora.Core.Application.Roles.RevokePermission;
using Authora.Core.Application.Roles.Update;
using Authora.Core.Application.Scopes.Create;
using Authora.Core.Application.Scopes.Delete;
using Authora.Core.Application.Scopes.Get;
using Authora.Core.Application.Scopes.GetAll;
using Authora.Core.Application.Scopes.Update;
using Authora.Core.Application.Users.Activate;
using Authora.Core.Application.Users.AssignRole;
using Authora.Core.Application.Users.Create;
using Authora.Core.Application.Users.Deactivate;
using Authora.Core.Application.Users.Delete;
using Authora.Core.Application.Users.Get;
using Authora.Core.Application.Users.GetAll;
using Authora.Core.Application.Users.GetCurrent;
using Authora.Core.Application.Users.GetGroups;
using Authora.Core.Application.Users.GetPermissions;
using Authora.Core.Application.Users.GetRoles;
using Authora.Core.Application.Users.RemoveRole;
using Authora.Core.Application.Users.RevokeSessions;
using Authora.Core.Application.Users.SetPassword;
using Authora.Core.Application.Users.Unlock;
using Authora.Core.Application.Users.Update;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Authora.Core;

/// <summary>
///     Defines Extension Methods for the IServiceCollection.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    ///     Registeres Application Logic to the Service Collection.
    /// </summary>
    /// <param name="serviceCollection"></param>
    /// <returns></returns>
    public static IServiceCollection AddApplication(this IServiceCollection serviceCollection)
    {
        serviceCollection.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<SignInOptions>, SignInOptionsValidator>());
        serviceCollection.AddTransient<SignInService>();
        serviceCollection.AddTransient<SignOutService>();
        serviceCollection.AddTransient<ValidateSessionService>();

        serviceCollection.AddTransient<PermissionEvaluator>();

        serviceCollection.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<AuditChainOptions>, AuditChainOptionsValidator>());
        serviceCollection.AddTransient<SearchAuditEventsService>();
        serviceCollection.AddTransient<VerifyAuditChainService>();

        serviceCollection.AddTransient<GetAdminUiSettingsService>();

        serviceCollection.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<OAuthOptions>, OAuthOptionsValidator>());
        serviceCollection.AddTransient<AccessTokenValidator>();
        serviceCollection.AddTransient<ClientAuthenticator>();
        serviceCollection.AddTransient<IntrospectTokenService>();
        serviceCollection.AddTransient<IssueAuthorizationCodeService>();
        serviceCollection.AddTransient<IssueTokenService>();
        serviceCollection.AddTransient<RevokeTokenService>();
        serviceCollection.AddTransient<TokenIssuer>();
        serviceCollection.AddTransient<ValidateAuthorizationRequestService>();

        serviceCollection.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<OidcOptions>, OidcOptionsValidator>());
        serviceCollection.AddTransient<GetUserInfoService>();
        serviceCollection.AddTransient<IdTokenIssuer>();

        serviceCollection.AddTransient<ClientSecretIssuer>();
        serviceCollection.AddTransient<AddRedirectUriService>();
        serviceCollection.AddTransient<AllowScopeService>();
        serviceCollection.AddTransient<CreateClientSecretService>();
        serviceCollection.AddTransient<DeleteClientService>();
        serviceCollection.AddTransient<DisableClientService>();
        serviceCollection.AddTransient<DisallowScopeService>();
        serviceCollection.AddTransient<ServeScopeService>();
        serviceCollection.AddTransient<StopServingScopeService>();
        serviceCollection.AddTransient<EnableClientService>();
        serviceCollection.AddTransient<SetFirstPartyService>();
        serviceCollection.AddTransient<GetAllClientsService>();
        serviceCollection.AddTransient<GetClientService>();
        serviceCollection.AddTransient<RegisterClientService>();
        serviceCollection.AddTransient<RemoveRedirectUriService>();
        serviceCollection.AddTransient<RevokeClientSecretService>();
        serviceCollection.AddTransient<UpdateClientService>();

        serviceCollection.AddTransient<AddGroupMemberService>();
        serviceCollection.AddTransient<AssignGroupRoleService>();
        serviceCollection.AddTransient<CreateGroupService>();
        serviceCollection.AddTransient<DeleteGroupService>();
        serviceCollection.AddTransient<GetAllGroupsService>();
        serviceCollection.AddTransient<GetGroupMembersService>();
        serviceCollection.AddTransient<GetGroupService>();
        serviceCollection.AddTransient<RemoveGroupMemberService>();
        serviceCollection.AddTransient<RemoveGroupRoleService>();
        serviceCollection.AddTransient<UpdateGroupService>();

        serviceCollection.AddTransient<GetAllPermissionsService>();

        serviceCollection.AddTransient<CreateRoleService>();
        serviceCollection.AddTransient<DeleteRoleService>();
        serviceCollection.AddTransient<GetAllRolesService>();
        serviceCollection.AddTransient<GetRoleService>();
        serviceCollection.AddTransient<GrantPermissionService>();
        serviceCollection.AddTransient<RevokePermissionService>();
        serviceCollection.AddTransient<UpdateRoleService>();

        serviceCollection.AddTransient<CreateScopeService>();
        serviceCollection.AddTransient<DeleteScopeService>();
        serviceCollection.AddTransient<GetAllScopesService>();
        serviceCollection.AddTransient<GetScopeService>();
        serviceCollection.AddTransient<UpdateScopeService>();

        serviceCollection.AddTransient<ActivateUserService>();
        serviceCollection.AddTransient<AssignRoleService>();
        serviceCollection.AddTransient<CreateUserService>();
        serviceCollection.AddTransient<DeactivateUserService>();
        serviceCollection.AddTransient<DeleteUserService>();
        serviceCollection.AddTransient<GetEffectivePermissionsService>();
        serviceCollection.AddTransient<GetCurrentUserService>();
        serviceCollection.AddTransient<GetUserService>();
        serviceCollection.AddTransient<GetUserGroupsService>();
        serviceCollection.AddTransient<GetUserRolesService>();
        serviceCollection.AddTransient<GetAllUsersService>();
        serviceCollection.AddTransient<RemoveRoleService>();
        serviceCollection.AddTransient<RevokeUserSessionsService>();
        serviceCollection.AddTransient<SetPasswordService>();
        serviceCollection.AddTransient<UnlockUserService>();
        serviceCollection.AddTransient<UpdateUserService>();

        return serviceCollection;
    }
}
