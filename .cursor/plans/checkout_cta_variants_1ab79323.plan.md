---
name: Checkout CTA Variants
overview: Add an enum-driven switch in the cart page so exactly one "Proceed to Checkout" CTA variant is rendered at a time, progressing from attribute/copy changes through semantic and boss-level mutations that break brittle locators while keeping checkout navigation intact.
todos:
  - id: enum-switch
    content: Add CheckoutCtaVariant enum + ActiveCheckoutCta constant in Cart Index.razor @code
    status: completed
  - id: render-variants
    content: Replace checkout CTA + commented block with @switch rendering Baseline, AttrAndCopy, AnchorSemantics, VietnameseSvg, BossDecoy
    status: completed
  - id: docs-note
    content: Note in docs/locator-conventions.md that only Baseline keeps cart-checkout-button as the convention
    status: completed
isProject: false
---

# Checkout CTA Locator-Break Variants

## Approach

Replace the commented-out button block in [`Components/Pages/Cart/Index.razor`](Components/Pages/Cart/Index.razor) with a single **enum constant** that selects which CTA markup to render. Flip the constant between runs; only one interactive checkout CTA is live at a time.

Stable signals kept for fair recovery drills:
- Still on `/cart` under Order Summary near Total
- Still navigates to `/checkout`
- `data-testid="cart-page"` unchanged
- Same disabled rule: `isBusy || cart.Items.Any(i => !i.InStock)`

## Variants (flip via one constant)

Add in `@code`:

```csharp
private enum CheckoutCtaVariant
{
    Baseline = 0,
    AttrAndCopy = 1,
    AnchorSemantics = 2,
    VietnameseSvg = 3,
    BossDecoy = 4
}

// Change this value to break/heal locator drills
private const CheckoutCtaVariant ActiveCheckoutCta = CheckoutCtaVariant.Baseline;
```

| Variant | What changes | What typically breaks |
|---------|--------------|------------------------|
| `Baseline` | Current markup: `button`, `data-testid="cart-checkout-button"`, "Proceed to Checkout", `fa-arrow-right` | — (control) |
| `AttrAndCopy` | `data-testid="btn-checkout-now"`, label "Secure Checkout", `fa-lock` | Exact testid + English text |
| `AnchorSemantics` | `<a href="/checkout">` with `@onclick:preventDefault` + same navigate handler; no `data-testid`; label "Proceed to Payment"; `fa-credit-card`; disabled via `pointer-events`/`opacity` like the existing commented `<a>` | `button` role / tag locators + testid |
| `VietnameseSvg` | `button`, `data-testid="checkout-step-1"`, class `checkout-action-btn` + blue/cyan gradient, label "Hoàn tất đơn hàng", inline SVG chevron (no Font Awesome) | English text + FA icon + old testid |
| `BossDecoy` | Real CTA: `<div role="button" tabindex="0">` with nested `<span>`s for a fragmented label (e.g. "Continue" + " to payment"), **no** `data-testid`; plus a nearby decoy `<button type="button" disabled>` labeled "Checkout options" that does nothing | testid, exact text, button-role on the real CTA, and text-only picks of "Checkout" |

Shared click behavior for all real CTAs: `@onclick` → `Navigation.NavigateTo("/checkout")` (anchor also uses `@onclick:preventDefault`).

## Markup structure

In the Order Summary section (replacing lines ~170–201):

```razor
@switch (ActiveCheckoutCta)
{
    case CheckoutCtaVariant.Baseline:
        @* current button *@
        break;
    case CheckoutCtaVariant.AttrAndCopy:
        ...
        break;
    // ...
}
```

Reuse the existing commented snippets as the first three alternate cases; implement `BossDecoy` as the new fourth case (real CTA + decoy). Place the decoy **above** the real CTA so naive “first Checkout*” queries fail.

## Docs

Add a short note under Cart in [`docs/locator-conventions.md`](docs/locator-conventions.md): the canonical storefront testid remains `cart-checkout-button` for `Baseline` only; other `ActiveCheckoutCta` values are intentional locator-break drills and must not be treated as production conventions. No integration test changes (Cart page tests do not assert this locator today).

## How to use the drill

1. Set `ActiveCheckoutCta = Baseline`, confirm existing automation finds the button.
2. Advance one level at a time (`AttrAndCopy` → `AnchorSemantics` → `VietnameseSvg` → `BossDecoy`).
3. Re-run the tool; record which locator strategy failed and what DOM signal it healed to.
4. Prefer healed locators based on intent (Order Summary primary CTA → `/checkout`), not a newly invented brittle testid.

## Out of scope

- Randomized per-render testids / CSS `::after` label tricks (Level 4 from the earlier discussion)
- Query-string or appsettings switching
- Changes to checkout page or Playwright/integration suites
