# 🌸 Kawaii UI Components Documentation

## Overview

This project features a comprehensive kawaii-themed UI component system built with soft pastels, rounded pill designs, cloud/card surfaces, chunky playful headings, gentle shadows, and cute accent badges.

## 🎨 Design System

### Color Palette
- **Mint** (`--mint`): Page backgrounds, large soft sections, supportive surface areas
- **Sky** (`--sky`): Search outlines, tab active states, secondary buttons, filter chips
- **Lavender** (`--lavender`): Secondary accent areas, category bars, alternate tabs
- **Pink** (`--pink`): Primary CTA buttons, "Add to Cart", important badges, featured actions
- **Butter** (`--butter`): "New", sale/promo/limited labels, highlights, supportive emphasis

### Design Principles
- **Soft Pastel Colors**: Gentle, non-aggressive color combinations
- **Pill-Shaped UI**: Rounded corners and pill-shaped elements throughout
- **Cloud Surfaces**: Soft, floating card designs with gentle shadows
- **Chunky Playful Headings**: Bold, fun typography using Sniglet font
- **Gentle Shadows**: Subtle drop shadows that enhance depth without being harsh
- **Cute Accent Elements**: Sparkles, badges, and micro-animations for delight

## 📦 Available Components

### 1. Navigation (`_Navbar.cshtml`)

**Usage:**
```razor
<partial name="_Navbar" />
```

**Features:**
- Kawaii brand with sparkle effects
- Cloud-styled header with gradient backgrounds
- Pill-shaped navigation links with hover animations
- Responsive hamburger menu with off-canvas sidebar
- Role-based navigation (Admin, Staff, Customer views)
- Promotional links with special butter theme styling

### 2. Search Bar (`_SearchBar.cshtml`)

**Usage:**
```razor
<partial name="_SearchBar" />
```

**Features:**
- Pill-shaped search input with sky theme
- Focus states with pink accent transitions
- Search suggestions dropdown with kawaii styling
- Responsive design for mobile devices
- Accessible form controls

### 3. Hero Banner (`_HeroBanner.cshtml`)

**Usage:**
```razor
@{
    var heroModel = new ELKH.ViewModels.HeroBannerVM
    {
        Title = "Welcome to ELKH",
        Subtitle = "Your kawaii sticker paradise",
        Description = "Discover amazing stickers with soft pastels and playful designs",
        CtaText = "Shop Now",
        CtaController = "Product",
        CtaAction = "Index",
        ShowSparkles = true,
        BackgroundTheme = "mint",
        Features = new List<HeroFeatureVM>
        {
            new HeroFeatureVM 
            { 
                Icon = "bi bi-heart", 
                Title = "Kawaii Designs", 
                Description = "Cute and colorful sticker designs" 
            }
        }
    };
}

<partial name="_HeroBanner" model="heroModel" />
```

**Features:**
- Cloud-styled title with floating animations
- Gradient backgrounds with mint/sky/lavender themes
- Feature cards with hover effects
- Floating decorative elements
- Responsive design with mobile optimizations
- Accessibility support with reduced motion preferences

### 4. Product Card (`_ProductCard.cshtml`)

**Usage:**
```razor
<partial name="_ProductCard" model="@product" />
```

**Features:**
- Rounded card surfaces with hover animations
- Product badges for new, sale, best seller, trending
- Wishlist heart button overlay
- Price display with discount calculations
- Stock status indicators
- Add to cart and quick view buttons
- Responsive design

### 5. Badge System (`_Badge.cshtml`)

**Usage:**
```razor
<!-- Simple badge -->
<partial name="_Badge" model="@(new BadgeVM { Text = "New", Type = "new" })" />

<!-- Sale badge with animation -->
<partial name="_Badge" model="@(new BadgeVM { Text = "50% Off", Type = "sale" })" />

<!-- Category badge (clickable) -->
<partial name="_Badge" model="@(new BadgeVM { 
    Text = "Cute Animals", 
    Type = "category",
    IsClickable = true,
    Controller = "Category",
    Action = "ByCategory"
})" />
```

**Badge Types:**
- `new`: Butter theme with subtle pulse animation
- `sale`: Butter theme with stronger glow animation  
- `best`/`featured`: Pink theme with sparkle effects
- `limited`: Butter theme for limited items
- `promo`: Butter theme for promotional codes
- `category`: Lavender theme for categories
- `hot`: Pink theme with intense pulse animation
- `default`: Neutral styling

