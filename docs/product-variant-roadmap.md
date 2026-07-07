# Product & Variant Management — Implementation Roadmap

**Project:** Nexus E-commerce  
**Document version:** 1.0  
**Last updated:** 2026-07-06  
**Status:** Approved direction — ready for implementation

---

## 1. Purpose

This roadmap defines how Nexus will implement **Product** and **Product Variant** management for administrators, and how variants will later power the customer catalog, cart, and orders.

It extends the baseline described in [`SRS.md`](./SRS.md) with a variant-aware model. Category management is already implemented and serves as the reference pattern for services, admin UI, and integration tests.

### Goals

- Admin can manage a **product** (marketing shell) and its **sellable variants** (SKU, price, stock).
- Products with multiple configurations (e.g. iPhone: Color × Storage) use **per-product options** and a variant generator.
- **Cart and orders reference variants**, not products, with snapshots at order time.
- Implementation follows existing Nexus conventions: EF Core migrations, `Services/*` layer, Blazor Admin components, integration tests with Testcontainers.

### Non-goals (out of scope for this roadmap)

- Shopping cart, checkout, payments, and order fulfillment (separate roadmaps; designed for here).
- Promotions / discount rules.
- Multi-currency or multi-warehouse inventory.
- Public storefront polish beyond basic catalog pages needed to validate variants.

---

## 2. Architecture decisions

| Decision | Choice | Rationale |
|----------|--------|-----------|
| Sellable unit | `ProductVariant` | Price, stock, and SKU differ per configuration. |
| Product role | Parent / marketing entity | Shared name, description, category, gallery, specs. |
| Options model | Per-product options | Flexible across categories (phones use Storage; apparel uses Size). |
| Price location | Variant only | Avoid sync bugs; catalog shows “from {min price}”. |
| Stock location | Variant only | FR-CART-06 and FR-ADM-ORD-05 require per-SKU stock. |
| SKU uniqueness | Global unique | Simpler fulfillment and admin search. |
| Deletion | Soft-delete (`IsActive`) | Matches `Category` and SRS FR-PRD-04 / FR-PRD-05. |
| Images | Product gallery + optional variant override | Color-specific photos without duplicating entire products. |
| Slug | Product-level slug | Public URL `/products/{slug}`; variant selected on detail page. |

### Example: iPhone with Color × Storage

| Color | Storage | SKU | Price | Stock |
|-------|---------|-----|-------|-------|
| Orange | 32GB | `IPH16-ORG-32` | 24,990,000 ₫ | 12 |
| Red | 32GB | `IPH16-RED-32` | 24,990,000 ₫ | 8 |
| Yellow | 18GB | `IPH16-YEL-18` | 22,990,000 ₫ | 5 |
| Yellow | 32GB | `IPH16-YEL-32` | 24,990,000 ₫ | 3 |

Invalid combinations (e.g. Red + 18GB) are simply not created — no “ghost” variants.

---

## 3. Current state

| Area | Status |
|------|--------|
| `Category` entity + admin CRUD | Done — `Services/Categories`, `Components/Admin/Pages/Categories` |
| `Product` entity | Minimal — `Id`, `Name`, `CategoryId`, `IsActive` only |
| Variants / options | Not started |
| Admin `/admin/products` route | Nav link exists; page not implemented |
| Mockups | `mockup/product-management3.html`, `mockup/product-detail.html`, `mockup/product-detail-for-user.html` |
| Integration tests | Category + Identity patterns in `Nexus.Test.Integration` |

---

## 4. Target data model

### 4.1 Entity relationship (logical)

```
Category
   │
   └── Product ─────────────────────────────────────────┐
         │                                               │
         ├── ProductImage (ordered gallery)              │
         ├── ProductOption (e.g. "Color", display order) │
         │     └── ProductOptionValue (e.g. "Orange")    │
         └── ProductVariant ◄── sellable unit ──────────┘
               ├── Sku (unique)
               ├── Price
               ├── StockQuantity
               ├── ImageUrl (optional override)
               ├── IsActive
               └── VariantOptionValue (join → ProductOptionValue)
```

