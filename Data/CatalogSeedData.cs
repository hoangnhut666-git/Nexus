using Microsoft.EntityFrameworkCore;
using Nexus.Data.Entities;
using Nexus.Services.Products;

namespace Nexus.Data;

/// <summary>
/// Seeds the product catalog (categories, products, options and variants) so that the
/// storefront always has meaningful data, even after switching to a fresh database.
/// Full catalog insert is idempotent (only when no products exist). Product gallery
/// images are then backfilled from wwwroot/images/catalog/{slug}.png when a product
/// has no images or only Picsum placeholders.
/// </summary>
public static class CatalogSeedData
{
    private const string CatalogImageRoot = "/images/catalog";

    // Rotating stock levels so seeded variants look realistic instead of uniform.
    private static readonly int[] StockPattern = [42, 25, 60, 15, 30, 48, 8, 55];

    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
        await using var context = await factory.CreateDbContextAsync();

        if (!await context.Products.AnyAsync())
        {
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

                foreach (var image in BuildCatalogImages(slug, spec.Name, spec.ImageFiles))
                {
                    product.Images.Add(image);
                }

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
                        StockQuantity = StockPattern[stockIndex++ % StockPattern.Length] + 2000,
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

        await EnsureLocalProductImagesAsync(context);
        await context.SaveChangesAsync();
    }

    private static List<Category> BuildCategories(DateTime now)
    {
        var definitions = new (string Name, string Slug, string Description)[]
        {
            ("Smartphones", "smartphones", "Premium smartphones with top-tier camera technology and performance."),
            ("Laptops", "laptops", "Thin, light, premium and powerful laptops for work and entertainment."),
            ("Headphones", "headphones", "Ultimate audio experience with active noise-canceling wireless earbuds."),
            ("Wearables", "wearables", "Smartwatches and fitness trackers for comprehensive health monitoring."),
            ("Accessories", "accessories", "Super-fast chargers, connection cables, and genuine power banks.")
        };

        return definitions
            .Select(d =>
            {
                return new Category
                {
                    Name = d.Name,
                    Slug = d.Slug,
                    Description = d.Description,
                    ImageUrl = $"https://picsum.photos/seed/{d.Slug}/600/400",
                    IsActive = true,
                    CreatedAt = now,
                    UpdatedAt = now
                };
            })
            .ToList();
    }

