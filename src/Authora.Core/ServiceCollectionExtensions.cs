// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Application.Account;
using Authora.Core.Application.Account.ChangePassword;
using Authora.Core.Application.Account.CompleteSignIn;
using Authora.Core.Application.Account.PendingSignIn;
using Authora.Core.Application.Account.RequestEmailVerification;
using Authora.Core.Application.Account.RequestPasswordReset;
using Authora.Core.Application.Account.ResetPassword;
using Authora.Core.Application.Account.RevokeConsent;
using Authora.Core.Application.Account.SecondFactors;
using Authora.Core.Application.Account.Security;
using Authora.Core.Application.Account.Sessions;
using Authora.Core.Application.Account.SignIn;
using Authora.Core.Application.Account.SignOut;
using Authora.Core.Application.Account.StepUp;
using Authora.Core.Application.Account.VerifyEmail;
using Authora.Core.Application.AdminUi.GetSettings;
using Authora.Core.Application.Audit.Chain;
using Authora.Core.Application.Audit.Search;
using Authora.Core.Application.Audit.Verify;
using Authora.Core.Application.Authorization;
using Authora.Core.Application.BackchannelLogout;
using Authora.Core.Application.Clients;
using Authora.Core.Application.Clients.AddPostLogoutRedirectUri;
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
using Authora.Core.Application.Clients.RemovePostLogoutRedirectUri;
using Authora.Core.Application.Clients.RemoveRedirectUri;
using Authora.Core.Application.Clients.RevokeSecret;
using Authora.Core.Application.Clients.SetFirstParty;
using Authora.Core.Application.Clients.SetMfaRequired;
using Authora.Core.Application.Clients.SetPkceOptional;
using Authora.Core.Application.Clients.ServeScope;
using Authora.Core.Application.Clients.StopServingScope;
using Authora.Core.Application.Clients.Update;
using Authora.Core.Application.Consents;
using Authora.Core.Application.Consents.Check;
using Authora.Core.Application.Consents.GetAll;
using Authora.Core.Application.Consents.Remember;
using Authora.Core.Application.Consents.Revoke;
using Authora.Core.Application.Email;
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
using Authora.Core.Application.Mfa;
using Authora.Core.Application.Mfa.ConfirmEnrollment;
using Authora.Core.Application.Mfa.GetStatus;
using Authora.Core.Application.Mfa.RegenerateRecoveryCodes;
using Authora.Core.Application.Mfa.Remove;
using Authora.Core.Application.Mfa.StartEnrollment;
using Authora.Core.Application.Oidc;
using Authora.Core.Application.Oidc.EndSession;
using Authora.Core.Application.Oidc.UserInfo;
using Authora.Core.Application.Passkeys;
using Authora.Core.Application.Passkeys.Register;
using Authora.Core.Application.Passkeys.Remove;
using Authora.Core.Application.Passkeys.Rename;
using Authora.Core.Application.Passkeys.StartAuthentication;
using Authora.Core.Application.Passkeys.StartRegistration;
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
using Authora.Core.Application.Users;
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
using Authora.Core.Application.Users.MarkEmailVerified;
using Authora.Core.Application.Users.RemovePasskey;
using Authora.Core.Application.Users.RemoveRole;
using Authora.Core.Application.Users.ResetMfa;
using Authora.Core.Application.Users.RevokeSessions;
using Authora.Core.Application.Users.SendEmailVerification;
using Authora.Core.Application.Users.SendPasswordReset;
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
        serviceCollection.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<PasswordResetOptions>, PasswordResetOptionsValidator>());
        serviceCollection.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<EmailVerificationOptions>, EmailVerificationOptionsValidator>());
        serviceCollection.AddTransient<ChangePasswordService>();
        serviceCollection.AddTransient<CompleteSignInService>();
        serviceCollection.AddTransient<GetAccountSecurityService>();
        serviceCollection.AddTransient<GetPendingSignInService>();
        serviceCollection.AddTransient<PendingSignInReader>();
        serviceCollection.AddTransient<RequestEmailVerificationService>();
        serviceCollection.AddTransient<RequestPasswordResetService>();
        serviceCollection.AddTransient<ProcessPasswordResetRequestService>();
        serviceCollection.AddTransient<ResetPasswordService>();
        serviceCollection.AddTransient<RevokeOwnConsentService>();
        serviceCollection.AddTransient<SignInService>();
        serviceCollection.AddTransient<SessionRevoker>();
        serviceCollection.AddTransient<SignOutService>();
        serviceCollection.AddTransient<StartStepUpService>();
        serviceCollection.AddTransient<ValidateSessionService>();
        serviceCollection.AddTransient<VerifyEmailService>();

        // Second factors register themselves as ISecondFactor; without any, signing in needs the password only.
        serviceCollection.AddScoped<SecondFactorRegistry>();

        // The authenticator app (TOTP) with recovery codes.
        serviceCollection.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<MfaOptions>, MfaOptionsValidator>());
        serviceCollection.TryAddEnumerable(ServiceDescriptor.Scoped<ISecondFactor, TotpSecondFactor>());
        serviceCollection.AddTransient<MfaVerifier>();
        serviceCollection.AddTransient<AdministratorMfaPolicy>();
        serviceCollection.AddTransient<ConfirmTotpEnrollmentService>();
        serviceCollection.AddTransient<GetMfaStatusService>();
        serviceCollection.AddTransient<RegenerateRecoveryCodesService>();
        serviceCollection.AddTransient<RemoveTotpService>();
        serviceCollection.AddTransient<StartTotpEnrollmentService>();

        // Passkeys (WebAuthn), registered after the authenticator app so the pages list the factors in this order.
        serviceCollection.TryAddEnumerable(ServiceDescriptor.Scoped<ISecondFactor, PasskeySecondFactor>());
        serviceCollection.AddTransient<PasskeyChallengeIssuer>();
        serviceCollection.AddTransient<PasskeyChangeGuard>();
        serviceCollection.AddTransient<PasskeyVerifier>();
        serviceCollection.AddTransient<RegisterPasskeyService>();
        serviceCollection.AddTransient<RemovePasskeyService>();
        serviceCollection.AddTransient<RenamePasskeyService>();
        serviceCollection.AddTransient<StartPasskeyAuthenticationService>();
        serviceCollection.AddTransient<StartPasskeyRegistrationService>();

        serviceCollection.AddTransient<PermissionEvaluator>();
        serviceCollection.AddTransient<PrivilegeGuard>();

        serviceCollection.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<AuditChainOptions>, AuditChainOptionsValidator>());
        serviceCollection.AddTransient<SearchAuditEventsService>();
        serviceCollection.AddTransient<VerifyAuditChainService>();

        serviceCollection.AddTransient<GetAdminUiSettingsService>();

        serviceCollection.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<EmailOptions>, EmailOptionsValidator>());
        serviceCollection.AddTransient<EmailTokenIssuer>();
        serviceCollection.AddTransient<EmailTokenRedeemer>();

        serviceCollection.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<OAuthOptions>, OAuthOptionsValidator>());
        serviceCollection.AddTransient<AccessTokenValidator>();
        serviceCollection.AddTransient<ClientAuthenticator>();
        serviceCollection.AddTransient<IntrospectTokenService>();
        serviceCollection.AddTransient<IssueAuthorizationCodeService>();
        serviceCollection.AddTransient<CheckAuthenticationRequirementService>();
        serviceCollection.AddTransient<IssueTokenService>();
        serviceCollection.AddTransient<RevokeTokenService>();
        serviceCollection.AddTransient<TokenIssuer>();
        serviceCollection.AddTransient<ValidateAuthorizationRequestService>();

        serviceCollection.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<OidcOptions>, OidcOptionsValidator>());
        serviceCollection.AddTransient<GetUserInfoService>();
        serviceCollection.AddTransient<IdTokenIssuer>();

        serviceCollection.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<BackchannelLogoutOptions>, BackchannelLogoutOptionsValidator>());
        serviceCollection.AddTransient<LogoutTokenIssuer>();
        serviceCollection.AddTransient<IdTokenHintReader>();
        serviceCollection.AddTransient<ValidateEndSessionRequestService>();

        serviceCollection.AddTransient<ClientSecretIssuer>();
        serviceCollection.AddTransient<AddPostLogoutRedirectUriService>();
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
        serviceCollection.AddTransient<SetPkceOptionalService>();
        serviceCollection.AddTransient<SetMfaRequiredService>();
        serviceCollection.AddTransient<GetAllClientsService>();
        serviceCollection.AddTransient<GetClientService>();
        serviceCollection.AddTransient<RegisterClientService>();
        serviceCollection.AddTransient<RemovePostLogoutRedirectUriService>();
        serviceCollection.AddTransient<RemoveRedirectUriService>();
        serviceCollection.AddTransient<RevokeClientSecretService>();
        serviceCollection.AddTransient<UpdateClientService>();

        serviceCollection.AddTransient<CheckConsentService>();
        serviceCollection.AddTransient<ConsentRevoker>();
        serviceCollection.AddTransient<GetUserConsentsService>();
        serviceCollection.AddTransient<RememberConsentService>();
        serviceCollection.AddTransient<RevokeConsentService>();

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
        serviceCollection.AddTransient<EmailVerificationSender>();
        serviceCollection.AddTransient<GetEffectivePermissionsService>();
        serviceCollection.AddTransient<GetCurrentUserService>();
        serviceCollection.AddTransient<GetUserService>();
        serviceCollection.AddTransient<GetUserGroupsService>();
        serviceCollection.AddTransient<GetUserRolesService>();
        serviceCollection.AddTransient<GetAllUsersService>();
        serviceCollection.AddTransient<MarkEmailVerifiedService>();
        serviceCollection.AddTransient<RemoveRoleService>();
        serviceCollection.AddTransient<ResetMfaService>();
        serviceCollection.AddTransient<RemoveUserPasskeyService>();
        serviceCollection.AddTransient<RevokeUserSessionsService>();
        serviceCollection.AddTransient<SendEmailVerificationService>();
        serviceCollection.AddTransient<SendPasswordResetService>();
        serviceCollection.AddTransient<SetPasswordService>();
        serviceCollection.AddTransient<UnlockUserService>();
        serviceCollection.AddTransient<UpdateUserService>();

        return serviceCollection;
    }
}
