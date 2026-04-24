namespace MuseSpace.Contracts.Auth;

public sealed class AdminLoginRequest
{
    public string PhoneNumber { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}
