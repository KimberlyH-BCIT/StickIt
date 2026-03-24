/*
╔══════════════════════════════════════════════════════════════════════════════════╗
║ TABLE OF CONTENTS - cart-ajax.js                                                 ║
╠══════════════════════════════════════════════════════════════════════════════════╣
║ 1. Initialization & Global State ........................... Lines    5-25     ║
║    - DOMContentLoaded event handler and cart tracking variables                 ║
║    - initAjaxCart(): Setup AJAX cart forms and page load handling               ║
║                                                                                  ║
║ 2. Cart Badge Management ................................... Lines   26-75     ║
║    - updateCartBadgeFromPage(): Sync badge from cart page data                  ║
║    - updateCartBadge(): Update header cart count display                        ║
║                                                                                  ║
║ 3. AJAX Form Submission .................................... Lines   76-140    ║
║    - handleCartSubmit(): Main add-to-cart AJAX handler                          ║
║    - FormData processing and server communication                               ║
║    - Success/error response handling                                            ║
║                                                                                  ║
║ 4. User Feedback System .................................... Lines  141-180    ║
║    - showCartSuccessMessage(): Success notification with quantity               ║
║    - showErrorMessage(): Error notification display                             ║
║                                                                                  ║
║ 5. Utility Functions ....................................... Lines  181-214    ║
║    - escapeHtml(): XSS prevention for dynamic content                           ║
║    - DOM manipulation and security helpers                                      ║
║                                                                                  ║
║ Features:                                                                        ║
║ - Cumulative quantity tracking during shopping session                          ║
║ - Real-time cart badge updates without page reload                              ║
║ - CSRF token preservation for secure AJAX requests                              ║
║ - Graceful error handling with user-friendly messages                           ║
║ - Progressive enhancement: works without JavaScript enabled                      ║
╚══════════════════════════════════════════════════════════════════════════════════╝
*/

// ═══════════════════════════════════════════════════════════════════════════
// AJAX Cart Management - Handles add to cart with cumulative count tracking
// ═══════════════════════════════════════════════════════════════════════════

// ═══════════════════════════════════════════════════════════════════════════════
// ║ Initialization & Global State Management                                   ║
// ═══════════════════════════════════════════════════════════════════════════════

document.addEventListener('DOMContentLoaded', initAjaxCart);

// Global state for tracking cart additions during the current shopping session
// Allows cumulative quantity display in success messages
let cartAddCount = 0;
let cartCountTimeout = null;

/**
 * Initializes AJAX cart functionality for all add-to-cart forms
 * Sets up event listeners and handles initial cart state synchronization
 */
