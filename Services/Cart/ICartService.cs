using Nexus.Services.Cart.Models;
using Nexus.Services.Categories.Models;

namespace Nexus.Services.Cart;

public interface ICartService
{
    Task<CartDto> GetCartAsync(
        string userId,
        CancellationToken cancellationToken = default);

    Task<int> GetItemCountAsync(
        string userId,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<CartDto>> AddItemAsync(
        string userId,
        AddToCartRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<CartDto>> UpdateItemQuantityAsync(
        string userId,
        int cartItemId,
        int quantity,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<CartDto>> RemoveItemAsync(
        string userId,
        int cartItemId,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<bool>> ClearAsync(
        string userId,
        CancellationToken cancellationToken = default);
}
