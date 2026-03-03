using ELKH.Models;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text;

namespace ELKH.Data;

/// <summary>
/// Seeds the database with demo categories and products on first run.
/// Entirely idempotent — skipped if any products already exist.
/// </summary>
public static class DbSeeder
{
    public static async Task SeedProductsAsync(ApplicationDbContext db)
    {
        // Skip if products already exist
        if (await db.Products.AnyAsync()) return;

        // ── 1. Ensure the seven categories exist ───────────────────────
        var categoryNames = new[]
        {
            "Die-Cut Stickers",
            "Holographic",
            "Waterproof",
            "Sheet Packs",
            "Anime & Pop Culture",
            "Nature & Floral",
            "Gaming"
        };

        foreach (var name in categoryNames)
        {
            if (!await db.Categories.AnyAsync(c => c.CategoryName == name))
                db.Categories.Add(new CategoryModel { CategoryName = name });
        }
        await db.SaveChangesAsync();

        // Load all seven categories with their assigned PKs
        var cats = await db.Categories
            .Where(c => categoryNames.Contains(c.CategoryName))
            .ToDictionaryAsync(c => c.CategoryName);

        var die    = cats["Die-Cut Stickers"];
        var holo   = cats["Holographic"];
        var water  = cats["Waterproof"];
        var sheet  = cats["Sheet Packs"];
        var anime  = cats["Anime & Pop Culture"];
        var nature = cats["Nature & Floral"];
        var gaming = cats["Gaming"];

        // ── 2. Build the 40 products ────────────────────────────────────
        var products = new List<ProductModel>
        {
            // ── Die-Cut Stickers (8) ────────────────────────────────────
            P("Kawaii Cat Die-Cut Sticker",
              "Adorable kawaii-style cat with big sparkle eyes. Printed on premium vinyl with a glossy finish.",
              2.99m, 0, 150, die),

            P("Shiba Inu Doge Die-Cut Sticker",
              "Classic internet-icon Shiba Inu in die-cut form. Very sticker. Much wow.",
              2.49m, 0, 200, die),

            P("Cactus Friends Die-Cut Sticker",
              "A cheerful cactus trio perfect for laptops, water bottles, and planners.",
              1.99m, 10, 175, die),

            P("Vintage Camera Die-Cut Sticker",
              "Retro 35 mm film camera rendered in warm pastel tones. Great for photographers.",
              3.49m, 0, 120, die),

            P("Astronaut Floating Die-Cut Sticker",
              "A tiny astronaut drifting through a pastel galaxy. Approx. 7 cm tall.",
              2.99m, 0, 160, die),

            P("Boba Tea Die-Cut Sticker",
              "Brown sugar milk tea with tapioca pearls. UV-resistant ink keeps colours vivid.",
              2.49m, 15, 180, die),

            P("Avocado Toast Die-Cut Sticker",
              "Trendy avocado toast slice with a smiling face. Dishwasher-safe laminate.",
              1.99m, 0, 140, die),

            P("Retro Cassette Die-Cut Sticker",
              "80s-style audio cassette tape in neon colours. Perfect for notebooks and guitar cases.",
              2.99m, 0, 130, die),

            // ── Holographic (6) ─────────────────────────────────────────
            P("Rainbow Galaxy Holographic Sticker",
              "Shifts through the full visible spectrum in direct light. Deep-space galaxy artwork.",
              3.99m, 0, 100, holo),

            P("Unicorn Horn Holographic Sticker",
              "Prismatic spiral unicorn horn that sparkles with every movement.",
              3.49m, 20, 90, holo),

            P("Crystal Prism Holographic Sticker",
              "Geometric crystal prism design with metallic rainbow shimmer. 8 cm wide.",
              4.99m, 0, 75, holo),

            P("Shooting Star Holographic Sticker",
              "A streaking shooting star with a long rainbow tail. Make a wish!",
              3.99m, 0, 95, holo),

            P("Northern Lights Holographic Sticker",
              "Aurora borealis waves captured in a colour-shifting holographic print.",
              4.49m, 10, 80, holo),

            P("Butterfly Wings Holographic Sticker",
              "Iridescent butterfly wings that appear to move when tilted. 9 cm wingspan.",
              3.99m, 0, 110, holo),

            // ── Waterproof (6) ──────────────────────────────────────────
            P("Ocean Waves Waterproof Sticker",
              "Japanese woodblock-inspired wave design. Fully waterproof and scratch-resistant.",
              3.49m, 0, 120, water),

            P("Mountain Peak Waterproof Sticker",
              "Minimalist mountain range silhouette in cool blues. Survives dishwashers and rain.",
              3.99m, 0, 110, water),

            P("City Skyline Waterproof Sticker",
              "Generic modern cityscape at dusk. Weatherproof vinyl with UV coating.",
              4.49m, 0, 85, water),

            P("Compass Rose Waterproof Sticker",
              "Vintage nautical compass rose in antique gold. Great for adventure gear.",
              3.99m, 15, 95, water),

            P("Deep Sea Fish Waterproof Sticker",
              "Bioluminescent anglerfish glowing in the deep ocean dark. Waterproof UV ink.",
              3.49m, 0, 130, water),

            P("Sunset Palm Waterproof Sticker",
              "Tropical palm silhouette against a gradient sunset. Fade-resistant outdoor vinyl.",
              3.99m, 0, 100, water),

            // ── Sheet Packs (5) ─────────────────────────────────────────
            P("Cottagecore Sheet Pack (20 Stickers)",
              "Mushrooms, flowers, hedgehogs, and vintage teapots — 20 illustrated stickers on one sheet.",
              8.99m, 0, 60, sheet),

            P("Space Explorer Sheet Pack (16 Stickers)",
              "Rockets, planets, astronauts, and satellites. 16 stickers for the stargazer in you.",
              7.99m, 10, 55, sheet),

            P("Retro Vibes Sheet Pack (24 Stickers)",
              "Cassettes, boom boxes, pixel art, and neon signs. 24 stickers for maximum nostalgia.",
              10.99m, 0, 45, sheet),

            P("Cute Animals Sheet Pack (20 Stickers)",
              "20 chibi-style animals including pandas, foxes, frogs, and capybaras.",
              8.99m, 25, 70, sheet),

            P("Fantasy Creatures Sheet Pack (18 Stickers)",
              "Dragons, phoenixes, mermaids, and griffins across 18 hand-drawn stickers.",
              9.49m, 0, 50, sheet),

            // ── Anime & Pop Culture (6) ─────────────────────────────────
            P("Totoro Forest Spirit Sticker",
              "Fan art-inspired forest spirit from the beloved Studio Ghibli universe. 6 cm tall.",
              2.99m, 0, 160, anime),

            P("Pikachu Chibi Sticker",
              "Classic yellow electric mouse in super-deformed chibi style. 5 cm tall.",
              2.49m, 0, 200, anime),

            P("Scout Regiment Sticker",
              "Wings of Freedom emblem from the hit manga series. Matte laminate finish.",
              3.49m, 10, 140, anime),

            P("Jolly Roger Sticker",
              "Skull-and-crossbones pirate flag from the beloved grand-line adventure series.",
              2.99m, 0, 150, anime),

            P("Energy Blast Sticker",
              "Iconic blue power ball inspired by the legendary anime power level sequence.",
              3.49m, 0, 130, anime),

            P("Leaf Village Symbol Sticker",
              "Hidden leaf village crest from the world-famous ninja academy series.",
              2.99m, 20, 145, anime),

            // ── Nature & Floral (5) ─────────────────────────────────────
            P("Cherry Blossom Branch Sticker",
              "Delicate sakura branch in soft pinks. Botanical illustration style, 10 cm wide.",
              2.49m, 0, 170, nature),

            P("Wildflower Meadow Sticker",
              "Loose watercolour wildflowers — daisies, lavender, poppies — on a white field.",
              3.49m, 0, 120, nature),

            P("Autumn Leaves Sticker",
              "Four falling autumn leaves in crimson, amber, rust, and gold.",
              2.99m, 0, 155, nature),

            P("Succulent Garden Sticker",
              "A pot of assorted succulents rendered in a clean, modern illustration style.",
              3.49m, 15, 110, nature),

            P("Monstera Leaf Sticker",
              "Tropical monstera deliciosa leaf in deep green with natural splits. 9 cm tall.",
              2.99m, 0, 160, nature),

            // ── Gaming (4) ──────────────────────────────────────────────
            P("Retro Game Controller Sticker",
              "Classic D-pad and button controller rendered in pixel art. For all the old-school gamers.",
              3.49m, 0, 140, gaming),

            P("Pixel Sword & Shield Sticker",
              "8-bit fantasy weapon set — longsword and kite shield in silver and blue pixels.",
              2.99m, 10, 120, gaming),

            P("Game Over Screen Sticker",
              "Retro arcade GAME OVER text on a black background with glowing red letters.",
              2.49m, 0, 130, gaming),

            P("Boss Fight Dragon Sticker",
              "Epic pixel-art red dragon in full battle stance. The ultimate boss encounter.",
              3.99m, 0, 95, gaming),
        };

        db.Products.AddRange(products);
        await db.SaveChangesAsync();
    }

    // ── Helper ──────────────────────────────────────────────────────────
    private static ProductModel P(
        string name,
        string description,
        decimal price,
        decimal discountPercent,
        int stock,
        CategoryModel category) => new()
    {
        Name             = name,
        NameNormalized   = Normalize(name),
        Description      = description,
        Price            = price,
        DiscountPercent  = discountPercent,
        StockQuantity    = stock,
        IsActive         = true,
        FkCategoryId     = category.PkCategoryId,
        Category         = category
    };

    private static string Normalize(string name)
    {
        if (string.IsNullOrEmpty(name)) return string.Empty;
        var s = name.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var ch in s)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
                sb.Append(ch);
        }
        return sb.ToString().Normalize(NormalizationForm.FormC).ToLowerInvariant();
    }
}