### 4.2 Entity fields (draft)

#### Product

| Field | Type | Notes |
|-------|------|-------|
| `Id` | int | PK |
| `Name` | string(200) | Required |
| `Slug` | string(200) | Unique; reuse `CategorySlugHelper` pattern |
| `Description` | string(4000) | Optional |
| `CategoryId` | int | FK → Category |
| `IsActive` | bool | Default true |
| `CreatedAt` | DateTime | |
| `UpdatedAt` | DateTime | |

#### ProductImage

| Field | Type | Notes |
|-------|------|-------|
| `Id` | int | PK |
| `ProductId` | int | FK |
| `ImageUrl` | string(500) | |
| `SortOrder` | int | Gallery order |
| `AltText` | string(200) | Optional |

#### ProductOption

| Field | Type | Notes |
|-------|------|-------|
| `Id` | int | PK |
| `ProductId` | int | FK |
| `Name` | string(100) | e.g. "Color", "Storage" |
| `SortOrder` | int | Display order on detail page |

#### ProductOptionValue

| Field | Type | Notes |
|-------|------|-------|
| `Id` | int | PK |
| `ProductOptionId` | int | FK |
| `Value` | string(100) | e.g. "Orange", "32GB" |
| `SortOrder` | int | |

#### ProductVariant

| Field | Type | Notes |
|-------|------|-------|
| `Id` | int | PK |
| `ProductId` | int | FK |
| `Sku` | string(50) | Unique index |
| `Price` | decimal(18,2) | VND per SRS FR-PRD-07 |
| `StockQuantity` | int | Default 0 |
| `ImageUrl` | string(500) | Optional color-specific image |
| `IsActive` | bool | Default true |

#### VariantOptionValue (join table)

| Field | Type | Notes |
|-------|------|-------|
| `ProductVariantId` | int | Composite PK |
| `ProductOptionValueId` | int | Composite PK |

**Constraint:** Each variant must have exactly one value per product option (enforced in service layer + optional DB check).

### 4.3 Future cart / order alignment (Phase 5+)

```
CartItem:  UserId, ProductVariantId, Quantity
OrderItem: ProductVariantId, Quantity, UnitPrice,
           SkuSnapshot, VariantLabelSnapshot
```

Design variant IDs and labels now so cart/order work does not require schema rework.

### 4.4 SRS updates (when implementing)

| Existing ID | Change |
|-------------|--------|
| FR-PRD-01 | Create product with name, description, category, images; variants hold price/stock/SKU |
| FR-PRD-03 | Update product fields and variant rows independently |
| FR-CAT-02 | Detail page shows option pickers; price/availability from selected variant |
| FR-CART-01 | Add **variant** to cart |
| FR-CART-06 | Stock check against **variant** `StockQuantity` |

Add new requirements:

| ID | Requirement | Priority |
|----|-------------|----------|
| FR-PRD-08 | Admin shall define per-product options (e.g. Color, Storage) and option values | Must |
| FR-PRD-09 | Admin shall create and manage variants with unique SKU, price, and stock | Must |
| FR-PRD-10 | Admin shall generate variants from option value combinations | Should |
| FR-PRD-11 | Admin shall bulk-set price or stock across variants | Should |
| FR-PRD-12 | Product without at least one active variant shall not appear in public catalog | Must |

---

## 5. Implementation phases

Phases are sequential. Each phase ends with migrations applied, services registered, and integration tests passing.

```
Phase 1 ──► Phase 2 ──► Phase 3 ──► Phase 4 ──► Phase 5
 Schema      Admin        Options      Customer     Cart prep
 + seed      list/CRUD    + matrix     catalog      (future)
```

---

### Phase 1 — Schema & product shell

