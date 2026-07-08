namespace Nexus.Services.Addresses.Models;

public sealed class AddressInput
{
    public string RecipientName { get; init; } = string.Empty;

    public string Phone { get; init; } = string.Empty;

    public string AddressLine { get; init; } = string.Empty;

    public string Ward { get; init; } = string.Empty;

    public string Province { get; init; } = string.Empty;

    public string Country { get; init; } = "Vietnam";

    public bool SetAsDefault { get; init; }

    public string? Label { get; init; }
}