    private static IReadOnlyList<SeedProduct> BuildProductSpecs() =>
    [
        // Smartphones
        new("smartphones", "Huawei P60 Pro",
            "Top-tier photography smartphone with XMAGE camera, unique pearl texture design, and a 120Hz LTPO curved display.",
            899.90m,
            [
                new("Color", ["Rococo Pearl", "Black", "Green"]),
                new("Storage", ["256GB", "512GB"])
            ],
            ["huawei-p60-pro.png"]),
        new("smartphones", "Honor Magic 6 Pro",
            "Premium flagship featuring Snapdragon 8 Gen 3, next-gen silicon-carbon battery, and a 180MP periscope telephoto camera.",
            999.90m,
            [
                new("Color", ["Epi Green", "Black"]),
                new("Storage", ["256GB", "512GB", "1TB"])
            ],
            ["honor-magic-6-pro.png"]),
        new("smartphones", "Huawei Mate 60 Pro",
            "Advanced satellite communication, exclusive Kirin processor, and an ultra-durable drop-resistant build.",
            1099.90m,
            [
                new("Color", ["Silver", "Black", "Cyan"]),
                new("Storage", ["512GB", "1TB"])
            ],
            ["huawei-mate-60-pro.png"]),

        // Laptops
        new("laptops", "Huawei MateBook X Pro",
            "Ultra-light laptop at just 1.26kg, magnesium alloy body, 3.1K Real Color display, and Intel Core i7 processor.",
            1499.90m,
            [
                new("Color", ["Ink Blue", "Space Gray"]),
                new("RAM", ["16GB", "32GB"]),
                new("Storage", ["1TB SSD"])
            ],
            ["huawei-matebook-x-pro.png"]),
        new("laptops", "Honor MagicBook 14",
            "Outstanding performance with dual cooling systems, all-day 75Wh battery, and a 2.5K eye-comfort display.",
            849.90m,
            [
                new("Color", ["Silver", "Space Gray"]),
                new("RAM", ["16GB"]),
                new("Storage", ["512GB SSD", "1TB SSD"])
            ],
            ["honor-magicbook-14.png"]),

        // Headphones
        new("headphones", "Huawei FreeBuds Pro 3",
            "TWS earbuds with ANC 3.0, supporting high-resolution L2HC and LDAC audio.",
            199.90m,
            [
                new("Color", ["Silver Frost", "Ceramic White", "Green"])
            ],
            ["huawei-freebuds-pro-3.png"]),
        new("headphones", "Honor Earbuds 3 Pro",
            "Featuring the world's first coaxial dual-driver design, ultra-lightweight build, and a built-in body temperature sensor.",
            169.90m,
            [
                new("Color", ["White", "Gray"])
            ],
            ["honor-earbuds-3-pro.png"]),
        new("headphones", "Huawei FreeClip",
            "Unique open-ear clip design, providing maximum comfort for all-day wear while keeping you aware of your surroundings.",
            189.90m,
            [
                new("Color", ["Purple", "Black"])
            ],
            ["huawei-freeclip.png"]),

        // Wearables
        new("wearables", "Huawei Watch GT 4",
            "Trendy octagonal smartwatch with TruSeen 5.5+ heart rate monitoring and up to 14 days of battery life.",
            249.90m,
            [
                new("Size", ["41mm", "46mm"]),
                new("Color / Strap", ["Black (Rubber)", "Silver (Steel)", "Brown (Leather)"])
            ],
            ["huawei-watch-gt-4.png"]),
        new("wearables", "Honor Watch 4",
            "Sporty smartwatch with a smooth 1.75-inch AMOLED display and Bluetooth calling support.",
            149.90m,
            [
                new("Color", ["Black", "Gold"])
            ],
            ["honor-watch-4.png"]),

        // Accessories
        new("accessories", "Huawei 88W SuperCharge",
            "Compact super-fast charger supporting multiple protocols (PD, PPS), suitable for both laptops and smartphones.",
            39.90m,
            [
                new("Color", ["White"])
            ],
            ["huawei-88w-supercharge.png"]),
        new("accessories", "Honor 10000mAh Power Bank",
            "Premium aluminum alloy power bank supporting safe 22.5W two-way fast charging.",
            29.90m,
            [
                new("Color", ["Black", "Silver"])
            ],
            ["honor-10000mah-power-bank.png"])
    ];

    private static async Task EnsureLocalProductImagesAsync(ApplicationDbContext context)
    {
        var specsBySlug = BuildProductSpecs().ToDictionary(
            spec => ProductSlugHelper.GenerateSlug(spec.Name),
            spec => spec,
            StringComparer.Ordinal);

        var products = await context.Products
            .Include(p => p.Images)
            .ToListAsync();

        foreach (var product in products)
        {
            if (!specsBySlug.TryGetValue(product.Slug, out var spec))
            {
                continue;
            }

            var existing = product.Images.ToList();
            var needsReplace = existing.Count == 0
                || existing.All(i => IsPicsumUrl(i.ImageUrl));

            if (!needsReplace)
            {
                continue;
            }

            if (existing.Count > 0)
            {
                context.ProductImages.RemoveRange(existing);
            }

            foreach (var image in BuildCatalogImages(product.Slug, product.Name, spec.ImageFiles))
            {
                product.Images.Add(image);
            }
        }
    }

    private static IReadOnlyList<ProductImage> BuildCatalogImages(
        string slug,
        string productName,
        string[] imageFiles)
    {
        var files = imageFiles.Length > 0 ? imageFiles : [$"{slug}.png"];

        return files
            .Select((file, index) => new ProductImage
            {
                ImageUrl = $"{CatalogImageRoot}/{file.TrimStart('/')}",
                SortOrder = index,
                AltText = productName
            })
            .ToList();
    }

    private static bool IsPicsumUrl(string? url) =>
        !string.IsNullOrWhiteSpace(url)
        && url.Contains("picsum.photos", StringComparison.OrdinalIgnoreCase);

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
        SeedOption[] Options,
        string[] ImageFiles);

    private sealed record SeedOption(string Name, string[] Values);
}
