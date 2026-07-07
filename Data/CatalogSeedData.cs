using Microsoft.EntityFrameworkCore;
using Nexus.Data.Entities;
using Nexus.Services.Products;

namespace Nexus.Data;

/// <summary>
/// Seeds the product catalog (categories, products, options and variants) so that the
/// storefront always has meaningful data, even after switching to a fresh database.
/// The seeder is idempotent: it only runs when no products exist yet.
/// </summary>
public static class CatalogSeedData
{
    // Rotating stock levels so seeded variants look realistic instead of uniform.
    private static readonly int[] StockPattern = [42, 25, 60, 15, 30, 48, 8, 55];

    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
        await using var context = await factory.CreateDbContextAsync();

        if (await context.Products.AnyAsync())
        {
            return;
        }

        var now = DateTime.UtcNow;

        var categories = BuildCategories(now);
        var categoriesBySlug = categories.ToDictionary(c => c.Slug, StringComparer.Ordinal);
        context.Categories.AddRange(categories);

        var stockIndex = 0;
        foreach (var spec in BuildProductSpecs())
        {
            var category = categoriesBySlug[spec.CategorySlug];
            var slug = ProductSlugHelper.GenerateSlug(spec.Name);

            var product = new Product
            {
                Name = spec.Name,
                Slug = slug,
                Description = spec.Description,
                Category = category,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            };

            product.Images.Add(new ProductImage
            {
                ImageUrl = $"https://picsum.photos/seed/{slug}/800/800",
                SortOrder = 0,
                AltText = spec.Name
            });

            // Build options and keep the created value entities so variants can reference them.
            var valuesPerOption = new List<List<ProductOptionValue>>();
            for (var optionIndex = 0; optionIndex < spec.Options.Length; optionIndex++)
            {
                var optionSpec = spec.Options[optionIndex];
                var option = new ProductOption
                {
                    Name = optionSpec.Name,
                    SortOrder = optionIndex
                };

                var createdValues = new List<ProductOptionValue>();
                for (var valueIndex = 0; valueIndex < optionSpec.Values.Length; valueIndex++)
                {
                    var value = new ProductOptionValue
                    {
                        Value = optionSpec.Values[valueIndex],
                        SortOrder = valueIndex
                    };
                    option.Values.Add(value);
                    createdValues.Add(value);
                }

                product.Options.Add(option);
                valuesPerOption.Add(createdValues);
            }

            foreach (var combination in CartesianProduct(valuesPerOption))
            {
                var labels = combination.Select(v => v.Value).ToList();
                var sku = ProductSkuHelper.GenerateSku(slug, labels);

                product.Variants.Add(new ProductVariant
                {
                    Sku = sku,
                    Price = spec.BasePrice,
                    StockQuantity = StockPattern[stockIndex++ % StockPattern.Length],
                    IsActive = true,
                    OptionValues = combination
                        .Select(v => new VariantOptionValue { ProductOptionValue = v })
                        .ToList()
                });
            }

            context.Products.Add(product);
        }

