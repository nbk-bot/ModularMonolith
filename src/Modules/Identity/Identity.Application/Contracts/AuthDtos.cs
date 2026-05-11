namespace Identity.Application.Contracts;

public sealed record RegisterRequest(string Email, string Password, string? FullName);
public sealed record LoginRequest(string Email, string Password);
public sealed record RefreshRequest(string RefreshToken);
public sealed record ForgotPasswordRequest(string Email);
public sealed record ResetPasswordRequest(string Email, string Token, string NewPassword);
public sealed record ConfirmEmailRequest(string UserId, string Token);

public sealed record AuthTokens(string AccessToken, string RefreshToken, DateTime ExpiresAt);
public sealed record UserDto(Guid Id, string Email, string? FullName, IReadOnlyList<string> Roles);
