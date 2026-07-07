namespace Nexus.Components.Admin.Shared;

public enum AdminToastType
{
    Info,
    Success,
    Warning,
    Danger
}

public sealed class AdminToastMessage
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public string Title { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public AdminToastType Type { get; init; } = AdminToastType.Info;
}
