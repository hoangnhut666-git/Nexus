using Microsoft.EntityFrameworkCore;
using Nexus.Data;
using Nexus.Data.Entities;
using Nexus.Services.Cart.Models;
using Nexus.Services.Categories.Models;

namespace Nexus.Services.Cart;

public sealed class CartService(
    IDbContextFactory<ApplicationDbContext> dbContextFactory) : ICartService
{
    public async Task<CartDto> GetCartAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await BuildCartAsync(context, userId, cancellationToken);
    }

    public async Task<int> GetItemCountAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return 0;

        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        return await context.CartItems
            .AsNoTracking()
            .Where(ci => ci.UserId == userId)
            .SumAsync(ci => (int?)ci.Quantity, cancellationToken) ?? 0;
    }

    public async Task<ServiceResult<CartDto>> AddItemAsync(
        string userId,
        AddToCartRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return ServiceResult<CartDto>.Fail("You must be signed in to use the cart.");

        if (request.Quantity < 1)
            return ServiceResult<CartDto>.Fail("Quantity must be at least one.");

        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var variant = await context.ProductVariants
            .Include(v => v.Product)
            .FirstOrDefaultAsync(v => v.Id == request.ProductVariantId, cancellationToken);

        if (variant is null)
            return ServiceResult<CartDto>.Fail("Product variant not found.");

        if (!variant.IsActive || !variant.Product.IsActive)
            return ServiceResult<CartDto>.Fail("This product is not available.");

        if (variant.StockQuantity <= 0)
            return ServiceResult<CartDto>.Fail("This product is out of stock.");

        var existing = await context.CartItems
            .FirstOrDefaultAsync(
                ci => ci.UserId == userId && ci.ProductVariantId == request.ProductVariantId,
                cancellationToken);

        var targetQuantity = (existing?.Quantity ?? 0) + request.Quantity;

        if (targetQuantity > variant.StockQuantity)
            return ServiceResult<CartDto>.Fail($"Only {variant.StockQuantity} in stock.");

        var now = DateTime.UtcNow;

        if (existing is null)
        {
            context.CartItems.Add(new CartItem
            {
                UserId = userId,
                ProductVariantId = request.ProductVariantId,
                Quantity = request.Quantity,
                CreatedAt = now,
                UpdatedAt = now
            });
        }
        else
        {
            existing.Quantity = targetQuantity;
            existing.UpdatedAt = now;
        }

        await context.SaveChangesAsync(cancellationToken);

        return ServiceResult<CartDto>.Ok(await BuildCartAsync(context, userId, cancellationToken));
    }

    public async Task<ServiceResult<CartDto>> UpdateItemQuantityAsync(
        string userId,
        int cartItemId,
        int quantity,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return ServiceResult<CartDto>.Fail("You must be signed in to use the cart.");

        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var item = await context.CartItems
            .Include(ci => ci.ProductVariant)
            .FirstOrDefaultAsync(ci => ci.Id == cartItemId && ci.UserId == userId, cancellationToken);

        if (item is null)
            return ServiceResult<CartDto>.Fail("Cart item not found.");

        if (quantity <= 0)
        {
            context.CartItems.Remove(item);
            await context.SaveChangesAsync(cancellationToken);
            return ServiceResult<CartDto>.Ok(await BuildCartAsync(context, userId, cancellationToken));
        }

        if (quantity > item.ProductVariant.StockQuantity)
            return ServiceResult<CartDto>.Fail($"Only {item.ProductVariant.StockQuantity} in stock.");

        item.Quantity = quantity;
        item.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync(cancellationToken);

        return ServiceResult<CartDto>.Ok(await BuildCartAsync(context, userId, cancellationToken));
    }

    public async Task<ServiceResult<CartDto>> RemoveItemAsync(
        string userId,
        int cartItemId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return ServiceResult<CartDto>.Fail("You must be signed in to use the cart.");

        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var item = await context.CartItems
            .FirstOrDefaultAsync(ci => ci.Id == cartItemId && ci.UserId == userId, cancellationToken);

        if (item is null)
            return ServiceResult<CartDto>.Fail("Cart item not found.");

        context.CartItems.Remove(item);
        await context.SaveChangesAsync(cancellationToken);

        return ServiceResult<CartDto>.Ok(await BuildCartAsync(context, userId, cancellationToken));
    }

    public async Task<ServiceResult<bool>> ClearAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return ServiceResult<bool>.Fail("You must be signed in to use the cart.");

        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        await context.CartItems
            .Where(ci => ci.UserId == userId)
            .ExecuteDeleteAsync(cancellationToken);

        return ServiceResult<bool>.Ok(true);
    }

    private static async Task<CartDto> BuildCartAsync(
        ApplicationDbContext context,
        string userId,
        CancellationToken cancellationToken)
    {
        var items = await context.CartItems
            .AsNoTracking()
            .Where(ci => ci.UserId == userId)
            .Include(ci => ci.ProductVariant)
                .ThenInclude(v => v.Product)
                    .ThenInclude(p => p.Images)
            .Include(ci => ci.ProductVariant)
                .ThenInclude(v => v.OptionValues)
                    .ThenInclude(ov => ov.ProductOptionValue)
                        .ThenInclude(pov => pov.ProductOption)
            .OrderByDescending(ci => ci.UpdatedAt)
            .ThenByDescending(ci => ci.Id)
            .ToListAsync(cancellationToken);

        var lines = items.Select(MapLine).ToList();

        return CartPricing.BuildCart(lines);
    }

    private static CartLineDto MapLine(CartItem item)
    {
        var variant = item.ProductVariant;
        var product = variant.Product;

        var label = string.Join(" / ", variant.OptionValues
            .OrderBy(ov => ov.ProductOptionValue.ProductOption.SortOrder)
            .ThenBy(ov => ov.ProductOptionValue.ProductOptionId)
            .Select(ov => ov.ProductOptionValue.Value));

        var imageUrl = variant.ImageUrl ?? product.Images
            .OrderBy(i => i.SortOrder)
            .ThenBy(i => i.Id)
            .Select(i => i.ImageUrl)
            .FirstOrDefault();

        return new CartLineDto
        {
            CartItemId = item.Id,
            ProductVariantId = variant.Id,
            ProductId = product.Id,
            ProductName = product.Name,
            Slug = product.Slug,
            VariantLabel = label,
            Sku = variant.Sku,
            ImageUrl = imageUrl,
            UnitPrice = variant.Price,
            Quantity = item.Quantity,
            StockQuantity = variant.StockQuantity
        };
    }
}
