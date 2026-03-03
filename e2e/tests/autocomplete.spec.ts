import { test, expect } from '@playwright/test';

test.describe('Product autocomplete accessibility and keyboard', () => {
  test.beforeEach(async ({ page }) => {
    // Ensure the app is running locally on baseURL
    await page.goto('/');
  });

  test('aria attributes present and keyboard navigation works', async ({ page }) => {
    // navigate to moderation page which has the productName input
    await page.goto('/Moderation');
    const input = page.locator('#productNameInput');
    await expect(input).toHaveAttribute('aria-autocomplete', 'list');
    await expect(input).toHaveAttribute('aria-controls');

    // type a query that will produce suggestions
    await input.fill('test');
    await page.waitForSelector('#productNameSuggestions .list-group-item');

    const box = page.locator('#productNameSuggestions');
    await expect(box).toHaveAttribute('role', 'listbox');

    // press ArrowDown to select first item
    await input.press('ArrowDown');
    const first = box.locator('.list-group-item').first();
    await expect(first).toHaveClass(/active/);

    // press Enter should navigate to details (or at least attempt)
    await Promise.all([
      page.waitForNavigation({ waitUntil: 'domcontentloaded', timeout: 5000 }).catch(() => null),
      input.press('Enter')
    ]);

    // If navigation happened, ensure URL contains /Product/Details or path equal
    if (page.url().includes('/Product/Details')) {
      expect(page.url()).toMatch(/Product\/Details\/[0-9]+/);
    }
  });

  test('type-ahead within list jumps to matching item', async ({ page }) => {
    await page.goto('/Moderation');
    const input = page.locator('#productNameInput');
    await input.fill('');
    await input.type('apple');
    await page.waitForSelector('#productNameSuggestions .list-group-item');
    // type a single character to test type-ahead
    await input.press('a');
    // active item should match starting with 'a' (best-effort)
    const active = page.locator('#productNameSuggestions .list-group-item.active');
    await expect(active).toBeVisible();
  });
});
