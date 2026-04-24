namespace MuseSpace.Contracts.Auth;

public sealed class CreateUserRequest
{
    public string PhoneNumber { get; set; } = string.Empty;
}
