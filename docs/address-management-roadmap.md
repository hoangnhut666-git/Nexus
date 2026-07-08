# Address Management for Checkout — Implementation Roadmap

**Project:** Nexus E-commerce  
**Document version:** 1.0  
**Last updated:** 2026-07-08  
**Status:** Approved direction — ready for implementation

---

## 1. Purpose

This roadmap defines how Nexus will let a customer **save and manage shipping addresses** so that checkout no longer requires re-typing an address and phone number every time.

Today, checkout (`Components/Pages/Checkout/Index.razor`) captures a one-off shipping address into an inline form and snapshots it onto the `Order` as flat `Ship*` columns. There is **no `Address` entity**, and `ApplicationUser` only stores `FullName` — nothing about where the customer lives.

The feature introduces a per-user **address book** (one user → many addresses) with a **default address**. When a default exists, checkout is pre-filled and the customer can place an order without re-entering anything. The address form is also reshaped to a **Vietnamese-native** structure (house/detail line → ward/commune → province/city), matching the country's 2-tier administrative model.

It builds directly on the completed checkout/order flow described in [`order-payment-roadmap.md`](./order-payment-roadmap.md). The cart and order services (`Services/Orders`, `Services/Cart`) are the reference pattern for services, `ServiceResult<T>`, migrations, and integration tests.

### Goals

- Customer can add, edit, delete, and list shipping addresses from account management.
- Customer can mark exactly one address as **default**.
- Checkout **pre-fills** from the default address; a customer with a saved default can place an order without typing an address.
- Checkout lets the customer **pick another saved address** or enter a new one (with an optional "save to my account" toggle).
- Address fields are **Vietnamese-native** (recipient, phone, detail line, ward/commune, province/city) and drop the unused postal-code requirement.
- Orders continue to **snapshot** the shipping address at order time (immutable history), independent of later address edits/deletes.
- Implementation follows existing Nexus conventions: `IDbContextFactory` services, `ServiceResult<T>`, EF Core migrations, Blazor Interactive Server components, and Testcontainers integration tests.

### Non-goals (out of scope for this roadmap)

- Separate billing addresses (single shipping address per order for v1).
- Address verification / geocoding / map picker.
- Administrative-unit dropdown datasets for province/ward (free-text in v1; noted as a follow-up in Phase 5).
- Multiple default addresses or per-category defaults.
- Importing addresses from external providers.
- Guest (unauthenticated) address storage — address book requires an authenticated Customer.

---

## 2. Key decisions

| Decision | Choice | Rationale |
|----------|--------|-----------|
| Storage shape | **Address book**: one `UserAddress` → many per user | Handles home/office/gift cases; a single default is just the one-entry case. |
| Order address | **Keep the `Order.Ship*` snapshot** | Orders are historical records; editing/deleting a saved address must not mutate past orders. The address book only *feeds* checkout. |
| Order ↔ address link | **No FK from `Order` to `UserAddress`** | Preserves the immutable snapshot; avoids dangling references when an address is deleted. |
| Field model | **Vietnamese-native**: `RecipientName`, `Phone`, `AddressLine`, `Ward`, `Province`, `Country` | Matches the 2-tier model (No./hamlet + street → ward/commune → province/city); no district. |
| Detail line | Single free-text `AddressLine` (house no. + street, or hamlet) | Real addresses vary too much to force separate house-number/street columns. |
| Postal code | **Dropped** from required flow | Rarely used in Vietnamese addressing. |
| Country | Defaults to `"Vietnam"`; hidden in UI for v1 | Single-country storefront; keep the column for future international shipping. |
| Order column reuse | Map VN fields onto existing `Order.Ship*` columns (see §5.4) | Avoids a disruptive migration on `Order`/DTOs/admin views; snapshot semantics unchanged. |
| Default invariant | **At most one default per user**, enforced in a transaction | First address auto-becomes default; setting a new default clears the old one. |
| Delete behavior | Hard delete; if the default is removed, promote the most recent remaining address | Simple and predictable; orders are unaffected (snapshot). |
| Service pattern | `IAddressService` using `IDbContextFactory`, returning `ServiceResult<T>` | Mirrors `OrderService` / `CartService`. |
| UI location | Manage pages under `Components/Account/Pages/Manage/Addresses/` | That's where per-user profile pages already live. |

---

## 3. Current state

