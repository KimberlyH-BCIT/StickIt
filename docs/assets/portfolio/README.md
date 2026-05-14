# Portfolio capture checklist

This folder is for the portfolio-facing visual evidence that the root `README.md` refers to.

The goal is to show the app as a working product, not just describe it.

## Required captures

### Core desktop screenshots
- `homepage-desktop.png`
- `catalog-desktop.png`
- `cart-desktop.png`
- `checkout-desktop.png`
- `admin-dashboard-desktop.png`
- `staff-orders-desktop.png`

### Responsive screenshots
- `homepage-mobile.png`
- `catalog-tablet.png`
- `checkout-mobile.png`

### Motion demo
- `storefront-flow.gif`

Recommended flow for the GIF:
1. land on homepage
2. open product catalog
3. open a product detail page
4. add item to cart
5. open cart
6. begin checkout

Keep it short: 10 to 20 seconds.

## Accessibility evidence to capture

Add screenshots or exports for:
- keyboard-visible focus states on nav, search, and checkout controls
- search autocomplete with ARIA-driven listbox behavior visible
- form validation state with accessible error messaging
- one axe or Lighthouse accessibility result capture
- one responsive/mobile view showing readable spacing and touch targets

Suggested filenames:
- `focus-state-nav.png`
- `focus-state-checkout.png`
- `autocomplete-listbox.png`
- `form-validation-accessibility.png`
- `axe-results.png`
- `lighthouse-accessibility.png`

## Capture guidelines

- Use seeded demo data, not placeholder lorem ipsum
- Prefer realistic catalog items such as `Maple Leaf Pride Sticker`, `Kawaii Panda Sticker`, `Santa Claus Face Sticker`, or `Pizza Slice Sticker`
- Capture clean browser chrome or crop consistently
- Use the same theme mode and zoom level across related shots
- Avoid exposing secrets, local file paths, or private browser extensions
- If showing admin/staff screens, log in with the seeded demo accounts documented in the main README

## Suggested sequence for README updates after captures exist

1. Replace the planned-asset table in `README.md` with real image embeds
2. Add the GIF near the top of the UI walkthrough section
3. Link to at least one accessibility audit capture from the accessibility section
4. Add one sentence under each screenshot explaining what the reviewer should notice
