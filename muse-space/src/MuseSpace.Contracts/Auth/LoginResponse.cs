namespace MuseSpace.Contracts.Auth;

public sealed class LoginResponse
{
    public string Token { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public Guid UserId { get; set; }
    public string PhoneNumber { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
}
