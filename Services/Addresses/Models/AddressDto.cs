namespace Nexus.Services.Addresses.Models;

public sealed class AddressDto
{
    public int Id { get; init; }

    public string RecipientName { get; init; } = string.Empty;

    public string Phone { get; init; } = string.Empty;

    public string AddressLine { get; init; } = string.Empty;

    public string Ward { get; init; } = string.Empty;

    public string Province { get; init; } = string.Empty;

    public string Country { get; init; } = string.Empty;

    public bool IsDefault { get; init; }

    public string? Label { get; init; }
}