**Objective:** Extend the data model and service foundation. Every product has at least one variant (default variant for simple products).

#### Tasks

| # | Task | Deliverable |
|---|------|-------------|
| 1.1 | Extend `Product` with `Slug`, `Description`, `CreatedAt`, `UpdatedAt` | `Data/Entities/Product.cs` |
| 1.2 | Add `ProductImage`, `ProductVariant` entities | `Data/Entities/` |
| 1.3 | Configure EF relationships, indexes (`IX_Products_Slug`, `IX_ProductVariants_Sku`) | `ApplicationDbContext.cs` |
| 1.4 | Create and apply migration | `Data/Migrations/` |
| 1.5 | Add `IProductImageService` (mirror `ICategoryImageService`) | `Services/Products/` |
| 1.6 | Add `IProductService` — create product + default variant, get by id | `Services/Products/` |
| 1.7 | Register services in `Program.cs` | |
| 1.8 | Add `ProductTestDataBuilder` helpers | `Nexus.Test.Integration/TestData/` |
| 1.9 | Integration tests: create product, slug uniqueness, default variant | `Features/Product/` |

#### Acceptance criteria

- [ ] Creating a product without variants auto-creates one default variant (SKU derived or supplied).
- [ ] Product slug is unique; duplicate names get disambiguated slugs.
- [ ] `dotnet test` passes with new product service tests.

#### Estimated effort

**3–5 days**

---

### Phase 2 — Admin product list & basic CRUD

**Objective:** Admin can list, create, edit, and soft-delete products with a single default variant (SKU, price, stock). UI follows category management patterns.

#### Tasks

| # | Task | Deliverable |
|---|------|-------------|
| 2.1 | `GetPagedAsync` with search (name, SKU), category filter, status filter | `ProductService` |
| 2.2 | `UpdateAsync`, `SetActiveAsync` (soft delete) | `ProductService` |
| 2.3 | DTOs: `ProductListItemDto`, `ProductDetailDto`, `ProductVariantDto`, requests | `Services/Products/Models/` |
| 2.4 | Admin page: product list table | `Components/Admin/Pages/Products/Index.razor` |
| 2.5 | Shared: `ProductTable.razor` | `Components/Admin/Shared/` |
| 2.6 | Drawer: create/edit product + single variant fields | `ProductDrawer.razor` |
| 2.7 | Image upload component (reuse category image pattern) | `ProductImageUpload.razor` |
| 2.8 | Wire `/admin/products` route with `[Authorize(Policy = "Admin")]` | |
| 2.9 | Integration tests: page renders, CRUD via service | `ProductPageTests`, `ProductServiceTests` |

#### UI reference

- List layout: `mockup/product-management3.html`
- Drawer fields: name, category, SKU, price, stock, description, images, status

#### List columns

| Column | Source |
|--------|--------|
| Product | Name + thumbnail |
| Category | Category name |
| SKU | Default / first variant SKU |
| Price | Min variant price (or sole variant) |
| Stock | Sum of variant stock |
| Status | Product `IsActive` |
| Actions | Edit, hide/show |

#### Acceptance criteria

- [ ] Admin can CRUD products with one variant from `/admin/products`.
- [ ] Inactive products hidden from admin default filter option; toggle to show hidden.
- [ ] Validation: required name, category, SKU, price ≥ 0, stock ≥ 0.
- [ ] Toast feedback matches category admin UX.

#### Estimated effort

**5–7 days**

---

### Phase 3 — Multi-variant & options

**Objective:** Support products like the iPhone example — multiple options, generated variants, variant matrix editing.

#### Tasks

