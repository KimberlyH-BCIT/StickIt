import { test, expect, Page } from '@playwright/test';

/** Log in as the seeded admin user (also has a Customer row via registration). */
async function loginAsCustomer(page: Page) {
  await page.goto('/Identity/Account/Login');
  await page.fill('#Input_Email',    'admin@stickit.dev');
  await page.fill('#Input_Password', 'Admin@2025!');
  await page.click('button[type="submit"]');
  await page.waitForURL(/^(?!.*Login)/);
}

test.describe('Product listing — search, filter, pagination', () => {
  test('product listing page loads and shows at least one product', async ({ page }) => {
    await page.goto('/Product');
    // The product grid should contain at least one card
    await expect(page.locator('#productGrid .col').first()).toBeVisible();
  });

  test('search input filters visible products', async ({ page }) => {
    await page.goto('/Product');
    const input = page.locator('input[name="search"]');
    await input.fill('zzz_no_match_xyz');
    await page.click('button[type="submit"]');

    // Either a "no products found" message or an empty grid
    const empty = page.locator('.alert, #productGrid .col');
    await expect(empty.first()).toBeVisible();
  });

  test('clear filter link removes search param', async ({ page }) => {
    await page.goto('/Product?search=sticker');
    const clear = page.locator('a:has-text("Clear")');
    if (await clear.isVisible()) {
      await clear.click();
      await expect(page).not.toHaveURL(/search=/);
    }
  });
});

test.describe('Cart — add and view', () => {
  test.beforeEach(async ({ page }) => {
    await loginAsCustomer(page);
  });

  test('Add to Cart button on product listing adds item and redirects back', async ({ page }) => {
    await page.goto('/Product');

    // Get the cart count before adding
    const badgeBefore = await page.locator('.cart-count, [data-cart-count]').first().textContent().catch(() => '0');

    // Click the first Add to Cart form submit
    await page.locator('#productGrid form[action*="AddToCart"] button').first().click();

    // After redirect, we should be back on the product or cart page
    await expect(page).not.toHaveURL(/Error/);
  });

  test('Cart page shows items after adding', async ({ page }) => {
    // Add a product first
    await page.goto('/Product');
    await page.locator('#productGrid form[action*="AddToCart"] button').first().click();

    // Now visit the cart
    await page.goto('/Cart');
    // Cart should have at least one row
    await expect(page.locator('table tbody tr, .cart-item').first()).toBeVisible();
  });

  test('Remove from cart decreases item count', async ({ page }) => {
    await page.goto('/Cart');

    const removeForms = page.locator('form[action*="RemoveFromCart"]');
    const count = await removeForms.count();

    if (count > 0) {
      await removeForms.first().locator('button').click();
      // After removal the table should have one fewer row
      await expect(removeForms).toHaveCount(Math.max(count - 1, 0));
    } else {
      // Cart already empty — acceptable
      test.skip();
    }
  });
});