| Area | Status |
|------|--------|
| `Order.Ship*` snapshot columns (`ShipFullName/Phone/Street/City/State/PostalCode/Country`) | Done — `Data/Entities/Order.cs`; EF config in `ApplicationDbContext` |
| Checkout form (inline `CheckoutFormModel`) → `CheckoutRequest` → `OrderService` | Done — `Components/Pages/Checkout/Index.razor` |
| Checkout prefill | Only `ShipFullName` from `ApplicationUser.FullName`; **no saved address** |
| `ApplicationUser` | `FullName` only — **no address field** — `Data/ApplicationUser.cs` |
| `Address` / `UserAddress` entity | **Does not exist** — greenfield |
| Account management pages | Present under `Components/Account/Pages/Manage/` (natural home for the address book) |
| Db access pattern | `IDbContextFactory<ApplicationDbContext>` per operation (see `OrderService`) |
| Migrations | EF Core, auto-applied on startup (`Program.cs`) |
| Integration tests | `Nexus.Test.Integration` (Testcontainers, `DbHelper`, xUnit, FluentAssertions) |

> ⚠️ **Migration hygiene (do first):** the repo currently has **two migration folders** — `Data/Migrations/` and root `Migrations/` — both under namespace `Nexus.Migrations`, each with its own `ApplicationDbContextModelSnapshot`. Confirm which snapshot the EF tooling treats as current (root `Migrations/` holds the newest by timestamp) **before** scaffolding the address migration, so it isn't generated against a stale model. See Phase 0.

---

## 4. Address format (Vietnamese, 2-tier)

The storefront targets Vietnam, whose administrative model is now two tiers (**province/city → ward/commune**); the district tier is gone. Addresses read:

```
No. 25, Nguyễn Trãi Street, Tân An Ward, Cần Thơ City
No. 12, Đông Hòa Hamlet, Hòa Bình Commune, Bắc Giang Province
```

Field mapping used throughout this roadmap:

| Concept | Example | `UserAddress` field | UI label |
|---------|---------|---------------------|----------|
| Recipient | Nguyễn Văn A | `RecipientName` | Recipient name |
| Phone | 0901234567 | `Phone` | Phone number |
| House no. + street / hamlet | "No. 25, Nguyễn Trãi Street" | `AddressLine` | Street address / detail |
| Ward / commune | Tân An | `Ward` | Ward / Commune |
| Province / city | Cần Thơ | `Province` | Province / City |
| Country | Vietnam (default, hidden) | `Country` | — |

A shared formatter composes the one-line display: `"{AddressLine}, {Ward}, {Province}"` (append `Country` only if not Vietnam).

---

## 5. Target data model

### 5.1 Entity relationship (logical)

```
ApplicationUser
   │
   └── UserAddress (0..*)
         │  RecipientName / Phone
         │  AddressLine / Ward / Province / Country
         │  IsDefault (at most one true per user)
         │  Label? ("Home", "Office")
         │  CreatedAt / UpdatedAt
         │
         └─(prefills)─► Checkout form ─(snapshot)─► Order.Ship* columns
                                                    (immutable history, no FK)
```

### 5.2 `UserAddress` entity (draft)

| Field | Type | Notes |
|-------|------|-------|
| `Id` | int | PK |
| `UserId` | string(450) | FK → `ApplicationUser` (`Cascade`); indexed |
| `RecipientName` | string(200) | Required |
| `Phone` | string(40) | Required |
| `AddressLine` | string(300) | Required (house no. + street, or hamlet) |
| `Ward` | string(150) | Required (ward / commune) |
| `Province` | string(150) | Required (province / city) |
| `Country` | string(120) | Default `"Vietnam"` |
| `IsDefault` | bool | At most one `true` per user |
| `Label` | string(60)? | Optional ("Home", "Office") |
| `CreatedAt` / `UpdatedAt` | DateTime | UTC |

### 5.3 EF configuration (mirror existing `OnModelCreating` blocks)

- `HasMaxLength` on every string column; `RecipientName`, `Phone`, `AddressLine`, `Ward`, `Province`, `Country` are `IsRequired`.
- `UserAddress → ApplicationUser`: `HasForeignKey(a => a.UserId)` with `DeleteBehavior.Cascade` (deleting a user removes their book).
- Index on `UserId`. Optionally a **filtered unique index** on `(UserId)` `WHERE IsDefault = 1` to hard-enforce a single default at the DB level (SQL Server supports filtered indexes).
- `Country` default value `"Vietnam"`.

