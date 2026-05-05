

#nullable enable

namespace Qyl.OpenTelemetry.SemanticConventions.Incubating.Attributes.Aspnetcore;

public static class AspnetcoreAttributes
{
    public const string AuthenticationResult = "aspnetcore.authentication.result";

    public static class AuthenticationResultValues
    {
        public const string Failure = "failure";

        public const string None = "none";

        public const string Success = "success";
    }

    public const string AuthenticationScheme = "aspnetcore.authentication.scheme";

    public const string AuthorizationPolicy = "aspnetcore.authorization.policy";

    public const string AuthorizationResult = "aspnetcore.authorization.result";

    public static class AuthorizationResultValues
    {
        public const string Failure = "failure";

        public const string Success = "success";
    }

    public const string IdentityErrorCode = "aspnetcore.identity.error_code";

    public const string IdentityPasswordCheckResult = "aspnetcore.identity.password_check_result";

    public static class IdentityPasswordCheckResultValues
    {
        public const string Failure = "failure";

        public const string PasswordMissing = "password_missing";

        public const string Success = "success";

        public const string SuccessRehashNeeded = "success_rehash_needed";

        public const string UserMissing = "user_missing";
    }

    public const string IdentityResult = "aspnetcore.identity.result";

    public static class IdentityResultValues
    {
        public const string Failure = "failure";

        public const string Success = "success";
    }

    public const string IdentitySignInResult = "aspnetcore.identity.sign_in.result";

    public static class IdentitySignInResultValues
    {
        public const string Failure = "failure";

        public const string LockedOut = "locked_out";

        public const string NotAllowed = "not_allowed";

        public const string RequiresTwoFactor = "requires_two_factor";

        public const string Success = "success";
    }

    public const string IdentitySignInType = "aspnetcore.identity.sign_in.type";

    public static class IdentitySignInTypeValues
    {
        public const string External = "external";

        public const string Passkey = "passkey";

        public const string Password = "password";

        public const string TwoFactor = "two_factor";

        public const string TwoFactorAuthenticator = "two_factor_authenticator";

        public const string TwoFactorRecoveryCode = "two_factor_recovery_code";
    }

    public const string IdentityTokenPurpose = "aspnetcore.identity.token_purpose";

    public static class IdentityTokenPurposeValues
    {
        public const string Other = "_OTHER";

        public const string ChangeEmail = "change_email";

        public const string ChangePhoneNumber = "change_phone_number";

        public const string EmailConfirmation = "email_confirmation";

        public const string ResetPassword = "reset_password";

        public const string TwoFactor = "two_factor";
    }

    public const string IdentityTokenVerified = "aspnetcore.identity.token_verified";

    public static class IdentityTokenVerifiedValues
    {
        public const string Failure = "failure";

        public const string Success = "success";
    }

    public const string IdentityUserUpdateType = "aspnetcore.identity.user.update_type";

    public static class IdentityUserUpdateTypeValues
    {
        public const string Other = "_OTHER";

        public const string AccessFailed = "access_failed";

        public const string AddClaims = "add_claims";

        public const string AddLogin = "add_login";

        public const string AddPassword = "add_password";

        public const string AddToRoles = "add_to_roles";

        public const string ChangeEmail = "change_email";

        public const string ChangePassword = "change_password";

        public const string ChangePhoneNumber = "change_phone_number";

        public const string ConfirmEmail = "confirm_email";

        public const string GenerateNewTwoFactorRecoveryCodes = "generate_new_two_factor_recovery_codes";

        public const string PasswordRehash = "password_rehash";

        public const string RedeemTwoFactorRecoveryCode = "redeem_two_factor_recovery_code";

        public const string RemoveAuthenticationToken = "remove_authentication_token";

        public const string RemoveClaims = "remove_claims";

        public const string RemoveFromRoles = "remove_from_roles";

        public const string RemoveLogin = "remove_login";

        public const string RemovePasskey = "remove_passkey";

        public const string RemovePassword = "remove_password";

        public const string ReplaceClaim = "replace_claim";

        public const string ResetAccessFailedCount = "reset_access_failed_count";

        public const string ResetAuthenticatorKey = "reset_authenticator_key";

        public const string ResetPassword = "reset_password";

        public const string SecurityStamp = "security_stamp";

        public const string SetAuthenticationToken = "set_authentication_token";

        public const string SetEmail = "set_email";

        public const string SetLockoutEnabled = "set_lockout_enabled";

        public const string SetLockoutEndDate = "set_lockout_end_date";

        public const string SetPasskey = "set_passkey";

        public const string SetPhoneNumber = "set_phone_number";

        public const string SetTwoFactorEnabled = "set_two_factor_enabled";

        public const string Update = "update";

        public const string UserName = "user_name";
    }

    public const string IdentityUserType = "aspnetcore.identity.user_type";

    public const string MemoryPoolOwner = "aspnetcore.memory_pool.owner";

    public const string SignInIsPersistent = "aspnetcore.sign_in.is_persistent";
}