function initAjaxCart() {
    const forms = document.querySelectorAll('form[action*="/Cart/AddToCart"]');

    forms.forEach(form => {
        form.addEventListener('submit', handleCartSubmit);
    });

    // Update cart badge on page load if we're on the cart page
    // This handles updates from non-AJAX operations like Remove, Update, Clear
    if (window.location.pathname.toLowerCase().includes('/cart')) {
        updateCartBadgeFromPage();
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// ║ Cart Badge Management                                                       ║
// ║ Real-time updates to header cart count display                             ║
// ═══════════════════════════════════════════════════════════════════════════════

/**
 * Updates cart badge from the cart count visible on the cart page
 * Used after non-AJAX operations like Remove, Update, Clear
 */
function updateCartBadgeFromPage() {
    // Try to find cart count from data attribute
    const container = document.querySelector('[data-cart-count]');
    if (container) {
        const count = parseInt(container.dataset.cartCount || '0', 10);
        updateCartBadge(count);
    }
}

/**
 * Handles form submission for add to cart with AJAX
 * @param {Event} e - The submit event
 */
async function handleCartSubmit(e) {
    e.preventDefault();

    const form = e.target;
    const formData = new FormData(form);
    const quantity = parseInt(formData.get('quantity') || '1', 10);

    try {
        const response = await fetch(form.action, {
            method: 'POST',
            body: formData,
            headers: {
                'X-Requested-With': 'XMLHttpRequest'
            }
        });

        if (response.ok) {
            const data = await response.json();

            // Check if the response indicates an error (out of stock, etc.)
            if (data.success === false) {
                showErrorMessage(data.message || 'Unable to add item to cart.');
                return;
            }

            // Update cart badge if present
            updateCartBadge(data.cartCount);

            // Show/update success message
            showCartSuccessMessage(quantity);

        } else {
            // Try to parse error message from response
            try {
                const errorData = await response.json();
                showErrorMessage(errorData.message || 'Failed to add item to cart. Please try again.');
            } catch {
                showErrorMessage('Failed to add item to cart. Please try again.');
            }
        }
    } catch (error) {
        console.error('Cart error:', error);
        showErrorMessage('An error occurred. Please try again.');
    }
}

/**
 * Updates the cart badge count in the header
 * @param {number} count - The new cart count
 */
function updateCartBadge(count) {
    const badge = document.getElementById('cart-badge');
    const badgeSr = document.getElementById('cart-badge-sr');

    if (badge && count !== undefined) {
        const displayCount = count > 99 ? '99+' : count.toString();
        badge.textContent = displayCount;

        if (count > 0) {
            badge.style.display = 'inline-block';
        } else {
            badge.style.display = 'none';
        }

        // Update screen reader text
        if (badgeSr) {
            const itemText = count === 1 ? 'item' : 'items';
            badgeSr.textContent = `${count} ${itemText} in cart`;
        }

        // Update aria-label on the cart link
        const cartLink = badge.closest('.cart-icon-link');
        if (cartLink) {
            const itemText = count === 1 ? 'item' : 'items';
            cartLink.setAttribute('aria-label', `Shopping Cart, ${count} ${itemText}`);
        }
    }
}

/**
 * Shows or updates the cart success message with cumulative count
 * @param {number} quantity - Number of items just added
 */
function showCartSuccessMessage(quantity) {
    // Clear any existing timeout
    if (cartCountTimeout) {
        clearTimeout(cartCountTimeout);
    }
    
    // Increment cumulative count
    cartAddCount += quantity;
    
    // Find or create alert
    let alert = document.getElementById('globalAlert');
    let alertMessage = document.getElementById('alertMessage');
    
    if (alert && alert.classList.contains('show') && alert.classList.contains('alert-success')) {
        // Update existing message
        const itemText = cartAddCount === 1 ? 'item' : 'items';
        alertMessage.innerHTML = `✓ Added ${cartAddCount} ${itemText} to your cart!`;
    } else {
        // Create new message
        const container = document.querySelector('.container');
        if (container) {
            // Remove old alert if exists
            const oldAlert = document.getElementById('globalAlert');
            if (oldAlert) {
                oldAlert.remove();
            }
            
            const itemText = cartAddCount === 1 ? 'item' : 'items';
            
            const alertHtml = `
                <div class="row">
                    <div class="col-12">
                        <div id="globalAlert" class="alert alert-success alert-dismissible fade show d-flex align-items-center justify-content-between" role="alert">
                            <div>
                                <i class="bi bi-check-circle-fill me-2"></i>
                                <strong id="alertMessage">✓ Added ${cartAddCount} ${itemText} to your cart!</strong>
                            </div>
                            <div class="d-flex gap-2 align-items-center">
                                <a href="/Cart/Index" class="btn btn-sm btn-success">
                                    <i class="bi bi-cart-check me-1"></i>View Cart
                                </a>
                                <button type="button" class="btn-close" data-bs-dismiss="alert" aria-label="Close"></button>
                            </div>
                        </div>
                    </div>
                </div>
            `;
            
            container.insertAdjacentHTML('afterbegin', alertHtml);
            
            // Add event listener to reset count when dismissed
            const newAlert = document.getElementById('globalAlert');
            const closeBtn = newAlert.querySelector('.btn-close');
            closeBtn.addEventListener('click', () => {
                cartAddCount = 0;
                if (cartCountTimeout) {
                    clearTimeout(cartCountTimeout);
                }
            });
        }
    }
    
    // Reset count after 8 seconds of inactivity
    cartCountTimeout = setTimeout(() => {
        cartAddCount = 0;
    }, 8000);
}

/**
 * Shows an error message
 * @param {string} message - Error message to display
 */
function showErrorMessage(message) {
    const container = document.querySelector('.container');
    if (!container) return;
    
    const alertHtml = `
        <div class="row">
            <div class="col-12">
                <div class="alert alert-danger alert-dismissible fade show" role="alert">
                    <i class="bi bi-exclamation-triangle-fill me-2"></i>
                    ${escapeHtml(message)}
                    <button type="button" class="btn-close" data-bs-dismiss="alert" aria-label="Close"></button>
                </div>
            </div>
        </div>
    `;
    
    container.insertAdjacentHTML('afterbegin', alertHtml);
    
    // Auto-dismiss after 5 seconds
    setTimeout(() => {
        const alert = container.querySelector('.alert-danger');
        if (alert) {
            alert.classList.remove('show');
            setTimeout(() => alert.remove(), 150);
        }
    }, 5000);
}

/**
 * Escapes HTML to prevent XSS
 * @param {string} str - String to escape
 * @returns {string} Escaped string
 */
function escapeHtml(str) {
    return String(str).replace(/[&"'<>]/g, s => ({
        '&': '&amp;', '"': '&quot;', "'": '&#39;', '<': '&lt;', '>': '&gt;'
    })[s]);
}
