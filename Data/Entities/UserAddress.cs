namespace Nexus.Data.Entities;

public class UserAddress
{
    public int Id { get; set; }

    public string UserId { get; set; } = string.Empty;

    public string RecipientName { get; set; } = string.Empty;

    public string Phone { get; set; } = string.Empty;

    // House number + street, or hamlet detail (e.g. "No. 25, Nguyễn Trãi Street").
    public string AddressLine { get; set; } = string.Empty;

    // Ward / commune (e.g. "Tân An").
    public string Ward { get; set; } = string.Empty;

    // Province / city (e.g. "Cần Thơ").
    public string Province { get; set; } = string.Empty;

    public string Country { get; set; } = "Vietnam";

    public bool IsDefault { get; set; }

    // Optional user label such as "Home" or "Office".
    public string? Label { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}
