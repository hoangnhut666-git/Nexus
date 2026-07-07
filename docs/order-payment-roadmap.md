# Order & Payment — Implementation Roadmap

**Project:** Nexus E-commerce  
**Document version:** 1.0  
**Last updated:** 2026-07-07  
**Status:** Approved direction — ready for implementation

---

## 1. Purpose

This roadmap defines how Nexus will implement **checkout, order placement, and payment**, letting a customer turn a persistent cart into a confirmed order and pay with one of two methods:

- **Cash on Delivery (COD)** — order is created immediately; payment settles on delivery.
- **PayPal** — order is created, the customer approves on PayPal, and payment is captured on return.

It extends [`SRS.md`](./SRS.md) (FR-ORD-*, FR-PAY-*, FR-ADM-ORD-*) and builds directly on the variant-aware cart delivered in [`product-variant-roadmap.md`](./product-variant-roadmap.md). Cart management (`Services/Cart`, `Components/Pages/Cart/Index.razor`) is already implemented and is the reference pattern for services, pricing, and integration tests.

### Goals

- Customer can check out from the cart, enter a shipping address, choose COD or PayPal, and place an order.
- Orders **snapshot** line prices, labels, SKUs, and totals at order time (immutable history).
- Stock is **deducted atomically on order creation** and **restored on cancellation**.
- PayPal charges in **USD** via the Orders v2 REST API, captured on redirect-return.
- Admin can list, filter, view, and progress orders through the SRS status workflow.
- Implementation follows existing Nexus conventions: `IDbContextFactory` services, `ServiceResult<T>`, EF Core migrations, Blazor Interactive Server components, and Testcontainers integration tests.

### Non-goals (out of scope for this roadmap)

- Guest checkout (SRS FR-ORD-06 is `Could`; v1 requires an authenticated Customer).
- Promotions / discount / promo codes (the cart promo box stays disabled).
- Refunds and partial captures through the gateway UI (record `Refunded` status manually only).
- Webhook-based payment reconciliation (capture-on-return only for v1; see Risks).
- Additional gateways (VNPay, Stripe) — the gateway abstraction leaves room, but only COD + PayPal ship.
- Multi-currency display (the whole app moves to a single currency: **USD**).
- Email notifications (on-screen confirmation only; email is a `Should` follow-up).

---

## 2. Key decisions

| Decision | Choice | Rationale |
|----------|--------|-----------|
| Store currency | **USD** (whole app) | PayPal cannot process VND; unify the app on one currency to avoid conversion logic. |
| Sellable reference on order | `ProductVariant` | Cart already references variants; orders must match (price/stock live on variant). |
| Line pricing | **Snapshot** on `OrderItem` | SRS 6.2 — prices/names must survive later catalog edits. |
| Order total | Recomputed server-side via `CartPricing` | FR-PAY-02 — never trust client totals; single source of truth. |
| Stock strategy | **Deduct on create, restore on cancel** | Simplest correct model; avoids reservation/timeout machinery in v1. |
| Concurrency guard | Conditional `ExecuteUpdateAsync` (`WHERE StockQuantity >= qty`) + optional `RowVersion` | Prevents oversell under concurrent checkout. |
| Payment methods | `Cod`, `PayPal` | Per requirement. |
| Payment abstraction | `IPaymentGateway` resolved by `PaymentMethod` | Keeps `OrderService` gateway-agnostic; room for future providers. |
| PayPal API | **Orders v2 REST** (sandbox first) | Checkout SDK is deprecated; REST is current. |
| PayPal confirmation | **Capture on redirect-return** | Sufficient for v1; webhook verification deferred. |
| PayPal currency | **USD** | Only currency both the store and PayPal will use. |
| Idempotency | Unique index on `Payment.GatewayTransactionRef` + status guard | FR-PAY, NFR-AVAIL-03 — no duplicate charges/captures. |
| Secrets | User secrets / env vars via options class | FR-PAY-07, NFR-MAINT-03 — mirror `IdentitySettings`. |
| Order number | Human-friendly, unique (e.g. `NX-20260707-000123`) | Used in confirmation, history, and support lookups. |

---

## 3. Current state

