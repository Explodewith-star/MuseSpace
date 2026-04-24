namespace MuseSpace.Domain.Entities;

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string PhoneNumber { get; set; } = string.Empty;
    /// <summary>User | Admin</summary>
    public string Role { get; set; } = "User";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginAt { get; set; }
}