### 5.4 Mapping to the order snapshot

At checkout, the chosen address is composed into the **existing** `Order.Ship*` columns — no change to `Order`, `CheckoutRequest`, or downstream DTOs:

| `UserAddress` | → `Order.Ship*` / `CheckoutRequest` |
|---------------|--------------------------------------|
| `RecipientName` | `ShipFullName` |
| `Phone` | `ShipPhone` |
| `AddressLine` | `ShipStreet` |
| `Ward` | `ShipState` (repurposed; already optional) |
| `Province` | `ShipCity` |
| `Country` | `ShipCountry` |
| — | `ShipPostalCode` = `null` |

> Column names stay generic in storage; the **UI labels** are the Vietnamese-native terms. Admin order/detail views (`Services/Orders/Models/AdminOrderDetailDto.cs`, order pages) continue to read the same columns — relabel their display strings to Ward/Province for consistency (Phase 4).

---

## 6. Implementation phases

Phases are sequential. Each ends with migrations applied, services registered in `Program.cs`, and integration tests passing.

```
Phase 0 ──► Phase 1 ──► Phase 2 ──► Phase 3 ──► Phase 4 ──► Phase 5 (opt)
Migration   UserAddress  Address     Address     Checkout    VN admin-unit
hygiene     schema       service     book UI      integration dropdowns
```

---

### Phase 0 — Migration hygiene (prerequisite)

**Objective:** Ensure a clean EF migration baseline before adding a new table.

#### Tasks

| # | Task | Deliverable |
|---|------|-------------|
| 0.1 | Confirm which `Migrations` folder/snapshot EF treats as current (`dotnet ef migrations list`) | — |
| 0.2 | Consolidate to a single migrations folder + one `ApplicationDbContextModelSnapshot` if the two diverge | `Migrations/` (or `Data/Migrations/`) |
| 0.3 | Verify `dotnet ef migrations add` scaffolds against the correct model on a scratch migration | — |

#### Acceptance criteria

- [ ] `dotnet ef migrations list` shows one coherent history.
- [ ] A no-op scaffold produces an **empty** `Up/Down` (proves the snapshot matches the DB).

#### Estimated effort

**0.5–1 day**

---

### Phase 1 — `UserAddress` schema

**Objective:** Add the `UserAddress` entity, EF config, and migration.

#### Tasks

| # | Task | Deliverable |
|---|------|-------------|
| 1.1 | Add `UserAddress` entity | `Data/Entities/UserAddress.cs` |
| 1.2 | Add `DbSet<UserAddress>` + Fluent config (max lengths, required, FK cascade, indexes, default-filtered unique index) | `Data/ApplicationDbContext.cs` |
| 1.3 | (Optional) navigation `ICollection<UserAddress> Addresses` on `ApplicationUser` | `Data/ApplicationUser.cs` |
| 1.4 | Create + apply migration `AddUserAddresses` | migrations folder |
| 1.5 | Extend `DbHelper` with `GetAddressesAsync(userId)` | `Nexus.Test.Integration/Infrastructure/DbHelper.cs` |

#### Acceptance criteria

- [ ] Migration applies cleanly on a fresh DB.
- [ ] `UserId` FK is `Cascade`; index on `UserId` exists.
- [ ] Single-default constraint enforced (filtered unique index or service-level guard).

#### Estimated effort

**1–1.5 days**

---

### Phase 2 — Address service

**Objective:** CRUD + default management behind `IAddressService`, with the single-default invariant enforced transactionally.

#### Tasks

| # | Task | Deliverable |
|---|------|-------------|
| 2.1 | `IAddressService` + models (`AddressDto`, `AddressInput`) | `Services/Addresses/` |
| 2.2 | `GetAddressesAsync(userId)` (default first, then newest) | `AddressService` |
| 2.3 | `GetDefaultAsync(userId)` | `AddressService` |
| 2.4 | `AddAsync(userId, input)` — first address auto-default | `AddressService` |
| 2.5 | `UpdateAsync(userId, addressId, input)` — ownership-checked | `AddressService` |
| 2.6 | `SetDefaultAsync(userId, addressId)` — clears prior default in one transaction | `AddressService` |
| 2.7 | `DeleteAsync(userId, addressId)` — promote a new default if the deleted one was default | `AddressService` |
| 2.8 | Register `IAddressService` (scoped) in `Program.cs` | `Program.cs` |
| 2.9 | Integration tests (`AddressServiceTests`) | `Features/Addresses/` |

