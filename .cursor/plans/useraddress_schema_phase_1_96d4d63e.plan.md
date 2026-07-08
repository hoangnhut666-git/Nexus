---
name: UserAddress Schema Phase 1
overview: Add a UserAddress entity, its EF Core configuration, and an AddUserAddresses migration so the app has a per-user address-book table. This is Phase 1 of the address-management roadmap; no UI or service logic yet.
todos:
  - id: entity
    content: Create Data/Entities/UserAddress.cs with VN-native fields (RecipientName, Phone, AddressLine, Ward, Province, Country, IsDefault, Label, timestamps)
    status: completed
  - id: dbcontext
    content: Add DbSet<UserAddress> and Fluent config block in ApplicationDbContext.OnModelCreating (max lengths, required, UserId index, cascade FK, Country default), mirroring CartItem
    status: completed
  - id: migration
    content: Scaffold AddUserAddresses migration into Data/Migrations (dotnet ef migrations add AddUserAddresses -o Data/Migrations) and apply it
    status: completed
  - id: dbhelper
    content: Add GetAddressesAsync helper to Nexus.Test.Integration/Infrastructure/DbHelper.cs
    status: completed
isProject: false
---

## Phase 1 — UserAddress schema

Adds the persistence layer for the address book: one entity, its Fluent config, a migration, and a test helper. No service or UI work (those are Phases 2-4).

### Context / baseline (verified)
- Migrations now live in a single folder: [Data/Migrations/](Data/Migrations/), namespace `Nexus.Data.Migrations`, baseline `20260707180842_Init.cs` (full squashed schema) + current `ApplicationDbContextModelSnapshot.cs`. New migrations must be scaffolded here with `-o Data/Migrations`.
- The existing user-owned entities (`CartItem`, `Order`) map the user FK via `HasOne<ApplicationUser>().WithMany().HasForeignKey(...)` with `UserId` as `string(450)` — Phase 1 mirrors this exactly (no navigation added to `ApplicationUser`).
- Single-default rule will be enforced in the service layer (Phase 2), so no filtered unique index in the schema.
- Migrations auto-apply on startup via `db.Database.MigrateAsync()` in [Program.cs](Program.cs); `dotnet watch` is running in terminal 23.

### 1. New entity: `Data/Entities/UserAddress.cs`
Vietnamese-native fields per the roadmap (no district, no postal code):

```csharp
namespace Nexus.Data.Entities;

public class UserAddress
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;

    public string RecipientName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string AddressLine { get; set; } = string.Empty; // house no. + street / hamlet
    public string Ward { get; set; } = string.Empty;         // ward / commune
    public string Province { get; set; } = string.Empty;     // province / city
    public string Country { get; set; } = "Vietnam";

    public bool IsDefault { get; set; }
    public string? Label { get; set; }                       // "Home", "Office"

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
```

### 2. Register + configure in `Data/ApplicationDbContext.cs`
Add the DbSet alongside the others:

```csharp
public DbSet<UserAddress> UserAddresses => Set<UserAddress>();
```

Add a Fluent config block in `OnModelCreating` (mirrors the `CartItem` block):

```csharp
builder.Entity<UserAddress>(entity =>
{
    entity.Property(a => a.UserId).HasMaxLength(450).IsRequired();
    entity.Property(a => a.RecipientName).HasMaxLength(200).IsRequired();
    entity.Property(a => a.Phone).HasMaxLength(40).IsRequired();
    entity.Property(a => a.AddressLine).HasMaxLength(300).IsRequired();
    entity.Property(a => a.Ward).HasMaxLength(150).IsRequired();
    entity.Property(a => a.Province).HasMaxLength(150).IsRequired();
    entity.Property(a => a.Country).HasMaxLength(120).IsRequired().HasDefaultValue("Vietnam");
    entity.Property(a => a.Label).HasMaxLength(60);

    entity.HasIndex(a => a.UserId);

    entity.HasOne<ApplicationUser>()
        .WithMany()
        .HasForeignKey(a => a.UserId)
        .OnDelete(DeleteBehavior.Cascade);
});
```

Notes:
- No filtered unique index (single-default enforced in service, per decision).
- No navigation property added to [Data/ApplicationUser.cs](Data/ApplicationUser.cs) — consistent with `CartItem`/`Order`.

### 3. Create + apply the migration
Scaffold into the active folder (namespace resolves to `Nexus.Data.Migrations` automatically):

```
dotnet ef migrations add AddUserAddresses -o Data/Migrations
```

Then apply. Either let `dotnet watch` restart trigger `MigrateAsync()` on startup, or run explicitly:

```
dotnet ef database update
```

Expected: a new `UserAddresses` table with a `UserId` index, an FK to `AspNetUsers` (cascade delete), and `Country` defaulting to `"Vietnam"`. The `ApplicationDbContextModelSnapshot.cs` in `Data/Migrations/` updates to include `UserAddress`.

### 4. Test support: `Nexus.Test.Integration/Infrastructure/DbHelper.cs`
Add read/insert helpers for later phases (mirrors `GetCartItemsAsync`):

```csharp
public async Task<IReadOnlyList<UserAddress>> GetAddressesAsync(string userId)
{
    await using var db = CreateContext();
    return await db.UserAddresses
        .Where(a => a.UserId == userId)
        .OrderByDescending(a => a.IsDefault)
        .ThenByDescending(a => a.Id)
        .ToListAsync();
}
```

### Acceptance criteria
- `dotnet build` succeeds; `AddUserAddresses` scaffolds against the current snapshot (no unrelated changes in the migration `Up`).
- Migration applies cleanly to the dev DB (table + `UserId` index + FK cascade + `Country` default present).
- `DbHelper.GetAddressesAsync` compiles against the test project.
- No changes to `Order`, `CheckoutRequest`, or checkout UI (those are Phase 4).

### Out of scope (later phases)
- `IAddressService` / CRUD + default management (Phase 2).
- Address book UI (Phase 3).
- Checkout prefill + VN relabel + `Ship*` mapping (Phase 4).