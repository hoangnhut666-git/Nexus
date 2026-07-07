namespace Nexus.Services.Products.Models;

public sealed class GenerateVariantsRequest
{
    public decimal DefaultPrice { get; set; }

    public int DefaultStock { get; set; }

    public bool DefaultIsActive { get; set; } = true;

    public string? SkuPrefix { get; set; }
}
