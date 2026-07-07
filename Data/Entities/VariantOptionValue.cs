namespace Nexus.Data.Entities;

public class VariantOptionValue
{
    public int ProductVariantId { get; set; }

    public int ProductOptionValueId { get; set; }

    public ProductVariant ProductVariant { get; set; } = null!;

    public ProductOptionValue ProductOptionValue { get; set; } = null!;
}