#### Business rules

1. Every mutation is **scoped to `userId`** (a user can only touch their own addresses).
2. Adding the first address sets `IsDefault = true`.
3. `SetDefaultAsync` runs inside a transaction: clear the current default, set the new one.
4. Deleting the default promotes the most recently updated remaining address (or leaves none if empty).

#### Acceptance criteria

- [ ] Cross-user access returns failure/not-found (isolation).
- [ ] Exactly one default per user after any sequence of operations.
- [ ] First-added address is default; deleting default promotes another.

#### Estimated effort

**2–3 days**

---

### Phase 3 — Address book UI

**Objective:** Customer manages addresses from account management.

#### Tasks

| # | Task | Deliverable |
|---|------|-------------|
| 3.1 | `/Account/Manage/Addresses` list page (cards, default badge, edit/delete, "Set as default") | `Components/Account/Pages/Manage/Addresses/Index.razor` |
| 3.2 | Add/Edit form (VN-native fields, validation, "Set as default" toggle) | `Components/Account/Pages/Manage/Addresses/Edit.razor` |
| 3.3 | Add "Addresses" link to the manage nav | `Components/Account/Shared/ManageNavMenu.razor` |
| 3.4 | Shared one-line address formatter | `Services/Addresses/AddressFormatter.cs` |
| 3.5 | Integration/render tests: page requires auth; CRUD reflects service | `Features/Addresses/` |

#### Acceptance criteria

- [ ] Signed-out user is redirected to login.
- [ ] Customer can add, edit, delete, and set a default address.
- [ ] Default address is visually marked; only one at a time.
- [ ] Validation blocks incomplete submissions.

#### Estimated effort

**2–3 days**

---

### Phase 4 — Checkout integration

**Objective:** Checkout uses saved addresses — prefill from default, pick another, or enter/save a new one.

#### Tasks

| # | Task | Deliverable |
|---|------|-------------|
| 4.1 | Load addresses in `OnInitializedAsync`; prefill form from default | `Components/Pages/Checkout/Index.razor` |
| 4.2 | "Deliver to …" summary + **Change** to pick a saved address (radio/select) | `Components/Pages/Checkout/Index.razor` |
| 4.3 | "Enter a new address" path + **Save to my account** checkbox → `AddressService.AddAsync` | `Components/Pages/Checkout/Index.razor` |
| 4.4 | Relabel checkout form to VN-native terms; drop postal code; map fields → `CheckoutRequest` per §5.4 | `Components/Pages/Checkout/Index.razor` |
| 4.5 | Relabel order confirmation/detail + admin order views to Ward/Province | `Components/Pages/Orders/Detail.razor`, `Components/Admin/Pages/Orders/Detail.razor` |
| 4.6 | Keep full manual-entry path working for users with **no** saved address (backward compatible) | `Components/Pages/Checkout/Index.razor` |
| 4.7 | Integration tests: prefill from default, pick saved, save-new, no-address fallback | `Features/Orders/` |

#### Checkout behavior

1. Load cart (existing) **and** the user's addresses.
2. If a default exists → prefill the form and show a "Deliver to …" summary with **Change**.
3. **Change** → choose another saved address, or expand the new-address form.
4. New address + "Save to my account" → persist via `AddressService` before/at placement.
5. Compose the chosen address into `CheckoutRequest` (§5.4) → `CreateOrderFromCartAsync` (unchanged).
6. No saved address → behaves exactly like today (manual entry).

#### Acceptance criteria

- [ ] Customer with a default can place an order without retyping an address.
- [ ] Customer can switch to another saved address at checkout.
- [ ] A new address entered with "save" appears in the address book afterward.
- [ ] Customers with no saved address can still check out (manual entry).
- [ ] Order snapshot (`Ship*`) matches the address used.

#### Estimated effort

**2–3 days**

---

### Phase 5 — (Optional) Vietnamese administrative-unit dropdowns

**Objective:** Replace free-text Province/Ward with validated dropdowns for cleaner data and better UX.

#### Tasks