| Area | Status |
|------|--------|
| `CartItem` entity + `CartService` (variant-based, persistent) | Done — `Services/Cart`, `Data/Entities/CartItem.cs` |
| Cart pricing (subtotal, shipping, 8% VAT, total) | Done — `Services/Cart/Models/CartPricing.cs` |
| Cart page + "Proceed to Checkout" button | Done — button navigates to `/checkout` (Phase 3) |
| Currency | **VND today** (`vi-VN`, `₫`, `30,000` shipping, `5,000,000` free threshold) — must switch to USD |
| Order / Payment entities | Done — `Data/Entities/Order.cs`, `OrderItem.cs`, `Payment.cs` (Phase 1) |
| Checkout / order / payment services | COD order service done — `Services/Orders` (Phase 2); PayPal gateway pending |
| Checkout / order / admin-order pages | Customer checkout + order pages done — `Components/Pages/Checkout`, `Components/Pages/Orders` (Phase 3); admin pages pending (Phase 5) |
| Integration tests | Cart + Product + Category + Identity patterns in `Nexus.Test.Integration` |
| Mockups | `mockup/cart-management.html` (checkout/order mockups not yet present) |

---

## 4. Currency migration (prerequisite)

The app is VND end-to-end today. "USD for this repo" means touching existing code **before** order work, otherwise cart and order totals disagree.

| # | Change | Location |
|---|--------|----------|
| C.1 | Replace VND shipping/threshold constants with USD (e.g. `StandardShippingFee = 9.99m`, `FreeShippingThreshold = 500m`); keep `TaxRate = 0.08m` | `Services/Cart/Models/CartPricing.cs` |
| C.2 | Introduce a shared `MoneyFormatter` (USD, `en-US`, `"C"` format) and use it everywhere instead of copied `FormatPrice` | `Services/.../MoneyFormatter.cs` (new) |
| C.3 | Replace `vi-VN` / `₫` formatting in the cart page with the shared formatter | `Components/Pages/Cart/Index.razor` |
| C.4 | Re-baseline cart pricing tests to USD expected values | `Nexus.Test.Integration/Features/Cart/CartServiceTests.cs` |
| C.5 | Review seed prices so catalog values read as sensible USD | `Data/CatalogSeedData.cs` |

> **Confirm before implementing:** exact USD shipping fee and free-shipping threshold. Defaults above are placeholders.

Acceptance:

- [x] Cart totals render as USD across the storefront.
- [x] `CartPricing` constants and tests agree (shipping $9.99, free over $99, tax rounds to 2 decimals). Cart test suite currently blocked by a pre-existing DB migration issue in the test harness, unrelated to the currency change.
- [x] No `vi-VN` / `₫` remains in customer-facing pricing.

---

## 5. Target data model

### 5.1 Entity relationship (logical)

```
ApplicationUser
   │
   └── Order ───────────────────────────────────────────┐
         │  OrderNumber (unique)                          │
         │  Status (enum)  PaymentMethod (enum)           │
         │  PaymentStatus (enum)                          │
         │  Subtotal / ShippingFee / TaxAmount / Total    │
         │  Currency = "USD"                              │
         │  Shipping address (name, phone, street, ...)   │
         │                                                │
         ├── OrderItem ◄── snapshot of a cart line ───────┤
         │     ProductVariantId (FK, Restrict)            │
         │     ProductName / VariantLabel / Sku (snap)    │
         │     UnitPrice / Quantity / LineTotal (snap)    │
         │                                                │
         └── Payment                                      │
               Method (enum)  Status (enum)               │
               Amount / Currency                          │
               GatewayTransactionRef (unique, nullable)   │
               RawPayloadJson (audit, optional)           │
                                                          │
ProductVariant ◄──────────────────────────────────────────┘
   (StockQuantity deducted on create, restored on cancel)
```

### 5.2 Enums

| Enum | Values | Notes |
|------|--------|-------|
| `OrderStatus` | `Pending, Paid, Processing, Shipped, Delivered, Cancelled` | Matches SRS 6.3 workflow |
| `PaymentMethod` | `Cod, PayPal` | Per requirement |
| `PaymentStatus` | `Pending, Completed, Failed, Refunded` | Matches FR-PAY-03 |

Store enums as strings via `.HasConversion<string>()` for readable rows and simpler auditing.

### 5.3 Entity fields (draft)

#### Order