        await context.SaveChangesAsync();
    }

    private static List<Category> BuildCategories(DateTime now)
    {
        var definitions = new (string Name, string Description)[]
        {
            ("Apparel", "Everyday clothing crafted from comfortable, durable fabrics."),
            ("Footwear", "Shoes and boots for work, sport and casual wear."),
            ("Electronics", "Smart gadgets and audio gear for modern living."),
            ("Accessories", "Finishing touches from wallets to sunglasses."),
            ("Home & Living", "Practical and stylish essentials for your home.")
        };

        return definitions
            .Select(d =>
            {
                var slug = ProductSlugHelper.GenerateSlug(d.Name);
                return new Category
                {
                    Name = d.Name,
                    Slug = slug,
                    Description = d.Description,
                    ImageUrl = $"https://picsum.photos/seed/{slug}/600/400",
                    IsActive = true,
                    CreatedAt = now,
                    UpdatedAt = now
                };
            })
            .ToList();
    }

    private static IReadOnlyList<SeedProduct> BuildProductSpecs() =>
    [
        // Apparel
        new("apparel", "Classic Cotton T-Shirt",
            "A breathable everyday tee made from 100% combed cotton with a relaxed fit.",
            19.90m,
            [
                new("Color", ["Black", "White", "Navy"]),
                new("Size", ["S", "M", "L", "XL"])
            ]),
        new("apparel", "Slim Fit Denim Jeans",
            "Mid-rise stretch denim with a tapered leg for a modern silhouette.",
            49.90m,
            [
                new("Color", ["Blue", "Black"]),
                new("Size", ["30", "32", "34", "36"])
            ]),
        new("apparel", "Pullover Hooded Sweatshirt",
            "Soft fleece-lined hoodie with a kangaroo pocket and adjustable drawstring.",
            39.90m,
            [
                new("Color", ["Gray", "Black"]),
                new("Size", ["S", "M", "L", "XL"])
            ]),
        new("apparel", "Linen Summer Dress",
            "Lightweight breathable linen dress perfect for warm-weather days.",
            54.90m,
            [
                new("Color", ["Beige", "Rose"]),
                new("Size", ["S", "M", "L"])
            ]),

        // Footwear
        new("footwear", "Everyday Running Sneakers",
            "Cushioned running shoes with a breathable mesh upper and grippy outsole.",
            79.90m,
            [
                new("Color", ["White", "Black"]),
                new("Size", ["7", "8", "9", "10", "11"])
            ]),
        new("footwear", "Leather Chelsea Boots",
            "Timeless ankle boots in genuine leather with elastic side panels.",
            119.90m,
            [
                new("Color", ["Brown", "Black"]),
                new("Size", ["8", "9", "10", "11"])
            ]),
        new("footwear", "Canvas Slip-On Shoes",
            "Casual low-profile slip-ons with a vulcanized rubber sole.",
            34.90m,
            [
                new("Color", ["Navy", "Red"]),
                new("Size", ["7", "8", "9", "10"])
            ]),
        new("footwear", "Trail Hiking Shoes",
            "Rugged hiking shoes with a waterproof membrane and aggressive tread.",
            94.90m,
            [
                new("Color", ["Green", "Gray"]),
                new("Size", ["8", "9", "10", "11"])
            ]),

        // Electronics
        new("electronics", "Wireless Noise-Cancelling Headphones",
            "Over-ear headphones with active noise cancellation and 30-hour battery life.",
            149.90m,
            [
                new("Color", ["Black", "Silver"])
            ]),
        new("electronics", "Smart Fitness Watch",
            "Tracks heart rate, sleep and workouts with a bright always-on display.",
            129.90m,
            [
                new("Color", ["Black", "Rose Gold"]),
                new("Band", ["Silicone", "Leather"])
            ]),
        new("electronics", "Portable Bluetooth Speaker",
            "Compact waterproof speaker with rich bass and 12 hours of playback.",
            59.90m,
            [
                new("Color", ["Charcoal", "Teal"])
            ]),
        new("electronics", "Mechanical Keyboard",
            "Hot-swappable mechanical keyboard with per-key RGB backlighting.",
            89.90m,
            [
                new("Switch", ["Red", "Brown", "Blue"])
            ]),

        // Accessories
        new("accessories", "Leather Bifold Wallet",
            "Slim full-grain leather wallet with RFID-blocking card slots.",
            29.90m,
            [
                new("Color", ["Brown", "Black"])
            ]),
        new("accessories", "Aviator Sunglasses",
            "Classic aviator frames with polarized UV400 lenses.",
            24.90m,
            [
                new("Color", ["Gold", "Silver"])
            ]),
        new("accessories", "Canvas Backpack",
            "Durable water-resistant backpack with a padded laptop compartment.",
            44.90m,
            [
                new("Color", ["Khaki", "Black"])
            ]),
        new("accessories", "Wool Blend Scarf",
            "Soft and warm wool-blend scarf with a fringed finish.",
            22.90m,
            [
                new("Color", ["Charcoal", "Camel", "Burgundy"])
            ]),

        // Home & Living
        new("home-living", "Ceramic Coffee Mug",
            "Dishwasher-safe glazed ceramic mug with a comfortable handle.",
            12.90m,
            [
                new("Color", ["White", "Black", "Blue"]),
                new("Size", ["11oz", "15oz"])
            ]),
        new("home-living", "Scented Soy Candle",
            "Hand-poured soy wax candle with a 45-hour clean burn.",
            18.90m,
            [
                new("Scent", ["Vanilla", "Lavender", "Sandalwood"])
            ]),
        new("home-living", "Cotton Bath Towel Set",
            "Ultra-absorbent 100% cotton towel set that stays soft wash after wash.",
            34.90m,
            [
                new("Color", ["White", "Gray", "Navy"])
            ]),
        new("home-living", "Stainless Steel Water Bottle",
            "Double-walled insulated bottle that keeps drinks cold for 24 hours.",
            27.90m,
            [
                new("Color", ["Silver", "Black", "Mint"]),
                new("Size", ["500ml", "750ml"])
            ]),
    ];

    private static IEnumerable<List<ProductOptionValue>> CartesianProduct(
        IReadOnlyList<List<ProductOptionValue>> sets)
    {
        if (sets.Count == 0)
        {
            yield return [];
            yield break;
        }

        foreach (var head in sets[0])
        {
            if (sets.Count == 1)
            {
                yield return [head];
                continue;
            }

            foreach (var tail in CartesianProduct(sets.Skip(1).ToList()))
            {
                var combo = new List<ProductOptionValue> { head };
                combo.AddRange(tail);
                yield return combo;
            }
        }
    }

    private sealed record SeedProduct(
        string CategorySlug,
        string Name,
        string Description,
        decimal BasePrice,
        SeedOption[] Options);

    private sealed record SeedOption(string Name, string[] Values);
}
