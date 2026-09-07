# Locator conventions

Automated scenarios target **tier 1 `data-testid`**. Lower tiers are backups only.

## Ranking

Lower tier = more robust. Shared by JS (`classifyLocatorType` / `getDefaultTier`) and C# (`LocatorRanking`).

| Tier | Type | Example | Robustness |
|------|------|---------|------------|
| 1 | `testid` | `[data-testid="sign-in"]` | Highest — explicit test contract |
| 2 | `id` | `#login-submit` | High — skip ids matching `/\d{4,}/` |
| 3 | `name` / `aria-label` | `input[name="email"]` | High — language-agnostic form fields |
| 4 | `role` | `role=button[name="Sign in"]` | Medium — semantic, copy-sensitive |
| 5 | `href` | `a[href="/login"]` | Medium — links only |
| 6 | `text` | `text="Sign in"` | Low — i18n / A/B fragile |
| 7 | `css-path` | `form > div > button:nth-of-type(2)` | Lowest — last resort |

## Naming

```
{area}-{element}[-qualifier]
```

- Always `data-testid` (never `data-test-id`).
- Kebab-case. No 4+ digit runs in the value.
- One unique name per singleton control.
- Lists reuse the same testid; distinguish with `data-slug`, `data-order-number`, or `data-name`.
- Put `data-testid` on Blazor `InputText` / `InputCheckbox` / `InputSelect` so it renders on the native control.

## Inventory

### Chrome

| testid | Where |
|--------|--------|
| `header-logo-link` | Site header logo |
| `header-nav-home` / `header-nav-shop` | Main nav |
| `header-search-input` | Header search |
| `header-wishlist-button` | Wishlist |
| `header-cart-link` / `header-cart-count` | Cart |
| `header-account-button` | Signed-in account menu |
| `header-orders-link` | Order history |
| `header-manage-link` | Account manage |
| `header-logout-button` | Logout |
| `header-login-link` | Sign in |
| `auth-layout` / `auth-home-link` | Auth layout |
| `admin-nav-dashboard` / `admin-nav-products` / `admin-nav-categories` / `admin-nav-orders` | Admin nav |
| `manage-nav-profile` / `manage-nav-addresses` / `manage-nav-email` / `manage-nav-password` / `manage-nav-2fa` / `manage-nav-passkeys` / `manage-nav-personal-data` | Manage nav |
| `status-message` | Shared status banner |
| `external-login-google` (etc.) | External login buttons |

### Auth

| testid | Where |
|--------|--------|
| `login-page` / `login-form` / `login-email-input` / `login-password-input` / `login-submit-button` | Login |
| `register-page` / `register-form` / `register-fullname-input` / `register-email-input` / `register-password-input` / `register-confirm-password-input` / `register-accept-terms` / `register-submit-button` | Register |
| `forgot-password-page` / `forgot-password-email-input` / `forgot-password-submit-button` | Forgot password |
| `reset-password-page` / `reset-password-submit-button` | Reset password |

### Storefront

| testid | Where |
|--------|--------|
| `home-page` | Home |
| `newsletter-email-input` / `newsletter-submit-button` | Newsletter |
| `shop-page` / `shop-search-input` / `shop-category-select` | Catalog |
| `product-card` + `data-slug` | Product card |
| `product-card-add-to-cart` / `product-card-choose-options` | Card CTAs |
| `pdp-page` / `pdp-add-to-cart` / `pdp-option` + `data-name` | Product detail (`pdp-add-to-cart` applies only when `DemoAppSettings.AddToCartCtaVariant` is `Baseline`; the active variant is stored in the DB and is global for all browsers sharing that database — other values are intentional locator-break drills, not production conventions) |
| `cart-page` / `cart-line` + `data-slug` / `cart-qty-increase` / `cart-qty-decrease` / `cart-remove-item` / `cart-checkout-button` | Cart (`cart-checkout-button` applies only when `DemoAppSettings.CheckoutCtaVariant` is `Baseline`; the active variant is stored in the DB and is global for all browsers sharing that database — other values are intentional locator-break drills, not production conventions) |
| `checkout-page` / `checkout-place-order-button` / `checkout-recipient-input` | Checkout |
| `orders-page` / `order-row` + `data-order-number` | Customer orders |
| `error-page` / `not-found-page` | Error pages |

### Admin

| testid | Where |
|--------|--------|
| `admin-dashboard` | Dashboard |
| `admin-products-page` / `btn-add-product` | Product list |
| `product-drawer` / `btn-submit-product` / `input-product-name` | Product drawer (`btn-submit-product` applies only when `DemoAppSettings.AddProductCtaVariant` is `Baseline`; the active variant is stored in the DB and is global for all browsers sharing that database — other values are intentional locator-break drills, not production conventions) |
| `admin-product-detail` / `btn-save-product-info` | Product detail |
| `admin-categories-page` / `btn-add-category` | Category list |
| `category-drawer` / `btn-submit-category` | Category drawer |
| `admin-orders-page` / `admin-order-detail` | Orders |
| `toast-container` / `toast-item` | Toasts |
| `dialog-delete-category` / `btn-confirm-delete` | Delete confirm |

### Account manage

| testid | Where |
|--------|--------|
| `manage-profile-page` / `manage-profile-submit-button` | Profile |
| `manage-addresses-page` / `btn-add-address` | Address list |
| `manage-address-form` / `manage-address-submit-button` | Address edit |
| `manage-email-page` / `manage-password-page` / `manage-2fa-page` | Other manage pages |