| Field | Type | Notes |
|-------|------|-------|
| `Id` | int | PK |
| `OrderNumber` | string(30) | Unique index; e.g. `NX-20260707-000123` |
| `UserId` | string(450) | FK → ApplicationUser; indexed |
| `Status` | OrderStatus | Indexed for admin filters |
| `PaymentMethod` | PaymentMethod | `Cod` / `PayPal` |
| `PaymentStatus` | PaymentStatus | |
| `Subtotal` | decimal(18,2) | Snapshot from `CartPricing` |
| `ShippingFee` | decimal(18,2) | Snapshot |
| `TaxAmount` | decimal(18,2) | Snapshot |
| `Total` | decimal(18,2) | Snapshot; payment amount must match |
| `Currency` | string(3) | `"USD"` |
| `ShipFullName` | string(200) | FR-ORD-02 |
| `ShipPhone` | string(40) | |
| `ShipStreet` | string(300) | |
| `ShipCity` | string(120) | |
| `ShipState` | string(120) | Optional |
| `ShipPostalCode` | string(20) | Optional |
| `ShipCountry` | string(120) | |
| `CreatedAt` / `UpdatedAt` | DateTime | UTC |

#### OrderItem

| Field | Type | Notes |
|-------|------|-------|
| `Id` | int | PK |
| `OrderId` | int | FK → Order (Cascade) |
| `ProductVariantId` | int | FK → ProductVariant (**Restrict** — keep history) |
| `ProductName` | string(200) | Snapshot |
| `VariantLabel` | string(300) | Snapshot (e.g. "Orange / 32GB") |
| `Sku` | string(50) | Snapshot |
| `UnitPrice` | decimal(18,2) | Snapshot |
| `Quantity` | int | |
| `LineTotal` | decimal(18,2) | `UnitPrice * Quantity` snapshot |

#### Payment

| Field | Type | Notes |
|-------|------|-------|
| `Id` | int | PK |
| `OrderId` | int | FK → Order (Cascade) |
| `Method` | PaymentMethod | |
| `Status` | PaymentStatus | |
| `Amount` | decimal(18,2) | Must equal `Order.Total` (FR-PAY-02) |
| `Currency` | string(3) | `"USD"` |
| `GatewayTransactionRef` | string(100)? | PayPal capture/order id; **unique** when present |
| `RawPayloadJson` | string(max)? | Optional gateway response for audit |
| `CreatedAt` | DateTime | |
| `CompletedAt` | DateTime? | Set when `Completed` |

### 5.4 EF configuration (mirror existing `OnModelCreating` blocks)

- All money fields `HasPrecision(18, 2)`.
- `HasMaxLength` on every string column.
- `Order`: unique index on `OrderNumber`; indexes on `UserId` and `Status`; FK to `ApplicationUser` with `Restrict`.
- `OrderItem → Order`: Cascade. `OrderItem → ProductVariant`: **Restrict**.
- `Payment → Order`: Cascade. Filtered unique index on `GatewayTransactionRef` (`WHERE GatewayTransactionRef IS NOT NULL`).
- Optional: add `[Timestamp] RowVersion` to `ProductVariant` for concurrency (Phase 1 decision).

### 5.5 SRS alignment

Requirements already covered by this roadmap: **FR-ORD-01..05, FR-ORD-07, FR-ORD-08, FR-PAY-02..07, FR-ADM-ORD-01..06**.

| Existing ID | Interpretation for v1 |
|-------------|-----------------------|
| FR-ORD-04 | COD order created `Pending`; PayPal order becomes `Paid` after capture |
| FR-ORD-05 | On-screen confirmation (email deferred) |
| FR-ORD-06 | Guest checkout **not** implemented (deferred) |
| FR-PAY-01 | PayPal is the third-party gateway; COD is internal |
| FR-PAY-06 | No card data stored — redirect model keeps Nexus out of PCI scope |
| FR-ADM-ORD-05 | Stock deducted at creation (not at ship); cancel restores |

Currency note to add to SRS Section 8: store operates in **USD** (supersedes VND assumption).

---

## 6. Implementation phases

Phases are sequential. Each phase ends with migrations applied, services registered in `Program.cs`, and integration tests passing. **COD ships end-to-end before PayPal.**