| # | Task | Deliverable |
|---|------|-------------|
| 5.1 | Bundle a province/ward dataset (JSON, 2-tier post-reform) | `wwwroot/data/vn-admin-units.json` or a seeded lookup |
| 5.2 | Cascading Province → Ward selectors in the address form | address `Edit.razor` |
| 5.3 | Store canonical names (and optional codes) | `UserAddress` (+ optional code columns) |

#### Acceptance criteria

- [ ] Province selection filters available wards.
- [ ] Stored province/ward values are canonical.

#### Estimated effort

**2–4 days** (only if pursued)

---

## 7. Target file structure

```
d:\DevZone\Nexus\
├── Data/
│   ├── Entities/
│   │   └── UserAddress.cs               (new)
│   ├── ApplicationUser.cs               (optional: Addresses nav)
│   ├── ApplicationDbContext.cs          (DbSet + Fluent config)
│   └── <migrations>/                    (AddUserAddresses)
├── Services/
│   └── Addresses/
│       ├── IAddressService.cs           (new)
│       ├── AddressService.cs            (new)
│       ├── AddressFormatter.cs          (new)
│       └── Models/
│           ├── AddressDto.cs
│           └── AddressInput.cs
├── Components/
│   ├── Account/
│   │   ├── Pages/Manage/Addresses/
│   │   │   ├── Index.razor              (list)
│   │   │   └── Edit.razor               (add/edit)
│   │   └── Shared/ManageNavMenu.razor   (add link)
│   └── Pages/
│       ├── Checkout/Index.razor         (prefill + picker + save)
│       └── Orders/Detail.razor          (relabel VN terms)
└── Nexus.Test.Integration/
    └── Features/
        └── Addresses/
            ├── AddressServiceTests.cs
            └── AddressPageTests.cs
```

---

## 8. Service API sketch

Mirror `IOrderService` / `ICartService` style — async, `CancellationToken`, `ServiceResult<T>` for commands.

```csharp
public interface IAddressService
{
    Task<IReadOnlyList<AddressDto>> GetAddressesAsync(
        string userId, CancellationToken ct = default);

    Task<AddressDto?> GetDefaultAsync(
        string userId, CancellationToken ct = default);

    Task<ServiceResult<AddressDto>> AddAsync(
        string userId, AddressInput input, CancellationToken ct = default);

    Task<ServiceResult<AddressDto>> UpdateAsync(
        string userId, int addressId, AddressInput input, CancellationToken ct = default);

    Task<ServiceResult> SetDefaultAsync(
        string userId, int addressId, CancellationToken ct = default);

    Task<ServiceResult> DeleteAsync(
        string userId, int addressId, CancellationToken ct = default);
}

public sealed class AddressInput
{
    public string RecipientName { get; init; } = string.Empty;
    public string Phone { get; init; } = string.Empty;
    public string AddressLine { get; init; } = string.Empty;  // house no. + street / hamlet
    public string Ward { get; init; } = string.Empty;         // ward / commune
    public string Province { get; init; } = string.Empty;     // province / city
    public string Country { get; init; } = "Vietnam";
    public bool SetAsDefault { get; init; }
    public string? Label { get; init; }
}
```

`AddressDto` adds `Id`, `IsDefault`, and a composed `OneLine` (via `AddressFormatter`).

---

## 9. Testing strategy

Follow the established `Nexus.Test.Integration` pattern (Testcontainers, `TestDatabaseFixture.ResetAsync`, `DbHelper`, FluentAssertions, xUnit).

| Layer | What to test |
|-------|----------------|
| **Address service** | Add/update/delete; first-address-is-default; single default invariant across `SetDefault`; delete-default promotes another; per-user isolation |
| **Address page** | `/Account/Manage/Addresses` requires auth; CRUD reflects service |
| **Checkout** | Prefill from default; pick a saved address; save-new persists to book; no-address manual fallback still works; `Ship*` snapshot matches chosen address |

**Test data:** extend `DbHelper` with `GetAddressesAsync(userId)`; reuse the existing user-seeding helpers.

**Key scenarios:**

1. Add first address → `IsDefault = true`.
2. Add second + set default → old default cleared, exactly one default.
3. Delete default (with others present) → most-recent remaining becomes default.
4. Checkout with default → order placed without manual entry; snapshot equals default.
5. Checkout, choose "new address" + save → order snapshot matches; address appears in book.
6. Checkout with empty book → manual entry works exactly as today.

---

## 10. Dependencies & risks

