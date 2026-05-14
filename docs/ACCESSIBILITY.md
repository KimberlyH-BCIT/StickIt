# StickIt accessibility implementation notes

## Overview
This document is meant to show the accessibility work that is present in the codebase today.

It is not a formal certification report. Instead, it collects:
- implemented accessibility patterns
- code references worth reviewing
- manual checks that should be run for portfolio review
- current evidence gaps and follow-up work

## Accessibility target

The project is aiming for **WCAG 2.1 Level AA** patterns in the customer-facing experience, but this repository does not currently include a full third-party audit package or committed axe/Lighthouse artifacts proving complete conformance.

What is evidenced in the repo today:
- semantic layout and skip links
- keyboard-aware navigation patterns
- ARIA usage in search, forms, and live feedback
- focus styling and reduced-motion support

What is still missing as portfolio evidence:
- committed screenshots of axe or Lighthouse results
- screen-reader test notes tied to specific builds
- a concise pass/fail checklist captured after a real UI review session

---

## Table of Contents
1. [Navigation & Keyboard Access](#navigation--keyboard-access)
2. [Forms & Input Controls](#forms--input-controls)
3. [Interactive Components](#interactive-components)
4. [Semantic HTML Structure](#semantic-html-structure)
5. [Images & Media](#images--media)
6. [Color & Visual Design](#color--visual-design)
7. [Dynamic Content & Live Regions](#dynamic-content--live-regions)
8. [Testing & Tools](#testing--tools)
9. [Known Issues & Roadmap](#known-issues--roadmap)

---

## Navigation & keyboard access

### Skip Links
Skip links are provided on all major pages for keyboard navigation:
- Skip to main content (`#main-content`)
- Skip to navigation (`#site-navigation`)
- Skip to search (`#search-form`)
- Skip to footer (`#site-footer`)

**Implementation:** `Views/Shared/_Layout.cshtml` lines 270-275

### Keyboard navigation
Implemented and intended patterns:
- **Tab order** follows logical reading order
- **Focus indicators**: 2px outline + box-shadow on all interactive elements
- **Escape key**: Closes modals and dropdowns
- **Enter/Space**: Activates buttons and links
- **Arrow keys**: Navigate dropdown menus and combobox suggestions

### Navigation Components
#### Main Navigation (`_Navbar.cshtml`)
- ARIA roles: `role="navigation"`, `role="menu"`, `role="menuitem"`
- Dropdown menus use proper ARIA menu pattern
- Hamburger menu has `aria-controls`, `aria-expanded`
- All links have descriptive `aria-label` attributes

#### Offcanvas Menu
- Proper `role="dialog"` semantics
- Focus trap when open
- Close button has descriptive `aria-label="Close navigation menu"`

---

## Forms & input controls

### Form accessibility features
Examples implemented in the app:
- ✅ Explicit `<label>` elements with `for` attribute matching input `id`
- ✅ Required fields marked with visual indicator (*) and `aria-required="true"`
- ✅ Help text associated via `aria-describedby`
- ✅ Error messages with `role="alert"` and `aria-live="polite"`
- ✅ Autocomplete attributes for common fields (email, name, address)

### Enhanced forms
- **Product Create/Edit**: Full accessibility with required indicators, validation feedback, and help text
- **Contact Form**: Enhanced with aria-required, aria-describedby for all fields
- **Login/Register**: Comprehensive ARIA attributes, password toggle with screen reader text
- **Checkout**: Step-by-step form with proper fieldset/legend groups and address autocomplete
- **Profile Editor**: File upload with accept attributes and size limits described

### Search Functionality
**Search Autocomplete** (`_SearchBar.cshtml`):
- ARIA Combobox pattern (`role="combobox"`)
- `aria-expanded` state changes
- `aria-autocomplete="list"`
- Suggestions box: `role="listbox"`
- Results update `aria-live` region
- Keyboard navigation: Up/Down arrows, Enter to select, Escape to close

**JavaScript Implementation:** `wwwroot/js/site.js` lines 81-200

---

## Interactive components

### Buttons
All buttons include:
- Descriptive text or `aria-label`
- Icon-only buttons always have `aria-label`
- Decorative icons marked with `aria-hidden="true"`
- Minimum 44x44px touch target size on mobile

### Links
- Descriptive link text (no "click here")
- External links open in new tab with `aria-label` indicating behavior
- Breadcrumb navigation with `aria-label="breadcrumb"`

### Modals & Dialogs
- `role="dialog"` or Bootstrap modal component
- `aria-labelledby` pointing to modal title
- Focus management (focus trapped, returns on close)
- Close button: `aria-label="Close"`

### AJAX Operations
**Cart Operations** (`cart-ajax.js`):
- Updates `aria-label` on cart icon with current count
- Screen reader text in `#cart-badge-sr` announces changes
- Success alerts use `role="alert"` for immediate announcement
- Error messages use `aria-live="polite"`

**Wishlist Toggle**:
- AJAX add/remove with aria-label updates
- Visual and screen reader feedback

---

## Semantic HTML structure

### Landmark Regions
- **Banner**: `<header role="banner">` - Site header with logo and main navigation
- **Main**: `<main role="main">` - Primary page content
- **Navigation**: `<nav role="navigation">` - Navigation sections
- **Contentinfo**: `<footer role="contentinfo">` - Site footer
- **Search**: `<form role="search">` - Search functionality

### Heading Hierarchy
Proper heading structure maintained throughout:
- **H1**: Page title (one per page)
- **H2**: Major sections
- **H3-H6**: Subsections in logical order

**Example:**
```html
<h1>Product Details</h1>
  <h2>Pricing Information</h2>
  <h2>Customer Reviews</h2>
    <h3>Filter Reviews</h3>
  <h2>Related Products</h2>
```

### Sections & Articles
- Page sections use `<section aria-labelledby="heading-id">`
- Product cards wrapped in semantic structure
- Lists use proper `<ul>`, `<ol>`, `<dl>` markup

---

## Images & media

### Image Accessibility
All images include appropriate alternative text:

**Product Images**:
```html
<img src="product.jpg" 
     alt="Galaxy Cat Sticker - Cute Animals category, 20% off" 
     loading="lazy" />
```

**Decorative Images**:
- Icons marked with `aria-hidden="true"`
- CSS background images (purely decorative) not exposed to screen readers

**Image Placeholders**:
```html
<div role="img" 
     aria-label="No image available for Product Name">
  <i class="bi bi-image" aria-hidden="true"></i>
</div>
```

### Avatar Images
User profile pictures include descriptive alt text:
```html
<img src="avatar.jpg" 
     alt="Your current profile picture" />
```

---

## Color & visual design

### Color contrast
The design system defines darker text tokens and contrast-oriented focus styles, but this document should be read as implementation evidence rather than a blanket claim that every rendered screen has been exhaustively measured.

Targets being designed toward:
- **Normal text**: 4.5:1 minimum contrast ratio
- **Large text** (18pt+): 3:1 minimum contrast ratio
- **UI components**: 3:1 minimum contrast ratio

**Color Variables** (`kawaii-theme.css`):
- Primary text: `--ink: #4A3B5C` (dark purple)
- Secondary text: `--ink-medium: #5D4A73`
- Light text: `--ink-soft: #7A6B8F`

### High Contrast Mode
Custom styles for `@media (prefers-contrast: high)`:
```css
@media (prefers-contrast: high) {
    .btn-primary-kawaii,
    .btn-secondary-kawaii {
        border-width: 3px !important;
        font-weight: 900 !important;
    }
    
    .text-muted {
        color: #666 !important; /* Higher contrast */
    }
}
```

**Implementation:** `_Layout.cshtml` lines 174-200

### Focus Indicators
Enhanced focus styles for keyboard navigation:
```css
*:focus, *:focus-visible {
    outline: 2px solid #0066cc !important;
    outline-offset: 2px !important;
}

.btn:focus, .form-control:focus {
    border-color: #0066cc !important;
    box-shadow: 0 0 0 0.25rem rgba(0, 102, 204, 0.25) !important;
}
```

### Reduced Motion
Respects user preferences for reduced motion:
```css
@media (prefers-reduced-motion: reduce) {
    * {
        animation-duration: 0.01ms !important;
        animation-iteration-count: 1 !important;
        transition-duration: 0.01ms !important;
    }
}
```

**Implementation:** `Home/Index.cshtml` lines 54-66

---

## Dynamic content & live regions

### ARIA Live Regions

**Cart Status** (`Cart/Index.cshtml`):
```html
<div id="cart-status" 
     aria-live="polite" 
     aria-atomic="true" 
     class="sr-only">
</div>
```

**Checkout Status** (`Checkout/Index.cshtml`):
```html
<div id="checkout-status" 
     aria-live="polite" 
     aria-atomic="true" 
     class="sr-only">
</div>
```

**Form Validation**:
```html
<span id="firstname-error" 
      asp-validation-for="FirstName" 
      role="alert" 
      aria-live="polite">
</span>
```

### Success/Error Alerts
Bootstrap alerts automatically announce via `role="alert"`:
```html
<div class="alert alert-success" 
     role="alert" 
     aria-live="assertive">
    ✓ Product added to cart!
</div>
```

### Dynamic Badge Updates
Cart icon badge updates announce to screen readers:
```javascript
// Update visible badge
badge.textContent = count;

// Update screen reader text
const itemText = count === 1 ? 'item' : 'items';
badgeSr.textContent = `${count} ${itemText} in cart`;

// Update aria-label
cartLink.setAttribute('aria-label', 
    `Shopping Cart, ${count} ${itemText}`);
```

**Implementation:** `cart-ajax.js` lines 140-167

---

## Testing & tools

### Manual testing checklist
- [ ] Keyboard-only navigation (no mouse)
- [ ] Screen reader testing (NVDA, JAWS, VoiceOver)
- [ ] Browser zoom to 200%
- [ ] High contrast mode
- [ ] Reduced motion preferences
- [ ] Mobile touch target sizes

### Automated testing tools
Recommended tools for repeatable portfolio evidence:
- **axe DevTools**: Browser extension for accessibility auditing
- **WAVE**: Web accessibility evaluation tool
- **Lighthouse**: Chrome DevTools accessibility audit
- **Pa11y**: Automated accessibility testing CLI

### Current evidence status
At the time of this branch update, the repository documents the intended testing approach but does not yet include committed output files or screenshots from:
- NVDA
- JAWS
- VoiceOver
- TalkBack

Until those artifacts are captured, treat screen-reader support here as an implementation goal supported by code patterns, not as a fully evidenced certification statement.

---

## Known issues & roadmap

### Current known issues
- No committed axe or Lighthouse result captures yet
- No screenshot set yet showing keyboard focus states across the main storefront flows
- No short accessibility walkthrough GIF yet for the portfolio README

### Future enhancements
- [ ] Add ARIA landmarks to product grid sections
- [ ] Implement keyboard shortcuts for common actions
- [ ] Add skip links within long product lists
- [ ] Enhance mobile touch target sizes on complex forms
- [ ] Add voice command support
- [ ] Commit real audit artifacts and responsive accessibility screenshots

---

## Developer guidelines

### Adding New Features
When adding new features, ensure:

1. **Keyboard Access**: All functionality available via keyboard
2. **ARIA Attributes**: Use appropriate roles, states, and properties
3. **Focus Management**: Handle focus for dynamic content
4. **Color Contrast**: Test with contrast checker tools
5. **Screen Reader**: Test with at least one screen reader

### Code Examples

**Accessible Button**:
```html
<button type="button" 
        class="btn-kawaii btn-primary-kawaii" 
        aria-label="Add Galaxy Cat Sticker to cart">
    <i class="bi bi-cart-plus me-1" aria-hidden="true"></i>
    Add to Cart
</button>
```

**Accessible Form Field**:
```html
<div class="mb-3">
    <label for="email" class="form-label">
        <span class="required-indicator" 
              aria-label="Required field">*</span>
        Email Address
    </label>
    <input type="email" 
           id="email" 
           name="email" 
           class="input-kawaii" 
           required 
           aria-required="true" 
           aria-describedby="email-help email-error" 
           autocomplete="email" />
    <small id="email-help" class="form-text">
        We'll never share your email
    </small>
    <span id="email-error" 
          class="text-danger" 
          role="alert" 
          aria-live="polite">
    </span>
</div>
```

**Accessible Modal**:
```html
<div class="modal" 
     tabindex="-1" 
     role="dialog" 
     aria-labelledby="modalTitle">
    <div class="modal-dialog">
        <div class="modal-content">
            <div class="modal-header">
                <h5 id="modalTitle" class="modal-title">
                    Confirm Action
                </h5>
                <button type="button" 
                        class="btn-close" 
                        data-bs-dismiss="modal" 
                        aria-label="Close modal">
                </button>
            </div>
            <!-- Modal body -->
        </div>
    </div>
</div>
```

---

## Support & contact

For accessibility questions or to report issues:
- **Email**: support@stickit.dev
- **GitHub Issues**: Tag with [accessibility] label

The direction of the project is toward stronger WCAG-aligned behavior, but this document should be maintained as evidence-backed implementation notes, not boilerplate compliance marketing.

---

**Last Updated**: May 2026  
**Target Standard**: WCAG 2.1 Level AA patterns where implemented  
**Evidence Status**: Partial implementation evidence, additional audit artifacts still needed