```
Phase 0 ──► Phase 1 ──► Phase 2 ──► Phase 3 ──► Phase 4 ──► Phase 5
Currency    Schema       Order svc    Checkout     PayPal       Admin
→ USD       + migration  (COD)        + order UI   gateway      orders
```

---

### Phase 0 — Currency switch to USD

**Objective:** Move the existing cart/pricing stack to USD so order totals are consistent from day one.

#### Tasks

| # | Task | Deliverable |
|---|------|-------------|
| 0.1 | Update `CartPricing` shipping fee + free-shipping threshold to USD values | `Services/Cart/Models/CartPricing.cs` |
| 0.2 | Add shared `MoneyFormatter` (USD `en-US`) | `Services/.../MoneyFormatter.cs` |
| 0.3 | Replace cart page price formatting with shared formatter | `Components/Pages/Cart/Index.razor` |
| 0.4 | Re-baseline cart pricing tests to USD | `Features/Cart/CartServiceTests.cs` |
| 0.5 | Review/adjust seed prices for USD readability | `Data/CatalogSeedData.cs` |

#### Acceptance criteria

- [ ] Storefront shows USD everywhere.
- [ ] `CartPricing` and cart tests agree; `dotnet test` green.

#### Estimated effort

**1–2 days**

---

### Phase 1 — Order & payment schema

**Objective:** Add `Order`, `OrderItem`, `Payment` entities + enums, configure EF, and migrate.

#### Tasks

| # | Task | Deliverable |
|---|------|-------------|
| 1.1 | Add `OrderStatus`, `PaymentMethod`, `PaymentStatus` enums | `Data/Entities/` (or `Data/Enums/`) |
| 1.2 | Add `Order`, `OrderItem`, `Payment` entities | `Data/Entities/` |
| 1.3 | Add `DbSet`s + Fluent config (precision, indexes, delete behavior, enum→string) | `Data/ApplicationDbContext.cs` |
| 1.4 | (Decision) Add `RowVersion` concurrency token to `ProductVariant` | `Data/Entities/ProductVariant.cs` |
| 1.5 | Create + apply migration `AddOrdersAndPayments` | `Data/Migrations/` |
| 1.6 | Extend `DbHelper` with order/payment read helpers | `Nexus.Test.Integration/Infrastructure/DbHelper.cs` |

#### Acceptance criteria

- [x] Migration applies cleanly on a fresh DB (`AddOrdersAndPayments` applied to `NexusDB1`).
- [x] Unique indexes on `OrderNumber` and `GatewayTransactionRef` (filtered) exist.
- [x] `OrderItem → ProductVariant` is `Restrict`.

> Note: migrations live in the default `Migrations/` folder (namespace `Nexus.Migrations`), alongside the consolidated `InitialCreate`. `RowVersion` on `ProductVariant` was intentionally skipped (Phase 2 uses an atomic conditional UPDATE instead).

#### Estimated effort

**2–3 days**

---

### Phase 2 — Order service (COD path)

**Objective:** Turn a cart into an order transactionally, deduct stock, and support COD end-to-end (no gateway yet).

#### Tasks

| # | Task | Deliverable |
|---|------|-------------|
| 2.1 | `IOrderService` + models (`CheckoutRequest`, `PlaceOrderResult`, `OrderDto`, `OrderLineDto`, `OrderSummaryDto`) | `Services/Orders/` |
| 2.2 | `CreateOrderFromCartAsync` — load cart, re-validate stock/active, recompute totals via `CartPricing`, snapshot lines | `OrderService` |
| 2.3 | Atomic stock deduction inside a DB transaction (`ExecuteUpdateAsync` guard); fail whole order if any line short | `OrderService` |
| 2.4 | Order number generator (date + sequence) | `OrderNumberGenerator.cs` |
| 2.5 | Clear cart on successful COD placement; update `CartState` | `OrderService` / caller |
| 2.6 | `GetOrderAsync(userId, orderNumber)`, `GetOrderHistoryAsync(userId)` | `OrderService` |
| 2.7 | Register services in `Program.cs` | |
| 2.8 | Integration tests (`OrderServiceTests`) | `Features/Orders/` |

#### Order creation flow (COD)

