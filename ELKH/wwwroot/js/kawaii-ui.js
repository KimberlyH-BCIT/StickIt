/*
╔==================================================================================╗
║ KAWAII UI INTERACTIONS - Design System JavaScript                               ║
║ Handles visual and interactive behaviors specific to the kawaii design system  ║
╚==================================================================================╝

TABLE OF CONTENTS:
- Tab Management: Custom tab switching with accessibility support
- Chip Toggles: Toggle button states with ARIA attributes  
- Add to Cart Feedback: Visual feedback for cart interactions
- Search UI Enhancements: Clear button and input management
- Password Toggle: Show/hide password functionality
- Kawaii Visual Effects: Sparkle animations, glow effects, hover states

PURPOSE:
This file contains JavaScript specifically for enhancing the kawaii design
system components with interactive behaviors, animations, and visual feedback.
For core application logic (AJAX, data processing), see site.js.

USAGE:
All functions are automatically initialized on DOMContentLoaded.
Components using kawaii design system classes will automatically
have their interactive behaviors enhanced.
*/

document.addEventListener("DOMContentLoaded", () => {
    initTabs();
    initToggleChips();
    initAddToCartFeedback();
    initSearchClear();
    initPasswordToggle();
    if (!respectsReducedMotion()) {
        initKawaiiInteractions();
    }
});

function initTabs() {
    const tabGroups = document.querySelectorAll("[data-tab-group]");

    tabGroups.forEach(group => {
        const tabs = group.querySelectorAll("[data-tab]");
        const panels = group.querySelectorAll("[data-tab-panel]");

        tabs.forEach(tab => {
            tab.addEventListener("click", () => {
                const target = tab.getAttribute("data-tab");

                tabs.forEach(t => {
                    t.classList.remove("active");
                    t.setAttribute("aria-selected", "false");
                });

                panels.forEach(panel => {
                    panel.hidden = panel.getAttribute("data-tab-panel") !== target;
                });

                tab.classList.add("active");
                tab.setAttribute("aria-selected", "true");
            });
        });
    });
}

function initToggleChips() {
    const chips = document.querySelectorAll("[data-chip-toggle]");

    chips.forEach(chip => {
        chip.addEventListener("click", () => {
            chip.classList.toggle("active");

            const pressed = chip.classList.contains("active");
            chip.setAttribute("aria-pressed", pressed.toString());
        });
    });
}

function initAddToCartFeedback() {
    const buttons = document.querySelectorAll("[data-add-to-cart]");

    buttons.forEach(button => {
        button.addEventListener("click", () => {
            const originalText = button.dataset.originalText || button.textContent;

            button.dataset.originalText = originalText;
            button.textContent = "Added!";
            button.classList.add("is-added");

            setTimeout(() => {
                button.textContent = originalText;
                button.classList.remove("is-added");
            }, 1400);
        });
    });
}

function initPasswordToggle() {
    const toggleButtons = document.querySelectorAll('[onclick*="togglePassword"]');

    toggleButtons.forEach(button => {
        button.addEventListener('click', function(e) {
            e.preventDefault();
            const onclick = this.getAttribute('onclick') || '';
            const match = onclick.match(/togglePassword\('([^']+)'/);

            if (!match) {
                return;
            }

            togglePassword(match[1], this);
        });
    });
}

function initSearchClear() {
    const searchWrappers = document.querySelectorAll("[data-search-box]");

    searchWrappers.forEach(wrapper => {
        const input = wrapper.querySelector("input");
        const clearButton = wrapper.querySelector("[data-search-clear]");

        if (!input || !clearButton) return;

        clearButton.addEventListener("click", () => {
            input.value = "";
            input.focus();
        });

        input.addEventListener("input", () => {
            clearButton.hidden = input.value.trim() === "";
        });

        clearButton.hidden = input.value.trim() === "";
    });
}

// Legacy function for compatibility with existing forms
function togglePassword(inputId, button) {
    const input = document.getElementById(inputId);
    const icon = button.querySelector('i');

    if (!input || !icon) {
        return;
    }

    if (input.type === 'password') {
        input.type = 'text';
        icon.className = 'bi bi-eye-slash';
        button.setAttribute('aria-label', 'Hide password');
    } else {
        input.type = 'password';
        icon.className = 'bi bi-eye';
        button.setAttribute('aria-label', 'Show password');
    }
}

function initKawaiiInteractions() {
    const sparkleButtons = document.querySelectorAll('.sparkle');

    sparkleButtons.forEach(button => {
        button.addEventListener('click', function(e) {
            createSparkleEffect(e.target);
        });
    });

    const glowElements = document.querySelectorAll('.glow-on-hover, .glow-pink, .glow-mint');

    glowElements.forEach(element => {
        element.addEventListener('mouseenter', function() {
            this.style.transition = 'filter 0.3s ease';
        });
    });

    const kawaiiCards = document.querySelectorAll('.kawaii-card');

    kawaiiCards.forEach(card => {
        card.addEventListener('mouseenter', function() {
            this.style.transform = 'translateY(-4px)';
        });

        card.addEventListener('mouseleave', function() {
            this.style.transform = 'translateY(0)';
        });
    });
}

function createSparkleEffect(element) {
    const sparkle = document.createElement('span');
    sparkle.textContent = '✨';
    sparkle.style.position = 'absolute';
    sparkle.style.pointerEvents = 'none';
    sparkle.style.zIndex = '9999';
    sparkle.style.fontSize = '1.2rem';
    sparkle.style.animation = 'sparkle-float 1s ease-out forwards';

    const rect = element.getBoundingClientRect();
    sparkle.style.left = (rect.left + Math.random() * rect.width) + 'px';
    sparkle.style.top = (rect.top + Math.random() * rect.height) + 'px';

    document.body.appendChild(sparkle);

    setTimeout(() => {
        if (sparkle.parentNode) {
            sparkle.parentNode.removeChild(sparkle);
        }
    }, 1000);
}

function respectsReducedMotion() {
    return window.matchMedia('(prefers-reduced-motion: reduce)').matches;
}

if (!document.querySelector('#sparkle-animation-style')) {
    const style = document.createElement('style');
    style.id = 'sparkle-animation-style';
    style.textContent = `
        @keyframes sparkle-float {
            0% {
                opacity: 0;
                transform: translateY(0) scale(0.8);
            }
            50% {
                opacity: 1;
                transform: translateY(-20px) scale(1.2);
            }
            100% {
                opacity: 0;
                transform: translateY(-40px) scale(0.8);
            }
        }
    `;
    document.head.appendChild(style);
}