| # | Task | Deliverable |
|---|------|-------------|
| 3.1 | Add `ProductOption`, `ProductOptionValue`, `VariantOptionValue` entities + migration | `Data/Entities/` |
| 3.2 | `SaveOptionsAsync` — replace option tree for a product | `ProductService` |
| 3.3 | `GenerateVariantsAsync` — Cartesian product of option values | `ProductService` |
| 3.4 | `UpsertVariantsAsync` — create/update/delete variant rows | `ProductService` |
| 3.5 | SKU auto-generation helper: `{productCode}-{value1}-{value2}` | `ProductSkuHelper.cs` |
| 3.6 | Admin: product detail page with tabs (Info \| Variants) | `Products/Detail.razor` |
| 3.7 | Options editor — add/reorder options and values | `ProductOptionsEditor.razor` |
| 3.8 | Variant matrix table for 1–2 options; list table for 3+ options | `ProductVariantMatrix.razor` |
| 3.9 | Bulk actions: set price, adjust stock, activate/deactivate | |
| 3.10 | Integration tests: option save, variant generation, SKU uniqueness | |

#### Variant matrix UX (2 options)

```
              | 18GB        | 32GB
--------------+-------------+-------------
Orange        |      —      | 12 @ 24.9M
Red           |      —      |  8 @ 24.9M
Yellow        |  5 @ 22.0M  |  3 @ 24.9M
```

- Empty cell = combination not offered.
- Click cell to edit SKU, price, stock, active flag.

#### Business rules

- Variant must map to exactly one value per option.
- Cannot delete an option value that is referenced by an order (future); for now, block delete if variant exists.
- At least one active variant required to activate product.
- Generating variants skips combinations that already exist (merge by option value set).

#### Acceptance criteria

- [ ] Admin can define Color + Storage and generate 4 variants for iPhone example.
- [ ] Admin can omit invalid combinations (no Red + 18GB).
- [ ] Each variant has unique SKU; duplicate SKU rejected with clear error.
- [ ] Product list still shows aggregated price/stock.

#### Estimated effort

**7–10 days**

---

### Phase 4 — Customer catalog (variant-aware)

**Objective:** Customers browse products and select variants on the detail page. No cart yet — validate variant resolution and stock display.

#### Tasks

| # | Task | Deliverable |
|---|------|-------------|
| 4.1 | `GetCatalogPagedAsync` — active products with min price, in-stock flag | `ProductService` |
| 4.2 | `GetBySlugAsync` — product + options + variants for storefront | |
| 4.3 | `ResolveVariantAsync(productId, selectedOptionValueIds)` | |
| 4.4 | Public product listing page | `Components/Pages/Products/Index.razor` (or storefront area) |
| 4.5 | Public product detail with option pickers | Reference `mockup/product-detail-for-user.html` |
| 4.6 | Disable add-to-cart button when out of stock (UI only until Phase 5) | |
| 4.7 | Category filter and keyword search | |
| 4.8 | Integration tests: catalog excludes inactive; variant resolution | |

#### Customer detail behavior

1. Load product by slug.
2. Render one control group per `ProductOption` (buttons or swatches).
3. On selection change, resolve variant → update price, stock message, image.
4. Invalid / incomplete selection → show “Select {option}” state.

#### Acceptance criteria

- [ ] Catalog shows only active products with ≥ 1 active variant.
- [ ] Detail page resolves Yellow + 32GB to correct SKU, price, stock.
- [ ] Out-of-stock variant shows unavailable state (FR-CAT-05).
- [ ] URLs use product slug; no variant in URL for v1 (selection client-side).

#### Estimated effort

**5–7 days**

---

### Phase 5 — Cart & order preparation (foundation only)

**Objective:** Schema and service contracts for cart/order integration. Full cart/checkout is a separate initiative; this phase prevents rework.

#### Tasks

| # | Task | Deliverable |
|---|------|-------------|
| 5.1 | Document `CartItem` / `OrderItem` entities (design only or stub migration) | This doc + optional `Data/Entities/` |
| 5.2 | `ReserveStockAsync` / `ReleaseStockAsync` on variant | `ProductService` or `IInventoryService` |
| 5.3 | Snapshot fields defined on `OrderItem` DTO | |
| 5.4 | Update SRS Section 6 data diagram | `SRS.md` |
| 5.5 | Add ADR or note in this doc for order-line snapshot format | |