1. Validate user + non-empty cart.
2. Load cart lines with variants; re-check `IsActive` and stock (cart may be stale).
3. Recompute subtotal/shipping/tax/total with `CartPricing.BuildCart` (server-side).
4. Begin transaction → create `Order` (`Pending` / `PaymentStatus = Pending`) + `OrderItem` snapshots + `Payment` (`Cod`, `Pending`).
5. Deduct stock atomically; roll back + fail if any decrement is rejected.
6. Clear cart, commit, return `orderNumber`.

#### Acceptance criteria

- [x] Empty cart → `ServiceResult` failure.
- [x] Stale/out-of-stock line → order rejected, nothing persisted.
- [x] Order totals equal cart totals exactly (snapshot).
- [x] Stock decremented by ordered quantity; concurrent checkout cannot oversell.
- [x] Cart cleared after successful COD order.

#### Estimated effort

**4–6 days**

---

### Phase 3 — Checkout & order pages (customer)

**Objective:** Customer-facing checkout, confirmation, and order history/detail. COD is fully usable after this phase.

#### Tasks

| # | Task | Deliverable |
|---|------|-------------|
| 3.1 | `/checkout` page: shipping form (prefill `FullName`), payment method radio (COD/PayPal), order summary | `Components/Pages/Checkout/Index.razor` |
| 3.2 | Wire cart "Proceed to Checkout" button → `/checkout` | `Components/Pages/Cart/Index.razor` |
| 3.3 | Submit handler: COD → place order → redirect to confirmation; PayPal → placeholder until Phase 4 | |
| 3.4 | `/orders/{orderNumber}` confirmation + detail (items, totals, status, address) — FR-ORD-08 | `Components/Pages/Orders/Detail.razor` |
| 3.5 | `/orders` history list (FR-ORD-07) | `Components/Pages/Orders/Index.razor` |
| 3.6 | `[Authorize(Roles = CustomerRole)]` + `StorefrontLayout` on all new pages | |
| 3.7 | Integration tests: checkout page renders, auth required, COD end-to-end via service | `Features/Orders/` |

#### Checkout page behavior

1. Load cart; if empty, redirect to `/cart`.
2. Show address form (validate required fields per FR-ORD-02) + summary reusing cart totals.
3. Payment method radio defaults to COD.
4. On place order (COD): call `CreateOrderFromCartAsync` → navigate to `/orders/{orderNumber}`.
5. Surface `ServiceResult.Error` inline (e.g. stock changed) and refresh cart.

#### Acceptance criteria

- [x] Signed-out user is redirected to login for `/checkout`, `/orders`.
- [x] COD checkout produces an order and confirmation page.
- [x] Order history lists the user's orders (newest first); detail matches snapshot.
- [x] Address validation blocks incomplete submissions.

#### Estimated effort

**4–6 days**

---

### Phase 4 — PayPal gateway

**Objective:** Add PayPal (USD, Orders v2 REST, capture-on-return) behind an `IPaymentGateway` abstraction.

#### Tasks

| # | Task | Deliverable |
|---|------|-------------|
| 4.1 | `IPaymentGateway` (`Method`, `CreatePaymentAsync`, `CapturePaymentAsync`) + `PaymentInitiation` / `PaymentResult` | `Services/Payments/` |
| 4.2 | `CodPaymentGateway` (immediate/no-op) | `Services/Payments/` |
| 4.3 | `PayPalOptions` (client id/secret/base URL) bound from config; user secrets in dev | `Services/Payments/PayPalOptions.cs` |
| 4.4 | `PayPalPaymentGateway` — create order (USD), return approval URL, capture by order id | `Services/Payments/` |
| 4.5 | Gateway resolver by `PaymentMethod` (keyed DI or factory) | `Program.cs` / factory |
| 4.6 | Order flow: PayPal path creates order `Pending`, returns approval redirect | `OrderService` |
| 4.7 | `/checkout/paypal/return` — capture, mark `Paid`/`Completed`, store `GatewayTransactionRef`, clear cart | `Components/Pages/Checkout/PayPalReturn.razor` |
| 4.8 | `/checkout/paypal/cancel` — mark `Failed`, keep order unpaid, allow retry | `Components/Pages/Checkout/PayPalCancel.razor` |
| 4.9 | Idempotent capture (unique ref + status guard) | `OrderService` / gateway |
| 4.10 | Integration tests with a **fake** `IPaymentGateway` (no network) | `Features/Orders/` |

#### PayPal flow (capture-on-return)

