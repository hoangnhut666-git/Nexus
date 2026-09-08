namespace Nexus.Data;

public class IdentitySettings
{
    public const string SectionName = "IdentitySettings";

    public bool RequireConfirmedAccount { get; set; } = false;

    public string AdminEmail { get; set; } = "admin@nexus.io";

    public string AdminPassword { get; set; } = "";

    public string AdminFullName { get; set; } = "Nexus Administrator";
}