#### Acceptance criteria

- [ ] Stock deduction API exists and is covered by tests.
- [ ] SRS and roadmap agree on variant-centric cart model.
- [ ] No product-level price/stock remains in schema or DTOs.

#### Estimated effort

**2–3 days** (foundation); full cart/checkout **separate roadmap**

---

## 6. Target file structure

```
d:\DevZone\Nexus\
├── Data/
│   └── Entities/
│       ├── Product.cs                 (extend)
│       ├── ProductImage.cs              (new)
│       ├── ProductOption.cs             (Phase 3)
│       ├── ProductOptionValue.cs        (Phase 3)
│       ├── ProductVariant.cs            (new)
│       └── VariantOptionValue.cs        (Phase 3)
├── Services/
│   └── Products/
│       ├── IProductService.cs
│       ├── ProductService.cs
│       ├── IProductImageService.cs
│       ├── ProductImageService.cs
│       ├── ProductSlugHelper.cs
│       ├── ProductSkuHelper.cs          (Phase 3)
│       └── Models/
│           ├── ProductListItemDto.cs
│           ├── ProductDetailDto.cs
│           ├── ProductVariantDto.cs
│           ├── ProductOptionDto.cs
│           ├── CreateProductRequest.cs
│           ├── UpdateProductRequest.cs
│           ├── ProductQuery.cs
│           ├── PagedResult.cs           (shared or reuse Categories)
│           └── ServiceResult.cs         (shared or reuse Categories)
├── Components/
│   └── Admin/
│       ├── Pages/
│       │   └── Products/
│       │       ├── Index.razor          (Phase 2)
│       │       └── Detail.razor         (Phase 3)
│       └── Shared/
│           ├── ProductTable.razor
│           ├── ProductDrawer.razor
│           ├── ProductImageUpload.razor
│           ├── ProductOptionsEditor.razor   (Phase 3)
│           └── ProductVariantMatrix.razor   (Phase 3)
└── Nexus.Test.Integration/
    ├── TestData/
    │   └── ProductTestDataBuilder.cs
    └── Features/
        └── Product/
            ├── ProductServiceTests.cs
            └── ProductPageTests.cs
```

---

## 7. Service API sketch

Mirror `ICategoryService` style — async methods, `CancellationToken`, `ServiceResult<T>` for commands.

```csharp
public interface IProductService
{
    // Phase 1–2
    Task<PagedResult<ProductListItemDto>> GetPagedAsync(ProductQuery query, CancellationToken ct = default);
    Task<ProductDetailDto?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<ProductDetailDto?> GetBySlugAsync(string slug, CancellationToken ct = default);
    Task<ServiceResult<ProductDetailDto>> CreateAsync(CreateProductRequest request, CancellationToken ct = default);
    Task<ServiceResult<ProductDetailDto>> UpdateAsync(int id, UpdateProductRequest request, CancellationToken ct = default);
    Task<ServiceResult> SetActiveAsync(int id, bool isActive, CancellationToken ct = default);

    // Phase 3
    Task<ServiceResult<ProductDetailDto>> SaveOptionsAsync(int productId, SaveProductOptionsRequest request, CancellationToken ct = default);
    Task<ServiceResult<IReadOnlyList<ProductVariantDto>>> GenerateVariantsAsync(int productId, GenerateVariantsRequest request, CancellationToken ct = default);
    Task<ServiceResult<ProductDetailDto>> UpsertVariantsAsync(int productId, UpsertVariantsRequest request, CancellationToken ct = default);

    // Phase 4
    Task<ProductVariantDto?> ResolveVariantAsync(int productId, IReadOnlyList<int> optionValueIds, CancellationToken ct = default);

    // Phase 5
    Task<ServiceResult> AdjustStockAsync(int variantId, int delta, CancellationToken ct = default);
}
```