1. Checkout with PayPal → create order (`Pending`) + PayPal order (USD `Total`).
2. Redirect to PayPal approval URL.
3. Approve → return to `/checkout/paypal/return?token={paypalOrderId}`.
4. Capture; on success → `Status = Paid`, `PaymentStatus = Completed`, store ref, clear cart, show confirmation.
5. On failure/cancel → `PaymentStatus = Failed` (order stays unpaid); offer retry. Never double-capture (FR-PAY-04, NFR-AVAIL-03).

#### Security / config

- Client id/secret from user secrets / env vars only (FR-PAY-07).
- Recompute/verify captured amount equals `Order.Total` (FR-PAY-02).
- Store only the transaction reference — no card data (FR-PAY-06).
- Sandbox credentials in development; live creds via environment in production.

#### Acceptance criteria

- [ ] Successful PayPal approval creates a `Paid` order with stored transaction ref.
- [ ] Cancel/failure leaves an unpaid order the user can retry.
- [ ] Duplicate return/refresh does not double-capture or double-clear.
- [ ] Captured amount matches order total; stored currency is USD.

#### Estimated effort

**5–7 days**

---

### Phase 5 — Admin order management

**Objective:** Admin lists/filters orders, views details, progresses status, and cancels (restoring stock).

#### Tasks

| # | Task | Deliverable |
|---|------|-------------|
| 5.1 | Admin order queries: `GetPagedAsync` (status + date filters), `GetByIdAsync` | `OrderService` (admin methods) |
| 5.2 | `UpdateStatusAsync` with valid transition guard (SRS 6.3) | `OrderService` |
| 5.3 | `CancelAsync` — set `Cancelled`, **restore stock**, mark payment where applicable | `OrderService` |
| 5.4 | (Should) Internal notes / status history for audit (FR-ADM-ORD-04, NFR-SEC-04) | entity/field |
| 5.5 | `/admin/orders` list page with filters | `Components/Admin/Pages/Orders/Index.razor` |
| 5.6 | `/admin/orders/{orderNumber}` detail with status controls | `Components/Admin/Pages/Orders/Detail.razor` |
| 5.7 | `[Authorize(Policy = "Admin")]` on admin order routes | |
| 5.8 | Integration tests: filters, transitions, cancel-restores-stock | `Features/Orders/` |

#### Status workflow (SRS 6.3)

```
Pending ──► Paid ──► Processing ──► Shipped ──► Delivered
   │           │
   └───────────┴──► Cancelled   (restores stock)
```

#### Acceptance criteria

- [ ] Admin can filter orders by status and date range.
- [ ] Only valid status transitions are allowed.
- [ ] Cancelling an order restores variant stock exactly once.
- [ ] Admin detail shows items, totals, payment status, and shipping address.

#### Estimated effort

**5–7 days**

---

## 7. Target file structure

```
d:\DevZone\Nexus\
├── Data/
│   ├── Entities/
│   │   ├── Order.cs                    (new)
│   │   ├── OrderItem.cs                (new)
│   │   ├── Payment.cs                  (new)
│   │   ├── OrderStatus.cs             (enum, new)
│   │   ├── PaymentMethod.cs           (enum, new)
│   │   ├── PaymentStatus.cs           (enum, new)
│   │   └── ProductVariant.cs           (add RowVersion)
│   └── Migrations/                     (AddOrdersAndPayments)
├── Services/
│   ├── Orders/
│   │   ├── IOrderService.cs
│   │   ├── OrderService.cs
│   │   ├── OrderNumberGenerator.cs
│   │   └── Models/
│   │       ├── CheckoutRequest.cs
│   │       ├── PlaceOrderResult.cs
│   │       ├── OrderDto.cs
│   │       ├── OrderLineDto.cs
│   │       ├── OrderSummaryDto.cs
│   │       └── OrderQuery.cs
│   ├── Payments/
│   │   ├── IPaymentGateway.cs
│   │   ├── CodPaymentGateway.cs
│   │   ├── PayPalPaymentGateway.cs
│   │   ├── PayPalOptions.cs
│   │   └── Models/ (PaymentInitiation, PaymentResult)
│   └── (shared) MoneyFormatter.cs
├── Components/
│   ├── Pages/
│   │   ├── Checkout/
│   │   │   ├── Index.razor
│   │   │   ├── PayPalReturn.razor
│   │   │   └── PayPalCancel.razor
│   │   └── Orders/
│   │       ├── Index.razor             (history)
│   │       └── Detail.razor            (confirmation + detail)
│   └── Admin/
│       └── Pages/
│           └── Orders/
│               ├── Index.razor
│               └── Detail.razor
└── Nexus.Test.Integration/
    └── Features/
        └── Orders/
            ├── OrderServiceTests.cs
            ├── OrderPageTests.cs
            └── AdminOrderTests.cs
```

