/**
 * Cart Page Interactions
 * Handles quantity controls, accessibility announcements, and cart updates
 */

// Enhanced cart accessibility and functionality
document.addEventListener('DOMContentLoaded', function() {
    const cartStatus = document.getElementById('cart-status');
    const cartTotal = document.getElementById('cart-total');

    // Quantity control functions with accessibility
    window.incrementQuantity = function(productId) {
        const input = document.getElementById(`quantity-${productId}`);
        if (input) {
            const currentValue = parseInt(input.value) || 1;
            const maxValue = parseInt(input.getAttribute('max')) || 99;

            if (currentValue < maxValue) {
                input.value = currentValue + 1;
                input.form.submit();

                // Announce the change to screen readers
                if (cartStatus) {
                    cartStatus.textContent = `Increased quantity to ${input.value}. Updating cart...`;
                }
            } else {
                if (cartStatus) {
                    cartStatus.textContent = `Maximum quantity of ${maxValue} reached for this item.`;
                }
            }
        }
    };

    window.decrementQuantity = function(productId) {
        const input = document.getElementById(`quantity-${productId}`);
        if (input) {
            const currentValue = parseInt(input.value) || 1;

            if (currentValue > 1) {
                input.value = currentValue - 1;
                input.form.submit();

                // Announce the change to screen readers
                if (cartStatus) {
                    cartStatus.textContent = `Decreased quantity to ${input.value}. Updating cart...`;
                }
            } else {
                if (cartStatus) {
                    cartStatus.textContent = 'Cannot decrease quantity below 1. Use remove button to delete item.';
                }
            }
        }
    };

    // Enhance quantity input accessibility
    document.querySelectorAll('input[type="number"]').forEach(function(input) {
        input.addEventListener('change', function() {
            const value = parseInt(this.value) || 1;
            const min = parseInt(this.getAttribute('min')) || 1;
            const max = parseInt(this.getAttribute('max')) || 99;

            if (value < min) {
                this.value = min;
                if (cartStatus) {
                    cartStatus.textContent = `Minimum quantity is ${min}.`;
                }
            } else if (value > max) {
                this.value = max;
                if (cartStatus) {
                    cartStatus.textContent = `Maximum quantity is ${max}.`;
                }
            }
        });

        // Add keyboard navigation enhancement
        input.addEventListener('keydown', function(e) {
            if (e.key === 'Enter') {
                e.preventDefault();
                this.form.submit();

                if (cartStatus) {
                    cartStatus.textContent = 'Updating cart quantity...';
                }
            }
        });
    });

    // Enhance remove buttons with confirmation
    document.querySelectorAll('button[onclick*="confirm"]').forEach(function(button) {
        button.addEventListener('click', function(e) {
            const productName = this.getAttribute('aria-label')?.match(/Remove (.+?) from/)?.[1] || 'this item';

            if (cartStatus) {
                cartStatus.textContent = `Confirming removal of ${productName}...`;
            }
        });
    });

    // Monitor cart updates
    let originalCartCount = document.querySelector('[data-cart-count]')?.getAttribute('data-cart-count');

    // Function to set cart status message (can be called from views)
    window.setCartStatusMessage = function(message) {
        if (cartStatus) {
            cartStatus.textContent = message;
        }
    };
});