namespace OIDC_test3.Dto;

public record LoginResponseDto(string RedirectUrl);

public record CallbackResponseDto(
    Guid SessionId,
    string AccessToken,
    int ExpiresIn,
    string RefreshToken,
    string IdToken,
    UserInfoDto User);

public record UserInfoDto(
    string Sub,
    string? UserName,
    string? Email,
    string? GivenName,
    string? FamilyName,
    string? MiddleName);

public record RefreshRequestDto(Guid SessionId);

public record RefreshResponseDto(
    string AccessToken,
    int ExpiresIn,
    string RefreshToken,
    string IdToken);

public record LogoutRequestDto(Guid SessionId);

public record SessionInfoDto(
    Guid SessionId,
    bool IsActive,
    DateTime? AccessTokenExpiresAt,
    UserInfoDto User);

public record IntrospectRequestDto(string AccessToken);