**Size Options:**
- `sm`: Small badges
- `default`: Standard size
- `lg`: Large badges

### 6. Category Tabs (`_CategoryTabs.cshtml`)

**Usage:**
```razor
@{
    var categoryTabsModel = new ELKH.ViewModels.CategoryTabsVM
    {
        Title = "Shop by Category",
        ShowPromotionsTab = true,
        ShowAllProductsTab = true,
        ShowViewAllTab = true,
        ShowProductCounts = true,
        ShowActiveFilters = true,
        Categories = new List<CategoryTabItem>
        {
            new CategoryTabItem 
            { 
                Id = 1, 
                Name = "Cute Animals", 
                IconClass = "bi bi-heart", 
                ProductCount = 42 
            },
            new CategoryTabItem 
            { 
                Id = 2, 
                Name = "Food", 
                IconClass = "bi bi-apple", 
                ProductCount = 28 
            }
        }
    };
}

<partial name="_CategoryTabs" model="categoryTabsModel" />
```

**Features:**
- Pill-shaped tabs with lavender theme
- Special promotions tab with butter theme
- Responsive design with mobile dropdown
- Active state management
- Product count badges
- Horizontal scrolling on desktop
- Accessible keyboard navigation

## 🎯 CSS Classes Reference

### Core Button Classes
- `.btn-kawaii`: Base button styling
- `.btn-primary-kawaii`: Pink primary buttons
- `.btn-secondary-kawaii`: Sky secondary buttons  
- `.btn-outline-kawaii`: Outlined buttons
- `.btn-butter-kawaii`: Butter accent buttons

### Surface Classes
- `.kawaii-card`: Standard card with hover effects
- `.kawaii-panel`: Panel with gradient background
- `.surface-mint`: Mint-themed surface
- `.surface-sky`: Sky-themed surface
- `.surface-lavender`: Lavender-themed surface

### Utility Classes
- `.sparkle`: Adds twinkling sparkle effect
- `.glow-on-hover`: Adds glow effect on hover
- `.glow-pink`: Pink glow variant
- `.glow-mint`: Mint glow variant
- `.text-pink`, `.text-sky`, `.text-purple`: Color utilities

### Typography Classes
- `.font-heading`: Uses Sniglet font for headings
- `.display-title`: Large display titles
- `.section-title`: Section headings
- `.card-title`: Card titles

## 🔧 Integration Tips

### Using in Controllers
```csharp
public IActionResult Index()
{
    var heroModel = new HeroBannerVM
    {
        Title = "Welcome to Our Store",
        Subtitle = "Amazing kawaii stickers await",
        CtaText = "Start Shopping"
    };
    
    ViewBag.HeroModel = heroModel;
    return View();
}
```

### Responsive Design
All components are mobile-first and include responsive breakpoints:
- Desktop: Full horizontal layouts
- Tablet: Adjusted spacing and sizing  
- Mobile: Stacked layouts, dropdown menus

### Accessibility Features
- Proper ARIA labels and roles
- Keyboard navigation support
- Screen reader optimizations
- Reduced motion preferences honored
- High contrast mode support
- Focus management

### Performance Optimizations
- CSS animations respect `prefers-reduced-motion`
- Lazy loading for images
- Efficient hover states
- Minimal JavaScript dependencies

## 🎨 Customization

### Color Theming
Modify CSS custom properties in `kawaii-theme.css`:
```css
:root {
    --sky: #A9DEF9;     /* Your sky blue */
    --butter: #F2E6A3;  /* Your butter yellow */
    --pink: #F694C1;    /* Your kawaii pink */
    /* ... etc */
}
```

### Animation Speed
Adjust timing variables:
```css
:root {
    --transition-fast: 0.15s cubic-bezier(0.4, 0.0, 0.2, 1);
    --transition-med: 0.25s cubic-bezier(0.4, 0.0, 0.2, 1);
}
```

### Border Radius
Modify roundness:
```css
:root {
    --radius-pill: 999px;  /* Make more/less rounded */
    --radius-xl: 32px;     /* Adjust card roundness */
}
```

---

*This kawaii UI system maintains consistency across all components while providing flexible customization options. The soft pastel palette and playful design elements create a delightful user experience that's both functional and emotionally engaging.* 🌸