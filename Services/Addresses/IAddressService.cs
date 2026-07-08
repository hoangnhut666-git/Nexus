using Nexus.Services.Addresses.Models;
using Nexus.Services.Categories.Models;

namespace Nexus.Services.Addresses;

public interface IAddressService
{
    Task<IReadOnlyList<AddressDto>> GetAddressesAsync(
        string userId,
        CancellationToken cancellationToken = default);

    Task<AddressDto?> GetDefaultAsync(
        string userId,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<AddressDto>> AddAsync(
        string userId,
        AddressInput input,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<AddressDto>> UpdateAsync(
        string userId,
        int addressId,
        AddressInput input,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<bool>> SetDefaultAsync(
        string userId,
        int addressId,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<bool>> DeleteAsync(
        string userId,
        int addressId,
        CancellationToken cancellationToken = default);
}
