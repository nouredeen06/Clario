namespace Clario.Enums;

public enum AuthError
{
    InvalidCredentials,
    EmailAlreadyExists,
    EmailNotConfirmed,
    WeakPassword,
    InvalidEmail,
    SignupDisabled,
    RateLimited,
    SessionExpired,
    Unknown
}