using Microsoft.EntityFrameworkCore;
using Nexus.Data;
using Nexus.Data.Entities;
using Nexus.Services.Addresses.Models;
using Nexus.Services.Categories.Models;

namespace Nexus.Services.Addresses;

public sealed class AddressService(
    IDbContextFactory<ApplicationDbContext> dbContextFactory) : IAddressService
{
    public async Task<IReadOnlyList<AddressDto>> GetAddressesAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return [];

        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var addresses = await context.UserAddresses
            .AsNoTracking()
            .Where(a => a.UserId == userId)
            .OrderByDescending(a => a.IsDefault)
            .ThenByDescending(a => a.Id)
            .ToListAsync(cancellationToken);

        return addresses.Select(MapDto).ToList();
    }

    public async Task<AddressDto?> GetDefaultAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return null;

        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var address = await context.UserAddresses
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.UserId == userId && a.IsDefault, cancellationToken);

        return address is null ? null : MapDto(address);
    }

    public async Task<ServiceResult<AddressDto>> AddAsync(
        string userId,
        AddressInput input,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return ServiceResult<AddressDto>.Fail("You must be signed in to save an address.");

        var validationError = Validate(input);
        if (validationError is not null)
            return ServiceResult<AddressDto>.Fail(validationError);

        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        var hasExisting = await context.UserAddresses
            .AnyAsync(a => a.UserId == userId, cancellationToken);

        // The first address is always the default; otherwise honor the caller's choice.
        var makeDefault = input.SetAsDefault || !hasExisting;

        if (makeDefault)
            await ClearDefaultsAsync(context, userId, cancellationToken);

        var now = DateTime.UtcNow;

        var address = new UserAddress
        {
            UserId = userId,
            RecipientName = input.RecipientName.Trim(),
            Phone = input.Phone.Trim(),
            AddressLine = input.AddressLine.Trim(),
            Ward = input.Ward.Trim(),
            Province = input.Province.Trim(),
            Country = string.IsNullOrWhiteSpace(input.Country) ? "Vietnam" : input.Country.Trim(),
            Label = string.IsNullOrWhiteSpace(input.Label) ? null : input.Label.Trim(),
            IsDefault = makeDefault,
            CreatedAt = now,
            UpdatedAt = now
        };

        context.UserAddresses.Add(address);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return ServiceResult<AddressDto>.Ok(MapDto(address));
    }

    public async Task<ServiceResult<AddressDto>> UpdateAsync(
        string userId,
        int addressId,
        AddressInput input,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return ServiceResult<AddressDto>.Fail("You must be signed in to update an address.");

        var validationError = Validate(input);
        if (validationError is not null)
            return ServiceResult<AddressDto>.Fail(validationError);

        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        var address = await context.UserAddresses
            .FirstOrDefaultAsync(a => a.Id == addressId && a.UserId == userId, cancellationToken);

        if (address is null)
            return ServiceResult<AddressDto>.Fail("Address not found.");

        var now = DateTime.UtcNow;

        address.RecipientName = input.RecipientName.Trim();
        address.Phone = input.Phone.Trim();
        address.AddressLine = input.AddressLine.Trim();
        address.Ward = input.Ward.Trim();
        address.Province = input.Province.Trim();
        address.Country = string.IsNullOrWhiteSpace(input.Country) ? "Vietnam" : input.Country.Trim();
        address.Label = string.IsNullOrWhiteSpace(input.Label) ? null : input.Label.Trim();
        address.UpdatedAt = now;

        if (input.SetAsDefault && !address.IsDefault)
        {
            await ClearDefaultsAsync(context, userId, cancellationToken);
            address.IsDefault = true;
        }

        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return ServiceResult<AddressDto>.Ok(MapDto(address));
    }

    public async Task<ServiceResult<bool>> SetDefaultAsync(
        string userId,
        int addressId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return ServiceResult<bool>.Fail("You must be signed in to change your default address.");

        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        var owned = await context.UserAddresses
            .AnyAsync(a => a.Id == addressId && a.UserId == userId, cancellationToken);

        if (!owned)
            return ServiceResult<bool>.Fail("Address not found.");

        var now = DateTime.UtcNow;

        await ClearDefaultsAsync(context, userId, cancellationToken);

        await context.UserAddresses
            .Where(a => a.Id == addressId)
            .ExecuteUpdateAsync(
                s => s.SetProperty(a => a.IsDefault, true).SetProperty(a => a.UpdatedAt, now),
                cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return ServiceResult<bool>.Ok(true);
    }

    public async Task<ServiceResult<bool>> DeleteAsync(
        string userId,
        int addressId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return ServiceResult<bool>.Fail("You must be signed in to delete an address.");

        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        var address = await context.UserAddresses
            .FirstOrDefaultAsync(a => a.Id == addressId && a.UserId == userId, cancellationToken);

        if (address is null)
            return ServiceResult<bool>.Fail("Address not found.");

        var wasDefault = address.IsDefault;

        context.UserAddresses.Remove(address);
        await context.SaveChangesAsync(cancellationToken);

        // Keep a default present: promote the most recently updated remaining address.
        if (wasDefault)
        {
            var promote = await context.UserAddresses
                .Where(a => a.UserId == userId)
                .OrderByDescending(a => a.UpdatedAt)
                .ThenByDescending(a => a.Id)
                .FirstOrDefaultAsync(cancellationToken);

            if (promote is not null)
            {
                promote.IsDefault = true;
                promote.UpdatedAt = DateTime.UtcNow;
                await context.SaveChangesAsync(cancellationToken);
            }
        }

        await transaction.CommitAsync(cancellationToken);

        return ServiceResult<bool>.Ok(true);
    }

    private static Task ClearDefaultsAsync(
        ApplicationDbContext context,
        string userId,
        CancellationToken cancellationToken) =>
        context.UserAddresses
            .Where(a => a.UserId == userId && a.IsDefault)
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.IsDefault, false), cancellationToken);

    private static string? Validate(AddressInput input)
    {
        if (string.IsNullOrWhiteSpace(input.RecipientName))
            return "Recipient name is required.";

        if (string.IsNullOrWhiteSpace(input.Phone))
            return "Phone number is required.";

        if (string.IsNullOrWhiteSpace(input.AddressLine))
            return "Street address is required.";

        if (string.IsNullOrWhiteSpace(input.Ward))
            return "Ward / commune is required.";

        if (string.IsNullOrWhiteSpace(input.Province))
            return "Province / city is required.";

        if (string.IsNullOrWhiteSpace(input.Country))
            return "Country is required.";

        return null;
    }

    private static AddressDto MapDto(UserAddress a) => new()
    {
        Id = a.Id,
        RecipientName = a.RecipientName,
        Phone = a.Phone,
        AddressLine = a.AddressLine,
        Ward = a.Ward,
        Province = a.Province,
        Country = a.Country,
        IsDefault = a.IsDefault,
        Label = a.Label
    };
}
