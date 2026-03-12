import { test, expect, Page } from '@playwright/test';

async function loginAsCustomer(page: Page) {
  await page.goto('/Identity/Account/Login');
  await page.fill('#Input_Email',    'admin@stickit.dev');
  await page.fill('#Input_Password', 'Admin@2025!');
  await page.click('button[type="submit"]');
  await page.waitForURL(/^(?!.*Login)/);
}

test.describe('Checkout page', () => {
  test.beforeEach(async ({ page }) => {
    await loginAsCustomer(page);

    // Ensure there is at least one cart item before visiting checkout
    await page.goto('/Product');
    await page.locator('#productGrid form[action*="AddToCart"] button').first().click();
  });

  test('checkout page loads with order summary', async ({ page }) => {
    await page.goto('/Checkout');
    // The page should show subtotal / total figures
    await expect(page.locator('body')).toContainText(/subtotal|total/i);
  });

  test('checkout page shows tax and shipping lines', async ({ page }) => {
    await page.goto('/Checkout');
    await expect(page.locator('body')).toContainText(/tax/i);
    await expect(page.locator('body')).toContainText(/shipping/i);
  });

  test('checkout page has a payment submit button', async ({ page }) => {
    await page.goto('/Checkout');
    // There should be a form with a submit button for payment
    const submitBtn = page.locator(
      'button[type="submit"], input[type="submit"], #paypal-button-container, button:has-text("Pay")'
    ).first();
    await expect(submitBtn).toBeVisible();
  });
});
