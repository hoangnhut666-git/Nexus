---
name: Order Payment Schema
overview: Add the Order, OrderItem, and Payment entities plus their enums, wire them into ApplicationDbContext with USD-precision money columns and proper indexes/delete behavior, generate the AddOrdersAndPayments migration, and extend the test DbHelper with order/payment read helpers. No RowVersion on ProductVariant (per decision).
todos:
  - id: enums
    content: Add OrderStatus, PaymentMethod, PaymentStatus enums in Data/Entities
    status: completed
  - id: entities
    content: Add Order, OrderItem, Payment POCO entities in Data/Entities
    status: completed
  - id: dbcontext
    content: Add DbSets + OnModelCreating config (precision, indexes, delete behavior, enum-to-string) in ApplicationDbContext
    status: completed
  - id: migration
    content: Generate AddOrdersAndPayments migration and verify it applies
    status: completed
  - id: dbhelper
    content: Extend DbHelper with order/payment read helpers
    status: completed
  - id: verify
    content: Build clean, confirm migration + snapshot, tick roadmap Phase 1 checklist
    status: completed
isProject: false
---

# Phase 1 - Order & payment schema

Goal: persist orders and payments with immutable line snapshots, in USD, following existing Nexus entity/DbContext conventions. No behavior/services yet - just schema and test helpers.

Reference: data model in [docs/order-payment-roadmap.md](docs/order-payment-roadmap.md) section 5. Pattern to mirror: existing `CartItem` config in [Data/ApplicationDbContext.cs](Data/ApplicationDbContext.cs) (lines 122-138) and entity style in [Data/Entities/Category.cs](Data/Entities/Category.cs).

Decision applied: no `RowVersion` on `ProductVariant` (Phase 2 will use an atomic conditional UPDATE instead).

### 1. Enums (namespace `Nexus.Data.Entities`)
Three files in [Data/Entities/](Data/Entities), stored as strings in the DB (see step 3):
- `OrderStatus`: `Pending, Paid, Processing, Shipped, Delivered, Cancelled`
- `PaymentMethod`: `Cod, PayPal`
- `PaymentStatus`: `Pending, Completed, Failed, Refunded`

### 2. Entities in [Data/Entities/](Data/Entities)
Plain POCOs matching existing style (nullable refs `= null!;`, collections `= [];`).

- `Order.cs`: `Id`, `OrderNumber` (string), `UserId` (string), `Status` (OrderStatus), `PaymentMethod` (PaymentMethod), `PaymentStatus` (PaymentStatus), money `Subtotal`/`ShippingFee`/`TaxAmount`/`Total` (decimal), `Currency` (string, default "USD"), shipping fields `ShipFullName`/`ShipPhone`/`ShipStreet`/`ShipCity`/`ShipState?`/`ShipPostalCode?`/`ShipCountry`, `CreatedAt`/`UpdatedAt`, nav `ICollection<OrderItem> Items` and `ICollection<Payment> Payments`.
- `OrderItem.cs`: `Id`, `OrderId`, `ProductVariantId`, snapshot fields `ProductName`/`VariantLabel`/`Sku`/`UnitPrice`/`Quantity`/`LineTotal`, navs `Order` and `ProductVariant`.
- `Payment.cs`: `Id`, `OrderId`, `Method` (PaymentMethod), `Status` (PaymentStatus), `Amount` (decimal), `Currency` (string), `GatewayTransactionRef?` (string), `RawPayloadJson?` (string), `CreatedAt`, `CompletedAt?`, nav `Order`.

### 3. DbContext wiring - [Data/ApplicationDbContext.cs](Data/ApplicationDbContext.cs)
Add DbSets: `Orders`, `OrderItems`, `Payments` (using the `=> Set<T>()` style already in the file).

Add `OnModelCreating` blocks mirroring the `CartItem` block:
- Order: enum props `.HasConversion<string>().HasMaxLength(...)`; money props `.HasPrecision(18,2)`; `OrderNumber` maxlen 30 + unique index; `UserId` maxlen 450 + index; `Status` index; shipping string maxlengths; `Currency` maxlen 3; FK to `ApplicationUser` via `HasForeignKey(o => o.UserId)` with `OnDelete(Restrict)`.
- OrderItem: snapshot string maxlengths; `UnitPrice`/`LineTotal` `.HasPrecision(18,2)`; FK to `Order` (Cascade); FK to `ProductVariant` (Restrict) so historical orders survive variant deletion.
- Payment: enum props to string; `Amount` `.HasPrecision(18,2)`; `Currency` maxlen 3; `GatewayTransactionRef` maxlen 100 with a filtered unique index (`HasFilter("[GatewayTransactionRef] IS NOT NULL")`); FK to `Order` (Cascade).

### 4. Migration
Generate `AddOrdersAndPayments`:

```
dotnet ef migrations add AddOrdersAndPayments --project Nexus.csproj --output-dir Data/Migrations
```

Migration is applied automatically at startup via `Program.cs` `MigrateAsync`; also verify it applies to the dev DB with `dotnet ef database update` (or a normal app run).

### 5. Test helper - [Nexus.Test.Integration/Infrastructure/DbHelper.cs](Nexus.Test.Integration/Infrastructure/DbHelper.cs)
Add read helpers for Phase 2 tests, matching existing method style (`await using var db = CreateContext();`):
- `GetOrderByNumberAsync(string orderNumber)` including `Items` and `Payments`.
- `GetOrdersAsync(string userId)`.
- `GetOrderItemsAsync(int orderId)`.
- `GetPaymentsAsync(int orderId)`.

### 6. Verify
- `dotnet build Nexus.csproj` clean (0 warnings/errors).
- Confirm migration file + updated `ApplicationDbContextModelSnapshot.cs` generated.
- Tick the Phase 1 checklist in [docs/order-payment-roadmap.md](docs/order-payment-roadmap.md) (leave the `RowVersion` item noted as intentionally skipped).

Note: the integration test harness currently has a pre-existing startup-migration failure unrelated to this change, so full `dotnet test` may not run green in this environment; build + migration generation are the verification gates for Phase 1.

### Out of scope
Order/payment services, checkout UI, stock deduction, PayPal (Phases 2-5).