---

## 8. Testing strategy

Follow the established `Nexus.Test.Integration` pattern:

| Layer | What to test |
|-------|----------------|
| **Service** | CRUD, slug/SKU uniqueness, variant generation, resolve variant, stock rules |
| **Page** | `/admin/products` renders; admin policy required |
| **Catalog** | Inactive products hidden; min price correct |

**Test data:** Extend `DbHelper` and Bogus builders for products with 1..N variants.

**Key scenarios:**

1. Create product → default variant created.
2. Generate 3×2 options → 6 variants; delete 2 → 4 remain.
3. Duplicate SKU → `ServiceResult` failure.
4. Resolve variant with wrong option count → null.
5. Set product inactive → excluded from `GetCatalogPagedAsync`.

---

## 9. Dependencies & risks

| Risk | Mitigation |
|------|------------|
| Scope creep into cart/checkout | Phase 5 is foundation only; stick to admin + catalog until Phase 4 done |
| Complex variant matrix UI | Start with list view; add 2D matrix when options = 2 |
| SRS drift | Update FR-PRD / FR-CAT IDs in Phase 1 or 2 |
| Image storage | Reuse local `wwwroot/uploads` pattern from categories |
| Performance with many variants | Paginate variant list; index `ProductId` on variants |

### External dependencies

- SQL Server via EF Core (existing)
- Category data must exist before assigning products to categories
- Docker for integration tests (existing)

---

## 10. Milestone summary

| Phase | Milestone | Admin | Customer | Tests |
|-------|-----------|-------|----------|-------|
| **1** | Schema + default variant | — | — | Service |
| **2** | Product list & CRUD | `/admin/products` | — | Service + page |
| **3** | Options & multi-variant | Detail + matrix | — | Generator + matrix |
| **4** | Storefront catalog | — | List + detail | Catalog + resolve |
| **5** | Cart foundation | — | — | Stock API |

**Total estimated effort (Phases 1–4):** ~20–29 days  
**Phase 5 stub:** +2–3 days

---

## 11. Definition of done (per phase)

- [ ] EF migration applied locally
- [ ] Services registered in `Program.cs`
- [ ] Admin or public UI functional for phase scope
- [ ] Integration tests added and passing (`dotnet test Nexus.sln`)
- [ ] No product-level price/stock in new code (variant-only)
- [ ] This document checklist for the phase marked complete

---

## 12. References

| Resource | Path |
|----------|------|
| Software requirements | [`docs/SRS.md`](./SRS.md) |
| Category implementation (pattern) | `Services/Categories/`, `Components/Admin/Pages/Categories/` |
| Admin product mockup | `mockup/product-management3.html` |
| Admin product detail mockup | `mockup/product-detail.html` |
| Customer product detail mockup | `mockup/product-detail-for-user.html` |
| Integration test infrastructure | `Nexus.Test.Integration/` |

---

## 13. Phase checklists (copy for tracking)

### Phase 1
- [ ] Entities and migration
- [ ] `IProductService` + default variant on create
- [ ] `IProductImageService`
- [ ] `ProductTestDataBuilder`
- [ ] Service integration tests green

### Phase 2
- [ ] Paginated admin list with filters
- [ ] Product drawer CRUD
- [ ] Image upload
- [ ] Page + service tests green

### Phase 3
- [ ] Option entities and migration
- [ ] Variant generator
- [ ] Product detail admin page
- [ ] Matrix / list variant editor
- [ ] Multi-variant tests green

### Phase 4
- [ ] Catalog query APIs
- [ ] Public list + detail pages
- [ ] Option pickers + variant resolution
- [ ] Catalog tests green

### Phase 5
- [ ] Cart/order entity design documented
- [ ] Stock adjust API
- [ ] SRS Section 6 updated
