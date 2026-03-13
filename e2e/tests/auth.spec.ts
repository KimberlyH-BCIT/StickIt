import { test, expect, Page } from '@playwright/test';

/**
 * Helper: log in with the given credentials via the Identity login page.
 * Waits for the redirect back to / before resolving.
 */
async function login(page: Page, email: string, password: string) {
  await page.goto('/Identity/Account/Login');
  await page.fill('#Input_Email',    email);
  await page.fill('#Input_Password', password);
  await page.click('button[type="submit"]');
  await page.waitForURL(/^(?!.*Login)/);
}

/** Helper: log out via the Identity logout endpoint. */
async function logout(page: Page) {
  // The logout form is in the navbar — submit it.
  await page.click('form[action*="Logout"] button, a[href*="Logout"]');
  await page.waitForURL(/Login|\/$/);
}

const ADMIN_EMAIL = 'admin@stickit.dev';
const ADMIN_PASS  = 'Admin@2025!';

test.describe('Authentication', () => {
  test('login with valid credentials redirects away from login page', async ({ page }) => {
    await page.goto('/Identity/Account/Login');
    await page.fill('#Input_Email',    ADMIN_EMAIL);
    await page.fill('#Input_Password', ADMIN_PASS);
    await page.click('button[type="submit"]');

    await expect(page).not.toHaveURL(/Login/);
  });

  test('login with wrong password shows validation error', async ({ page }) => {
    await page.goto('/Identity/Account/Login');
    await page.fill('#Input_Email',    ADMIN_EMAIL);
    await page.fill('#Input_Password', 'wrong-password-123');
    await page.click('button[type="submit"]');

    await expect(page).toHaveURL(/Login/);
    // Identity shows "Invalid login attempt." on failure
    const errorText = page.locator('.validation-summary-errors, .text-danger');
    await expect(errorText.first()).toBeVisible();
  });

  test('authenticated user sees logout option in navbar', async ({ page }) => {
    await login(page, ADMIN_EMAIL, ADMIN_PASS);
    // The navbar should contain a logout button after login
    await expect(page.locator('form[action*="Logout"], a[href*="Logout"]').first()).toBeVisible();
  });

  test('accessing protected page while unauthenticated redirects to login', async ({ page }) => {
    await page.goto('/Cart');
    await expect(page).toHaveURL(/Login/);
  });
});