| Risk | Mitigation |
|------|------------|
| New migration scaffolded against stale snapshot (two `Migrations` folders) | **Phase 0** reconciles the migration baseline first |
| More than one default per user (race/bug) | Filtered unique index on `(UserId) WHERE IsDefault=1` + transactional `SetDefault` |
| Deleting an address corrupting past orders | Orders snapshot `Ship*`; **no FK** from `Order` to `UserAddress` |
| VN field ↔ generic `Ship*` column confusion | Documented mapping (§5.4); relabel order/admin views; column names stay generic |
| Scope creep (billing addr, geocoding, dropdowns) | Explicitly out of scope; admin-unit dropdowns isolated to optional Phase 5 |
| Checkout regression for existing users | Phase 4 keeps the manual-entry path fully working (backward compatible) |

### External dependencies

- SQL Server via EF Core (existing).
- Docker for integration tests (existing).
- Completed checkout/order flow (existing — see order-payment roadmap).
- (Phase 5 only) a Vietnamese province/ward dataset.

---

## 11. Milestone summary

| Phase | Milestone | Customer | Tests |
|-------|-----------|----------|-------|
| **0** | Migration baseline clean | — | `migrations list` coherent |
| **1** | `UserAddress` schema | — | migration + `DbHelper` |
| **2** | Address service | — | service (CRUD + default) |
| **3** | Address book UI | Manage addresses | service + page |
| **4** | Checkout integration | One-click checkout with default | service + page |
| **5** | (Opt) VN dropdowns | Cleaner address entry | form |

**Total estimated effort:** ~7.5–11 days (Phase 0 through Phase 4; +2–4 days for optional Phase 5).

---

## 12. Definition of done (per phase)

- [ ] EF migration applied locally (where applicable)
- [ ] Services registered in `Program.cs`
- [ ] Customer UI functional for phase scope
- [ ] Single-default invariant holds
- [ ] Order snapshot (`Ship*`) unaffected by later address edits/deletes
- [ ] Integration tests added and passing (`dotnet test Nexus.sln`)
- [ ] This document checklist for the phase marked complete

---

## 13. References

| Resource | Path |
|----------|------|
| Order & payment roadmap | [`docs/order-payment-roadmap.md`](./order-payment-roadmap.md) |
| Software requirements | [`docs/SRS.md`](./SRS.md) |
| Order entity (snapshot columns) | `Data/Entities/Order.cs` |
| Checkout page (integration point) | `Components/Pages/Checkout/Index.razor` |
| Checkout request DTO | `Services/Orders/Models/CheckoutRequest.cs` |
| Result type | `Services/Categories/Models/ServiceResult.cs` |
| DB context (Fluent config pattern) | `Data/ApplicationDbContext.cs` |
| Account management pages | `Components/Account/Pages/Manage/` |
| Integration test infrastructure | `Nexus.Test.Integration/` |

---

## 14. Phase checklists (copy for tracking)

### Phase 0 — Migration hygiene
- [ ] `migrations list` reviewed
- [ ] Single snapshot / folder confirmed
- [ ] No-op scaffold verified empty

### Phase 1 — Schema
- [ ] `UserAddress` entity
- [ ] `DbSet` + Fluent config (lengths, required, FK cascade, indexes, single-default)
- [ ] Migration `AddUserAddresses` applied
- [ ] `DbHelper.GetAddressesAsync`

### Phase 2 — Address service
- [ ] `IAddressService` + `AddressDto` / `AddressInput`
- [ ] CRUD (ownership-checked)
- [ ] `SetDefaultAsync` (transactional, single default)
- [ ] Delete-default promotion
- [ ] Registered in `Program.cs`
- [ ] Service tests green

### Phase 3 — Address book UI
- [ ] List page (default badge, actions)
- [ ] Add/Edit form (VN fields, validation)
- [ ] Manage nav link
- [ ] `AddressFormatter`
- [ ] Page tests green

### Phase 4 — Checkout integration
- [ ] Prefill from default
- [ ] Pick a saved address
- [ ] Enter + save a new address
- [ ] VN-native relabel + drop postal code
- [ ] Order/admin views relabeled
- [ ] Manual-entry fallback intact
- [ ] Tests green

### Phase 5 — (Optional) VN admin-unit dropdowns
- [ ] Province/ward dataset
- [ ] Cascading selectors
- [ ] Canonical values stored
