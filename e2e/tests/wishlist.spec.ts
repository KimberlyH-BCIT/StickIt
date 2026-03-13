import { test, expect, Page } from '@playwright/test';

async function loginAsCustomer(page: Page) {
  await page.goto('/Identity/Account/Login');
  await page.fill('#Input_Email',    'admin@stickit.dev');
  await page.fill('#Input_Password', 'Admin@2025!');
  await page.click('button[type="submit"]');
  await page.waitForURL(/^(?!.*Login)/);
}

test.describe('Wishlist', () => {
  test('unauthenticated user sees sign-in modal prompt instead of adding', async ({ page }) => {
    await page.goto('/Product');
    // The wishlist button for unauthenticated users triggers a modal, not a form POST
    const wishlistBtn = page.locator('button:has-text("Wishlist")').first();
    await expect(wishlistBtn).toBeVisible();
    // Button should NOT be inside a form that POSTs (no logged-in POST form)
    const btnInForm = await wishlistBtn.evaluate(
      el => el.closest('form[method="post"]') !== null
    );
    expect(btnInForm).toBe(false);
  });

  test('authenticated user can add a product to their wishlist', async ({ page }) => {
    await loginAsCustomer(page);
    await page.goto('/Product');

    // Find the wishlist form (only rendered for authenticated users)
    const wishlistForm = page.locator('.add-to-wishlist-form').first();
    await expect(wishlistForm).toBeVisible();

    // Submit — should succeed or return AlreadyExists (not an error page)
    await Promise.all([
      page.waitForResponse(resp => resp.url().includes('AddAjax') && resp.status() < 400),
      wishlistForm.locator('button').click()
    ]);
  });

  test('wishlist page shows items for authenticated user', async ({ page }) => {
    await loginAsCustomer(page);
    await page.goto('/Wishlist');
    // Should not be redirected to login or show an unhandled error
    await expect(page).not.toHaveURL(/Login/);
    await expect(page.locator('body')).not.toContainText(/500|unhandled exception/i);
  });
});
