# Software Requirements Specification (SRS)

## Nexus — Technology Product E-Commerce Platform

| Field | Value |
|-------|-------|
| **Document ID** | SRS-NEXUS-001 |
| **Version** | 1.0 |
| **Status** | Draft |
| **Date** | 2026-07-06 |
| **Project** | Nexus |

---

## Table of Contents

1. [Introduction](#1-introduction)
2. [Overall Description](#2-overall-description)
3. [System Features and Functional Requirements](#3-system-features-and-functional-requirements)
4. [External Interface Requirements](#4-external-interface-requirements)
5. [Non-Functional Requirements](#5-non-functional-requirements)
6. [Data Requirements](#6-data-requirements)
7. [Use Cases](#7-use-cases)
8. [Assumptions and Constraints](#8-assumptions-and-constraints)
9. [Appendix](#9-appendix)

---

## 1. Introduction

### 1.1 Purpose

This Software Requirements Specification (SRS) defines the functional and non-functional requirements for **Nexus**, an online e-commerce platform for selling technology products. The document is intended for:

- **Developers** — to design and implement the system
- **Project stakeholders** — to validate scope and acceptance criteria
- **Testers** — to derive test cases and verify compliance

### 1.2 Scope

Nexus is a web-based application that enables:

- **Administrators** to manage the product catalog and customer orders
- **Customers** to browse products, manage a shopping cart, place orders, and complete payments

The system does **not** include physical inventory logistics (warehouse management, shipping carrier integration) unless explicitly added in a future release.

### 1.3 Definitions, Acronyms, and Abbreviations

| Term | Definition |
|------|------------|
| **Admin** | A privileged user who manages products and orders |
| **Customer** | A registered or guest user who browses and purchases products |
| **Cart** | A temporary collection of products selected for purchase |
| **Checkout** | The process of confirming cart contents and initiating payment |
| **Order** | A confirmed purchase record created after successful checkout |
| **Payment Gateway** | A third-party service that processes card or digital payments |
| **SRS** | Software Requirements Specification |
| **EF Core** | Entity Framework Core (ORM) |
| **Blazor** | ASP.NET Core UI framework |

### 1.4 References

| ID | Document |
|----|----------|
| REF-01 | IEEE Std 830-1998 — Recommended Practice for Software Requirements Specifications |
| REF-02 | Nexus project repository: `D:\DevZone\Nexus` |
| REF-03 | ASP.NET Core Identity documentation |

### 1.5 Overview

The remainder of this document describes the system context (Section 2), detailed functional requirements (Section 3), interface requirements (Section 4), non-functional requirements (Section 5), data model expectations (Section 6), use cases (Section 7), and project constraints (Section 8).

---

## 2. Overall Description

### 2.1 Product Perspective

Nexus is a standalone web application built on the following technology stack (current baseline):

| Layer | Technology |
|-------|------------|
| **Frontend** | Blazor Web App (Interactive Server render mode) |
| **Backend** | ASP.NET Core 10 |
| **Authentication** | ASP.NET Core Identity |
| **Database** | Microsoft SQL Server (via Entity Framework Core) |
| **Hosting** | HTTPS-enabled web server |

Identity scaffolding (registration, login, password reset, 2FA, passkeys) is already present in the project. E-commerce features (products, cart, orders, payments) are **to be implemented** per this specification.

### 2.2 Product Functions (Summary)

| Role | Primary Functions |
|------|-------------------|
| **Admin** | Product CRUD; order viewing, updating, and processing |
| **Customer** | Product browsing; cart management; checkout; payment |

### 2.3 User Classes and Characteristics

#### 2.3.1 Administrator

- Manages catalog and fulfillment workflow
- Requires elevated privileges (`Admin` role)
- Expected to use the system regularly during business hours
- Familiar with basic web applications

#### 2.3.2 Customer

- Browses and purchases technology products
- May be a registered user (persistent cart, order history) or guest (session-based cart, limited history)
- Varying technical proficiency; interface must be intuitive

### 2.4 Operating Environment

| Component | Requirement |
|-----------|-------------|
| **Client** | Modern web browser (Chrome, Edge, Firefox, Safari — last two major versions) |
| **Server** | Windows or Linux host capable of running .NET 10 |
| **Database** | SQL Server 2019+ or Azure SQL Database |
| **Network** | HTTPS required in production |

### 2.5 Design and Implementation Constraints

- Must use ASP.NET Core Identity for authentication and role-based authorization
- Must persist business data in SQL Server via Entity Framework Core
- Payment processing must use a reputable third-party payment gateway (e.g., Stripe, PayPal, or VNPay — final provider TBD)
- Sensitive credentials (API keys, connection strings) must not be stored in source control

### 2.6 Assumptions and Dependencies

- Product images and descriptions are provided by Admin users
- Payment gateway account and API credentials will be provisioned before checkout goes live
- Email delivery for account confirmation is configured for production (development may use a no-op sender)
- Tax and shipping rules follow a simplified flat-rate or free-shipping model in v1 unless otherwise specified

---

## 3. System Features and Functional Requirements

Requirements use the format **FR-&lt;module&gt;-&lt;number&gt;** for traceability.

### 3.1 Authentication and Authorization

| ID | Requirement | Priority |
|----|-------------|----------|
| FR-AUTH-01 | The system shall allow users to register an account with email and password | Must |
| FR-AUTH-02 | The system shall require email confirmation before a Customer can sign in (configurable per environment) | Must |
| FR-AUTH-03 | The system shall allow registered users to sign in and sign out securely | Must |
| FR-AUTH-04 | The system shall support password reset via email | Must |
| FR-AUTH-05 | The system shall assign each user exactly one primary role: `Admin` or `Customer` | Must |
| FR-AUTH-06 | The system shall restrict Admin-only pages and APIs to users in the `Admin` role | Must |
| FR-AUTH-07 | The system shall restrict Customer cart, checkout, and order history to authenticated Customers (guest checkout optional — see FR-ORD-06) | Must |
| FR-AUTH-08 | The system shall support optional two-factor authentication (2FA) for account security | Should |

### 3.2 Product Management (Admin)

| ID | Requirement | Priority |
|----|-------------|----------|
| FR-PRD-01 | Admin shall be able to create a product with: name, description, price, stock quantity, category, and image URL(s) | Must |
| FR-PRD-02 | Admin shall be able to view a paginated list of all products | Must |
| FR-PRD-03 | Admin shall be able to update any product field | Must |
| FR-PRD-04 | Admin shall be able to delete (or soft-delete) a product | Must |
| FR-PRD-05 | Deleted or inactive products shall not appear in the public catalog | Must |
| FR-PRD-06 | Admin shall be able to filter and search products by name and category | Should |
| FR-PRD-07 | Product price shall be stored and displayed in a single configured currency (e.g., VND or USD) | Must |

### 3.3 Product Catalog (Customer)

| ID | Requirement | Priority |
|----|-------------|----------|
| FR-CAT-01 | Customer shall be able to browse all active products | Must |
| FR-CAT-02 | Customer shall be able to view product detail pages (name, description, price, availability, images) | Must |
| FR-CAT-03 | Customer shall be able to filter products by category | Should |
| FR-CAT-04 | Customer shall be able to search products by keyword | Should |
| FR-CAT-05 | Out-of-stock products shall be visible but marked unavailable for add-to-cart | Must |

### 3.4 Shopping Cart (Customer)

| ID | Requirement | Priority |
|----|-------------|----------|
| FR-CART-01 | Customer shall be able to add a product to the cart from the catalog or product detail page | Must |
| FR-CART-02 | Customer shall be able to view all items in the cart with quantity, unit price, and line total | Must |
| FR-CART-03 | Customer shall be able to update item quantity in the cart | Must |
| FR-CART-04 | Customer shall be able to remove an item from the cart | Must |
| FR-CART-05 | The cart shall display subtotal before tax and shipping | Must |
| FR-CART-06 | The system shall prevent adding more items than available stock | Must |
| FR-CART-07 | For authenticated users, the cart shall persist across sessions | Must |
| FR-CART-08 | For guest users, the cart shall persist for the browser session (cookie or server session) | Should |

### 3.5 Checkout and Orders (Customer)

| ID | Requirement | Priority |
|----|-------------|----------|
| FR-ORD-01 | Customer shall be able to initiate checkout from the cart | Must |
| FR-ORD-02 | Checkout shall collect shipping address: full name, phone, street, city, state/province, postal code, country | Must |
| FR-ORD-03 | Checkout shall display an order summary before payment | Must |
| FR-ORD-04 | Upon successful payment, the system shall create an order with status `Pending` or `Paid` | Must |
| FR-ORD-05 | Customer shall receive an order confirmation (on-screen; email notification Should) | Must |
| FR-ORD-06 | Guest checkout may be supported; if enabled, order lookup requires order ID and email | Could |
| FR-ORD-07 | Customer shall be able to view their order history (authenticated users) | Must |
| FR-ORD-08 | Customer shall be able to view order detail including items, totals, status, and shipping address | Must |

### 3.6 Order Management (Admin)

| ID | Requirement | Priority |
|----|-------------|----------|
| FR-ADM-ORD-01 | Admin shall be able to view a list of all orders with filters by status and date | Must |
| FR-ADM-ORD-02 | Admin shall be able to view full order details | Must |
| FR-ADM-ORD-03 | Admin shall be able to update order status through defined states (see Section 6.3) | Must |
| FR-ADM-ORD-04 | Admin shall be able to add internal notes to an order | Should |
| FR-ADM-ORD-05 | When an order is marked `Shipped` or `Delivered`, stock quantities shall reflect prior reservation/deduction | Must |
| FR-ADM-ORD-06 | Admin shall be able to cancel an order; cancelled orders shall restore reserved stock | Must |

### 3.7 Payment

| ID | Requirement | Priority |
|----|-------------|----------|
| FR-PAY-01 | The system shall integrate with a third-party payment gateway for card/digital payments | Must |
| FR-PAY-02 | Payment amount shall match the order total at checkout time | Must |
| FR-PAY-03 | The system shall record payment status: `Pending`, `Completed`, `Failed`, `Refunded` | Must |
| FR-PAY-04 | Failed payments shall not create a completed order | Must |
| FR-PAY-05 | The system shall store a payment transaction reference from the gateway | Must |
| FR-PAY-06 | No raw card numbers or CVV shall be stored in the application database | Must |
| FR-PAY-07 | Payment secrets (API keys) shall be loaded from configuration or secret store only | Must |

---

## 4. External Interface Requirements

### 4.1 User Interfaces

| Screen Area | Description |
|-------------|-------------|
| **Public storefront** | Home, product listing, product detail, search/filter |
| **Cart & checkout** | Cart summary, shipping form, payment step, confirmation |
| **Customer account** | Order history, order detail, profile (extends existing Identity pages) |
| **Admin dashboard** | Product management, order management |
| **Authentication** | Login, register, password reset (existing Identity UI) |

**UI guidelines:**

- Responsive layout for desktop and mobile viewports
- Clear calls-to-action (Add to Cart, Checkout, Pay Now)
- Accessible form labels and error messages
- Consistent navigation between catalog, cart, and account areas

### 4.2 Hardware Interfaces

None. Nexus is a web-only application.

### 4.3 Software Interfaces

| Interface | Purpose |
|-----------|---------|
| **SQL Server** | Persistent storage for users, products, carts, orders, payments |
| **Payment gateway REST/SDK API** | Process payments and receive webhooks |
| **SMTP / email provider** (production) | Account confirmation, password reset, optional order emails |

### 4.4 Communication Interfaces

- All client-server communication over **HTTPS** (TLS 1.2+)
- Payment gateway webhooks over HTTPS with signature verification
- Standard HTTP status codes for API responses where applicable

---

## 5. Non-Functional Requirements

### 5.1 Security

| ID | Requirement |
|----|-------------|
| NFR-SEC-01 | Passwords shall be hashed using ASP.NET Core Identity defaults (PBKDF2 or successor) |
| NFR-SEC-02 | All authenticated pages shall enforce authorization checks server-side |
| NFR-SEC-03 | CSRF protection shall be enabled for form submissions (antiforgery tokens) |
| NFR-SEC-04 | Admin actions shall be auditable (who changed order status and when) |
| NFR-SEC-05 | PCI-DSS scope shall be minimized by using hosted payment fields or redirect/checkout session from the gateway |

### 5.2 Performance

| ID | Requirement |
|----|-------------|
| NFR-PERF-01 | Product listing page shall load within 3 seconds under normal load (≤ 100 concurrent users) |
| NFR-PERF-02 | Database queries for catalog browsing shall use pagination |
| NFR-PERF-03 | Cart operations shall complete within 1 second under normal load |

### 5.3 Scalability

| ID | Requirement |
|----|-------------|
| NFR-SCALE-01 | Architecture shall support horizontal scaling of the web tier (stateless app server where possible) |
| NFR-SCALE-02 | Database schema shall support at least 10,000 products and 100,000 orders without redesign |
| NFR-SCALE-03 | Blazor Server circuit affinity shall be considered when scaling (sticky sessions or migration to mixed/wasm render modes for scale-out) |

### 5.4 Availability and Reliability

| ID | Requirement |
|----|-------------|
| NFR-AVAIL-01 | Target uptime of 99.5% in production (excluding planned maintenance) |
| NFR-AVAIL-02 | Database backups shall be performed daily in production |
| NFR-AVAIL-03 | Payment failures shall be handled gracefully with user-visible error messages and no duplicate charges |

### 5.5 Usability

| ID | Requirement |
|----|-------------|
| NFR-USE-01 | A new Customer shall complete a purchase in ≤ 5 clicks from the product page (excluding payment provider steps) |
| NFR-USE-02 | Error messages shall be written in plain language |
| NFR-USE-03 | UI shall follow a consistent layout (shared main layout and navigation) |

### 5.6 Maintainability

| ID | Requirement |
|----|-------------|
| NFR-MAINT-01 | Business logic shall be separated from UI components (services/repositories pattern) |
| NFR-MAINT-02 | Database schema changes shall be applied via EF Core migrations |
| NFR-MAINT-03 | Configuration shall be environment-specific (`appsettings.{Environment}.json`, user secrets, environment variables) |

### 5.7 Legal and Compliance

| ID | Requirement |
|----|-------------|
| NFR-LEGAL-01 | Privacy policy and terms of service pages shall be linked from checkout |
| NFR-LEGAL-02 | Customer personal data shall be deletable upon request (extends existing Identity personal data management) |

---

## 6. Data Requirements

### 6.1 Core Entities (Logical Model)

```
┌─────────────┐     ┌─────────────┐     ┌──────────────┐
│ Application │     │   Product   │     │   Category   │
│    User     │     ├─────────────┤     ├──────────────┤
├─────────────┤     │ Id          │     │ Id           │
│ Id          │     │ Name        │     │ Name         │
│ Email       │     │ Description │     └──────┬───────┘
│ Role        │     │ Price       │            │
└──────┬──────┘     │ StockQty    │◄───────────┘
       │            │ ImageUrl    │
       │            │ IsActive    │
       │            └──────┬──────┘
       │                   │
       ▼                   ▼
┌─────────────┐     ┌─────────────┐     ┌──────────────┐
│    Cart     │────►│  CartItem   │     │    Order     │
├─────────────┤     ├─────────────┤     ├──────────────┤
│ Id          │     │ ProductId   │     │ Id           │
│ UserId      │     │ Quantity    │     │ UserId       │
└─────────────┘     └─────────────┘     │ Status       │
                                        │ TotalAmount  │
                                        │ ShippingAddr │
                                        │ CreatedAt    │
                                        └──────┬───────┘
                                               │
                    ┌─────────────┐            ▼
                    │   Payment   │     ┌──────────────┐
                    ├─────────────┤     │  OrderItem   │
                    │ OrderId     │     ├──────────────┤
                    │ GatewayRef  │     │ ProductId    │
                    │ Amount      │     │ Quantity     │
                    │ Status      │     │ UnitPrice    │
                    └─────────────┘     └──────────────┘
```

### 6.2 Entity Descriptions

| Entity | Key Attributes | Notes |
|--------|----------------|-------|
| **Product** | Name, description, price, stock, category, images, active flag | Soft-delete via `IsActive` |
| **Category** | Name, optional parent for hierarchy | Technology subcategories (e.g., Laptops, Phones) |
| **Cart / CartItem** | User or session reference, product, quantity | One cart per user |
| **Order** | User, status, totals, shipping address, timestamps | Immutable line prices at order time |
| **OrderItem** | Product snapshot, quantity, unit price | Preserves price even if product changes later |
| **Payment** | Order reference, gateway transaction ID, status, amount | Linked 1:1 or 1:many per gateway design |

### 6.3 Order Status Workflow

```
Pending ──► Paid ──► Processing ──► Shipped ──► Delivered
   │           │
   └───────────┴──► Cancelled
```

| Status | Description |
|--------|-------------|
| **Pending** | Order created; awaiting payment |
| **Paid** | Payment confirmed |
| **Processing** | Admin is preparing the order |
| **Shipped** | Order dispatched |
| **Delivered** | Customer received the order |
| **Cancelled** | Order cancelled; stock restored if applicable |

### 6.4 Data Retention

- Order and payment records: retain minimum 7 years for financial audit (configurable)
- Abandoned carts: purge after 30 days of inactivity
- Inactive user accounts: follow organizational privacy policy

---

## 7. Use Cases

### 7.1 Use Case Diagram (Textual)

```
                    ┌──────────────────────────────────────┐
                    │            Nexus System               │
                    │                                       │
   ┌────────┐       │  ┌─────────────┐  ┌───────────────┐  │
   │ Admin  │───────┼─►│ Manage      │  │ Manage Orders │  │
   └────────┘       │  │ Products    │  └───────────────┘  │
                    │  └─────────────┘                      │
   ┌──────────┐     │  ┌─────────────┐  ┌───────────────┐  │
   │ Customer │─────┼─►│ Browse /    │  │ Checkout &    │  │
   └──────────┘     │  │ Search      │  │ Pay           │  │
                    │  └─────────────┘  └───────────────┘  │
                    │  ┌─────────────┐                      │
                    │  │ Manage Cart │                      │
                    │  └─────────────┘                      │
                    └──────────────────────────────────────┘
```

### 7.2 Use Case Specifications

#### UC-01: Admin Adds Product

| Field | Detail |
|-------|--------|
| **Actor** | Admin |
| **Preconditions** | Admin is authenticated with `Admin` role |
| **Main Flow** | 1. Admin opens product management → 2. Clicks Add Product → 3. Enters product details → 4. Submits → 5. System validates and saves → 6. Product appears in catalog |
| **Alternate Flow** | 4a. Validation fails → system shows errors |
| **Postconditions** | New product is stored and visible in Admin list and public catalog |

#### UC-02: Customer Adds Item to Cart

| Field | Detail |
|-------|--------|
| **Actor** | Customer |
| **Preconditions** | Product is active and in stock |
| **Main Flow** | 1. Customer views product → 2. Selects quantity → 3. Clicks Add to Cart → 4. System updates cart → 5. Confirmation shown |
| **Alternate Flow** | 3a. Insufficient stock → error message |
| **Postconditions** | Cart contains the product with requested quantity |

#### UC-03: Customer Checks Out and Pays

| Field | Detail |
|-------|--------|
| **Actor** | Customer |
| **Preconditions** | Cart is non-empty; user authenticated (or guest flow enabled) |
| **Main Flow** | 1. Open cart → 2. Proceed to checkout → 3. Enter shipping address → 4. Review summary → 5. Redirect to payment gateway → 6. Complete payment → 7. Gateway confirms → 8. Order created with status `Paid` → 9. Confirmation displayed |
| **Alternate Flow** | 6a. Payment fails → order remains unpaid or is marked failed; user may retry |
| **Postconditions** | Order and payment records exist; stock decremented or reserved |

#### UC-04: Admin Processes Order

| Field | Detail |
|-------|--------|
| **Actor** | Admin |
| **Preconditions** | Order exists with status `Paid` or `Processing` |
| **Main Flow** | 1. Admin opens order list → 2. Selects order → 3. Updates status (e.g., to `Shipped`) → 4. System records change and timestamp |
| **Postconditions** | Order status updated; customer can see new status in order history |

---

## 8. Assumptions and Constraints

### 8.1 Assumptions

1. Single-store, single-currency deployment for v1
2. Admin users are created by seeding or manual database assignment initially
3. Product images are hosted via URL (external CDN or `wwwroot` uploads)
4. Shipping cost is a flat rate or free for v1

### 8.2 Constraints

1. Must remain compatible with .NET 10 and existing Identity schema
2. Budget and timeline dictate payment provider selection
3. Blazor Server imposes SignalR connection limits per server instance

### 8.3 Out of Scope (v1)

- Multi-vendor marketplace
- Product reviews and ratings
- Wish lists and product recommendations
- Real-time inventory sync with external ERP
- Native mobile applications
- Multi-language / multi-currency support

---

## 9. Appendix

### 9.1 Requirements Traceability Matrix (Summary)

| Business Need | Requirements |
|---------------|--------------|
| Admin adds products | FR-PRD-01 – FR-PRD-04 |
| Admin manages orders | FR-ADM-ORD-01 – FR-ADM-ORD-06 |
| Customer browses products | FR-CAT-01 – FR-CAT-05 |
| Customer manages cart | FR-CART-01 – FR-CART-08 |
| Customer checkout & payment | FR-ORD-01 – FR-ORD-08, FR-PAY-01 – FR-PAY-07 |
| Secure authentication | FR-AUTH-01 – FR-AUTH-08, NFR-SEC-01 – NFR-SEC-05 |
| Scalable architecture | NFR-SCALE-01 – NFR-SCALE-03 |
| User-friendly UI | NFR-USE-01 – NFR-USE-03 |

### 9.2 Suggested Implementation Phases

| Phase | Deliverables |
|-------|--------------|
| **Phase 1** | Roles, product entities, Admin product CRUD, public catalog |
| **Phase 2** | Shopping cart (persistent), checkout flow |
| **Phase 3** | Payment gateway integration, order creation |
| **Phase 4** | Admin order management, email notifications, polish & performance |

### 9.3 Document Revision History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0 | 2026-07-06 | — | Initial SRS based on Nexus system specification |

---

*End of Document*