---

## 8. Service API sketch

Mirror `ICartService` / `IProductService` style — async, `CancellationToken`, `ServiceResult<T>` for commands.

```csharp
public interface IOrderService
{
    // Phase 2–4 (customer)
    Task<ServiceResult<PlaceOrderResult>> CreateOrderFromCartAsync(
        string userId, CheckoutRequest request, CancellationToken ct = default);

    Task<OrderDto?> GetOrderAsync(
        string userId, string orderNumber, CancellationToken ct = default);

    Task<IReadOnlyList<OrderSummaryDto>> GetOrderHistoryAsync(
        string userId, CancellationToken ct = default);

    // Phase 4 (PayPal capture)
    Task<ServiceResult<OrderDto>> CompletePayPalPaymentAsync(
        string userId, string paypalOrderId, CancellationToken ct = default);

    Task<ServiceResult> MarkPayPalCancelledAsync(
        string userId, string paypalOrderId, CancellationToken ct = default);

    // Phase 5 (admin)
    Task<PagedResult<OrderSummaryDto>> GetPagedAsync(OrderQuery query, CancellationToken ct = default);
    Task<OrderDto?> GetByIdAsync(int orderId, CancellationToken ct = default);
    Task<ServiceResult<OrderDto>> UpdateStatusAsync(int orderId, OrderStatus next, CancellationToken ct = default);
    Task<ServiceResult<OrderDto>> CancelAsync(int orderId, CancellationToken ct = default);
}

public interface IPaymentGateway
{
    PaymentMethod Method { get; }
    Task<ServiceResult<PaymentInitiation>> CreatePaymentAsync(Order order, CancellationToken ct = default);
    Task<ServiceResult<PaymentResult>> CapturePaymentAsync(string gatewayOrderId, CancellationToken ct = default);
}
```

`PlaceOrderResult` carries `OrderNumber` plus an optional `RedirectUrl` (PayPal approval link) so the checkout page knows whether to confirm or redirect.

---

## 9. Testing strategy

Follow the established `Nexus.Test.Integration` pattern (Testcontainers, `TestDatabaseFixture.ResetAsync`, `DbHelper`, FluentAssertions, xUnit).

| Layer | What to test |
|-------|----------------|
| **Order service** | Create-from-cart, empty-cart reject, stale-stock reject, total snapshot, cart cleared, stock decrement + restore, per-user isolation |
| **Payment** | Fake `IPaymentGateway` for PayPal path; capture success/failure; idempotent double-capture guard |
| **Page** | `/checkout`, `/orders` require Customer; `/admin/orders` requires Admin |
| **Admin** | Status transition validity; cancel restores stock exactly once; filters |

**Test data:** extend `DbHelper` with `GetOrdersAsync(userId)`, `GetOrderItemsAsync(orderId)`, `GetPaymentsAsync(orderId)`; reuse `SeedVariantAsync` helper from `CartServiceTests`.

**Key scenarios:**

1. COD create → order `Pending`, payment `Cod/Pending`, stock deducted, cart empty.
2. Concurrent checkout on last unit → exactly one order succeeds (no oversell).
3. PayPal capture success → order `Paid`, payment `Completed`, ref stored.
4. PayPal cancel → order stays unpaid; retry allowed; no double stock deduction.
5. Admin cancel → status `Cancelled`, stock restored once.
6. Totals equal `CartPricing` output at order time.

---

## 10. Dependencies & risks

| Risk | Mitigation |
|------|------------|
| Currency drift (VND vs USD) | Do Phase 0 first; single `MoneyFormatter`; re-baseline tests |
| Oversell under concurrency | Conditional `ExecuteUpdateAsync` guard + optional `RowVersion` |
| Double capture / double stock deduct | Unique `GatewayTransactionRef` + status guard; idempotent handlers |
| PayPal sandbox/config friction | Options class + user secrets; document sandbox setup |
| No webhook in v1 | Capture-on-return is acceptable for v1; note webhook as follow-up for reliability |
| Scope creep (refunds, guest, email) | Explicitly out of scope; track as follow-ups |
| Stale cart at checkout | Re-validate stock/active and recompute totals server-side before persisting |

### External dependencies

- PayPal developer account + sandbox app (client id/secret).
- SQL Server via EF Core (existing).
- Docker for integration tests (existing).
- Completed variant catalog + cart (existing).

---

## 11. Milestone summary

| Phase | Milestone | Customer | Admin | Tests |
|-------|-----------|----------|-------|-------|
| **0** | Currency → USD | USD pricing | — | Cart re-baselined |
| **1** | Order/payment schema | — | — | DbHelper + migration |
| **2** | Order service (COD) | — | — | Service |
| **3** | Checkout + order pages | COD end-to-end | — | Service + page |
| **4** | PayPal gateway | PayPal end-to-end | — | Fake gateway |
| **5** | Admin orders | — | List/detail/status | Admin + cancel |

**Total estimated effort:** ~21–31 days (Phase 0 through Phase 5).

---

## 12. Definition of done (per phase)

- [ ] EF migration applied locally (where applicable)
- [ ] Services registered in `Program.cs`
- [ ] Customer or admin UI functional for phase scope
- [ ] Integration tests added and passing (`dotnet test Nexus.sln`)
- [ ] Totals recomputed server-side; snapshots immutable
- [ ] Secrets kept out of source control
- [ ] This document checklist for the phase marked complete

---

## 13. References

| Resource | Path |
|----------|------|
| Software requirements | [`docs/SRS.md`](./SRS.md) |
| Product & variant roadmap | [`docs/product-variant-roadmap.md`](./product-variant-roadmap.md) |
| Cart implementation (pattern) | `Services/Cart/`, `Components/Pages/Cart/Index.razor` |
| Cart pricing | `Services/Cart/Models/CartPricing.cs` |
| Result type | `Services/Categories/Models/ServiceResult.cs` |
| Integration test infrastructure | `Nexus.Test.Integration/` |
| PayPal Orders v2 REST | https://developer.paypal.com/docs/api/orders/v2/ |

---

## 14. Phase checklists (copy for tracking)

### Phase 0 — Currency
- [x] `CartPricing` USD constants
- [x] Shared `MoneyFormatter` (used by cart, storefront, and admin components)
- [x] Cart page uses USD
- [x] Cart tests re-baselined to USD (execution blocked by pre-existing test-harness migration issue, not the currency change)

### Phase 1 — Schema
- [x] Enums + `Order`/`OrderItem`/`Payment` entities
- [x] EF config (precision, indexes, delete behavior, enum→string)
- [x] ~~`RowVersion` on `ProductVariant`~~ (intentionally skipped per decision)
- [x] Migration `AddOrdersAndPayments` applied
- [x] `DbHelper` order/payment helpers

### Phase 2 — Order service (COD)
- [x] `IOrderService` + models
- [x] Create-from-cart with snapshot + total recompute
- [x] Atomic stock deduction + transaction
- [x] Order number generator
- [x] History + get-by-number
- [x] Service tests green (also fixed the pre-existing test-harness startup double-migration/seed so the full suite runs; 93/93 green)

### Phase 3 — Checkout & order pages
- [x] `/checkout` page (address + method + summary)
- [x] Cart button wired to checkout
- [x] COD confirmation flow
- [x] `/orders` history + `/orders/{orderNumber}` detail
- [x] Auth enforced
- [x] Page tests green (100/100 suite)

### Phase 4 — PayPal
- [ ] `IPaymentGateway` + `CodPaymentGateway`
- [ ] `PayPalOptions` + secrets
- [ ] `PayPalPaymentGateway` (create + capture, USD)
- [ ] Return/cancel handlers
- [ ] Idempotent capture
- [ ] Fake-gateway tests green

### Phase 5 — Admin orders
- [ ] Admin paged query + filters
- [ ] Status transition guard
- [ ] Cancel restores stock
- [ ] Admin list + detail pages
- [ ] Admin tests green
