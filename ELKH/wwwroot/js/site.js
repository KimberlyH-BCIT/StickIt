/*
╔==================================================================================╗
║ CORE APPLICATION JAVASCRIPT - Data & Business Logic                             ║
║ Handles AJAX operations, data processing, and core application functionality   ║
╚==================================================================================╝

TABLE OF CONTENTS:
- Entry Points & Initialization: DOMContentLoaded event handlers
- Utility Helper Functions: XSS prevention, alert notifications
- Product Search Autocomplete: Live search with API integration
- Wishlist AJAX Operations: Add/remove wishlist functionality  
- Product Card Navigation: Clickable product card behaviors
- Newsletter Subscription: Email subscription with validation

PURPOSE:
This file contains JavaScript for core application functionality including
AJAX operations, API communication, data processing, and business logic.
For UI interactions and design system behaviors, see kawaii-ui.js.

SECURITY & ACCESSIBILITY:
- All dynamic content is XSS-protected via escapeHtml()
- WAI-ARIA compliant with screen reader support
- CSRF tokens preserved for all AJAX form submissions
- Progressive enhancement: all features work without JavaScript
*/

// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// ===============================================================================
// ║ Entry Points & Initialization                                              ║
// ║ Core application features initialized when DOM is ready                    ║
// ===============================================================================
document.addEventListener('DOMContentLoaded', initWishlistAjax);
document.addEventListener('DOMContentLoaded', initProductAutocomplete);
document.addEventListener('DOMContentLoaded', initProductCardNavigation);

// ===============================================================================
// ║ Utility Helper Functions                                                   ║
// ║ Reusable functions for security, UI feedback, and DOM manipulation        ║
// ===============================================================================

/**
 * Escapes the five HTML special characters to prevent XSS when inserting
 * untrusted strings via innerHTML.
 */
function escapeHtml(str) {
    return String(str).replace(/[&"'<>]/g, s => ({
        '&': '&amp;', '"': '&quot;', "'": '&#39;', '<': '&lt;', '>': '&gt;'
    })[s]);
}

/**
 * Displays a transient Bootstrap alert at the top of .container.
 * Auto-dismisses after 5 seconds.
 * @param {'success'|'danger'|'warning'|'info'} level - Bootstrap alert variant.
 * @param {string} text - Message text; HTML-escaped before insertion to prevent XSS.
 */
function showTempMessage(level, text) {
    if (!text) return;
    const container = document.querySelector('.container');
    if (!container) return;

    // Map level names to Bootstrap alert classes; unknown levels default to info.
    const cls = { success: 'alert-success', danger: 'alert-danger', warning: 'alert-warning' }[level] || 'alert-info';

    const alert = document.createElement('div');
    alert.className = `alert ${cls} alert-dismissible fade show`;
    alert.role = 'alert';
    alert.setAttribute('aria-atomic', 'true');
    // escapeHtml guards against XSS from server-supplied message strings.
    alert.innerHTML = `${escapeHtml(text)} <button type="button" class="btn-close" data-bs-dismiss="alert" aria-label="Close"></button>`;
    container.insertAdjacentElement('afterbegin', alert);
    setTimeout(() => { alert.classList.remove('show'); alert.classList.add('hide'); alert.remove(); }, 5000);
}

// ===============================================================================
// ║ Product Search Autocomplete                                                ║
// ║ Intelligent search with debounced requests and accessibility support      ║
// ===============================================================================

function initProductAutocomplete() {
    const input = document.getElementById('productNameInput');
    if (!input) return;
    const box = document.getElementById('productNameSuggestions');

    // Debounce timer - reset on every keystroke so the fetch fires only after
    // the user pauses typing (250 ms), avoiding a request per character.
    let debounceTimer = null;

    // Type-ahead state: accumulate printable key presses within an 800 ms window
    // so users can jump to a dropdown item by typing its first few characters.
    let typeaheadBuffer = '';
    let typeaheadTimer  = null;

    // ── ARIA setup ────────────────────────────────────────────────────────────
    // The suggestion box acts as a WAI-ARIA listbox. The input declares its
    // relationship to it via aria-controls so screen readers can find the list.
    if (box && !box.hasAttribute('role')) {
        box.setAttribute('role', 'listbox');
        box.setAttribute('aria-label', 'Product suggestions');
        box.id = box.id || 'productNameSuggestions';
    }
    input.setAttribute('aria-autocomplete', 'list');
    input.setAttribute('aria-controls', box.id);

    // ── Keyboard navigation ───────────────────────────────────────────────────
    // Attached here (not inside the fetch callback) so the handler exists before
    // the first fetch completes. The flag prevents duplicate listeners if
    // initProductAutocomplete is ever called more than once.
    if (!input.dataset.autocompleteKeybound) {
        input.addEventListener('keydown', handleKeydown);
        input.dataset.autocompleteKeybound = '1';
    }

    // ── Input / fetch ─────────────────────────────────────────────────────────
    input.addEventListener('input', function () {
        clearTimeout(debounceTimer);
        const q = input.value.trim();
        if (!q) { box.style.display = 'none'; return; }

        debounceTimer = setTimeout(async () => {
            try {
                const res = await fetch('/Product/SearchNames?q=' + encodeURIComponent(q));
                if (!res.ok) return;
                const arr = await res.json();

                // Replace previous suggestions with fresh results.
                box.innerHTML = '';
                arr.forEach((item, idx) => box.appendChild(buildSuggestionItem(item, idx, q)));
                box.style.display = arr.length ? 'block' : 'none';
            } catch { /* silently ignore network / parse errors */ }
        }, 250);
    });

    // Close the dropdown when the user clicks outside the input or suggestion box.
    document.addEventListener('click', e => {
        if (!input.contains(e.target) && !box.contains(e.target))
            box.style.display = 'none';
    });

    // ── Private helpers ───────────────────────────────────────────────────────

    /**
     * Builds and returns a single <a> suggestion element for `item`.
     * Thumbnail URL is HTML-escaped to prevent attribute injection.
     */
    function buildSuggestionItem(item, idx, q) {
        const a = document.createElement('a');
        a.className = 'list-group-item list-group-item-action d-flex justify-content-between align-items-center';
        a.href = '/Product/Details/' + item.id;
        a.dataset.index = idx;
        a.dataset.id = item.id;

        const price = new Intl.NumberFormat(
            undefined,
            { style: 'currency', currency: 'CAD' }
        ).format(item.price);

        a.innerHTML = `
            <div class="d-flex align-items-center">
                <img src="${escapeHtml(item.thumbnail || '/images/placeholder.png')}" alt=""
                     style="width:40px;height:40px;object-fit:cover;margin-right:8px;border-radius:4px;">
                <span class="suggestion-name">${buildNameHtml(item, q)}</span>
            </div>
            <small class="text-muted">${price}</small>`;

        a.addEventListener('click', e => {
            e.preventDefault();
            input.value = item.name;
            box.style.display = 'none';
            window.location.href = a.href;
        });
        return a;
    }

    /**
     * Returns safely HTML-escaped text with query-token matches wrapped in <mark>.
     * Splits the query into whitespace-delimited tokens, escapes each for regex use,
     * then applies word-boundary (\b) matching so only prefix/whole-word hits are
     * highlighted rather than mid-word substrings.
     * Falls back to plain escaped text if regex construction fails.
     */
    function highlightMatch(text, query) {
        if (!query) return escapeHtml(text);
        try {
            const tokens = query
                .split(/\s+/)
                .map(t => t.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')) // escape regex metacharacters
                .filter(t => t.length > 0);
            if (!tokens.length) return escapeHtml(text);
            // \b ensures matches only begin at word boundaries (prefix highlighting).
            const re = new RegExp('\\b(' + tokens.join('|') + ')', 'ig');
            return escapeHtml(text).replace(re, '<mark>$1</mark>');
        } catch {
            return escapeHtml(text);
        }
    }

    /**
     * Returns safely-escaped HTML for the suggestion name with match highlights.
     *
     * When the server supplies explicit match positions (start + length pairs)
     * they are used directly because they reflect the actual fuzzy-match spans.
     * The algorithm walks the name left-to-right using a `last` pointer, emitting
     * escapeHtml()-wrapped plain text between spans and <mark> tags over them.
     * Matches are sorted ascending first so the pointer always advances forward.
     *
     * Falls back to token-regex highlighting when no positions are provided.
     */
    function buildNameHtml(item, q) {
        const name = item.name || '';
        if (item.matches && item.matches.length) {
            try {
                const matches = item.matches
                    .map(m => ({ start: m.start, length: m.length }))
                    .sort((a, b) => a.start - b.start); // ensure left-to-right order
                let last = 0;
                const parts = [];
                for (const m of matches) {
                    if (m.start > last) parts.push(escapeHtml(name.substring(last, m.start)));
                    parts.push('<mark>' + escapeHtml(name.substr(m.start, m.length)) + '</mark>');
                    last = m.start + m.length;
                }
                if (last < name.length) parts.push(escapeHtml(name.substring(last)));
                return parts.join('');
            } catch {
                return escapeHtml(name);
            }
        }
        return highlightMatch(name, q);
    }

    /**
     * Keyboard handler for the autocomplete dropdown.
     *
     * Supported keys:
     *   ArrowDown / ArrowUp  – move selection by one row (wraps at ends)
     *   Home / End           – jump to first / last item
     *   PageDown / PageUp    – move by PAGE_STEP rows
     *   Enter                – navigate to the highlighted item (or only result)
     *   Escape               – close the dropdown
     *   Printable chars      – type-ahead jump using the accumulated key buffer
     *
     * After every navigation, aria-activedescendant on the input and aria-selected
     * on each item are updated so screen readers announce the current selection.
     */
    function handleKeydown(e) {
        if (box.style.display !== 'block') return;
        const items = Array.from(box.querySelectorAll('.list-group-item'));
        if (!items.length) return;

        const PAGE_STEP = 5;
        let idx = items.findIndex(it => it.classList.contains('active'));

        // Moves focus to items[newIdx]: removes active from the previous item,
        // adds it to the new one, and scrolls it into view. Extracted to eliminate
        // the identical three-line block that was repeated once per navigation key.
        function activateItem(newIdx) {
            if (idx >= 0) items[idx].classList.remove('active');
            idx = newIdx;
            items[idx].classList.add('active');
            items[idx].scrollIntoView({ block: 'nearest' });
        }

        switch (e.key) {
            case 'ArrowDown':
                e.preventDefault();
                // (idx + 1) % length wraps last → first.
                activateItem((idx + 1) % items.length);
                break;

            case 'ArrowUp':
                e.preventDefault();
                // (idx - 1 + length) % length wraps first → last.
                // When nothing is selected (idx === -1) jump straight to the last item.
                activateItem(idx === -1 ? items.length - 1 : (idx - 1 + items.length) % items.length);
                break;

            case 'Home':
                e.preventDefault();
                activateItem(0);
                break;

            case 'End':
                e.preventDefault();
                activateItem(items.length - 1);
                break;

            case 'PageDown':
                e.preventDefault();
                activateItem(idx === -1
                    ? Math.min(PAGE_STEP - 1, items.length - 1)
                    : Math.min(items.length - 1, idx + PAGE_STEP));
                break;

            case 'PageUp':
                e.preventDefault();
                activateItem(idx === -1
                    ? Math.max(0, items.length - PAGE_STEP)
                    : Math.max(0, idx - PAGE_STEP));
                break;

            case 'Enter': {
                e.preventDefault();
                // Navigate to the highlighted item, or to the only result if none is highlighted.
                const target = idx >= 0 ? items[idx] : (items.length === 1 ? items[0] : null);
                if (target) {
                    input.value = target.querySelector('span').textContent;
                    box.style.display = 'none';
                    window.location.href = '/Product/Details/' + target.dataset.id;
                }
                break;
            }

            case 'Escape':
                box.style.display = 'none';
                break;

            default:
                // Type-ahead: accumulate printable characters in a short-lived buffer
                // and jump to the first item whose text starts with the buffer.
                // The buffer clears after 800 ms of inactivity to start fresh.
                if (e.key.length === 1 && /^[\w\p{L}]$/u.test(e.key)) {
                    typeaheadBuffer += e.key;
                    clearTimeout(typeaheadTimer);
                    typeaheadTimer = setTimeout(() => { typeaheadBuffer = ''; }, 800);
                    const buf = typeaheadBuffer.toLowerCase();
                    const found = items.findIndex(it => it.textContent.trim().toLowerCase().startsWith(buf));
                    if (found >= 0) activateItem(found);
                }
                break;
        }

        // Sync ARIA state: aria-activedescendant on the input points to the focused
        // item's id; aria-selected mirrors that for listbox role compliance.
        const active = box.querySelector('.list-group-item.active');
        if (active) {
            input.setAttribute('aria-activedescendant', active.id);
            items.forEach(it => it.setAttribute('aria-selected', it === active ? 'true' : 'false'));
        } else {
            input.removeAttribute('aria-activedescendant');
            items.forEach(it => it.setAttribute('aria-selected', 'false'));
        }
    }
}

// ===============================================================================
// ║ Wishlist AJAX Operations                                                   ║
// ║ Dynamic wishlist management with server synchronization                   ║
// ===============================================================================

/**
 * Updates the wishlist count displayed in the navbar link.
 * No-op when count is undefined (server response omitted the authoritative count).
 */
function updateWishlistCount(count) {
    if (typeof count === 'undefined') return;
    const link = document.getElementById('my-wishlist-link');
    if (link) link.textContent = `My Wishlist (${count})`;
}

/**
 * Reads the current wishlist count from the navbar link text.
 * Returns null if the link is absent (user not signed in) or unparseable.
 */
function getCurrentWishlistCount() {
    const link = document.getElementById('my-wishlist-link');
    if (!link) return null;
    const m = link.textContent.match(/(\(\d+\))/);
    return m ? parseInt(m[1], 10) : null;
}

/**
 * Attaches submit handlers to all add/remove wishlist forms on the page.
 * Uses optimistic UI: the navbar count and card visibility are updated
 * immediately before the server responds, then reverted on failure.
 */
function initWishlistAjax() {
    document.querySelectorAll('.add-to-wishlist-form').forEach(form => {
        form.addEventListener('submit', async function (e) {
            e.preventDefault();
            const productId = form.dataset.productId || form.querySelector('input[name="productId"]').value;
            const token     = form.querySelector('input[name="__RequestVerificationToken"]').value;
            const button    = form.querySelector('.add-to-wishlist-button');

            // Optimistic UI: show spinner and increment count before the response arrives.
            const previousText = button.innerHTML;
            button.disabled = true;
            button.innerHTML = '<span class="spinner-border spinner-border-sm" role="status" aria-hidden="true"></span> Adding...';
            const prevCount = getCurrentWishlistCount();
            if (prevCount !== null) updateWishlistCount(prevCount + 1);

            let succeeded = false;
            try {
                const res  = await fetch(form.action, {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/x-www-form-urlencoded; charset=UTF-8' },
                    body: `__RequestVerificationToken=${encodeURIComponent(token)}&productId=${encodeURIComponent(productId)}`
                });
                const json = await res.json();

                if (!json.success) {
                    // Revert optimistic count increment on failure.
                    if (prevCount !== null) updateWishlistCount(prevCount);
                    showTempMessage('warning', json.message || 'Failed to add to wishlist');
                } else {
                    succeeded = true;
                    showTempMessage('success', json.message || 'Added to wishlist');
                    // Prefer the server's authoritative count over the local estimate.
                    if (typeof json.count !== 'undefined') updateWishlistCount(json.count);
                }
            } catch {
                if (prevCount !== null) updateWishlistCount(prevCount);
                showTempMessage('danger', 'Network error');
            } finally {
                button.disabled = false;
                if (succeeded) {
                    // Swap to filled-heart "Wishlisted" state and lock the button
                    // so it cannot be submitted again (item is already in the wishlist).
                    button.innerHTML = '♥&nbsp;Wishlisted';
                    button.classList.remove('btn-outline-secondary');
                    button.classList.add('btn-secondary');
                    button.disabled = true;
                } else {
                    button.innerHTML = previousText;
                }
            }
        });
    });

    document.querySelectorAll('.remove-from-wishlist-form').forEach(form => {
        form.addEventListener('submit', async function (e) {
            e.preventDefault();
            const productId = form.dataset.productId || form.querySelector('input[name="productId"]').value;
            const token     = form.querySelector('input[name="__RequestVerificationToken"]').value;
            const button    = form.querySelector('.remove-from-wishlist-button');

            // Optimistic UI: fade out the card and decrement count immediately.
            const card      = form.closest('.col-md-4');
            const prevCount = getCurrentWishlistCount();
            if (card) { card.style.transition = 'opacity 0.2s'; card.style.opacity = '0.5'; }
            if (prevCount !== null) updateWishlistCount(prevCount - 1);
            button.disabled = true;

            try {
                const res  = await fetch(form.action, {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/x-www-form-urlencoded; charset=UTF-8' },
                    body: `__RequestVerificationToken=${encodeURIComponent(token)}&productId=${encodeURIComponent(productId)}`
                });
                const json = await res.json();

                if (!json.success) {
                    // Revert optimistic removal on failure.
                    if (prevCount !== null) updateWishlistCount(prevCount);
                    if (card) card.style.opacity = '1';
                    showTempMessage('warning', json.message || 'Failed to remove');
                    button.disabled = false;
                } else {
                    showTempMessage('success', json.message || 'Removed from wishlist');
                    if (card) card.remove();
                    if (typeof json.count !== 'undefined') updateWishlistCount(json.count);
                }
            } catch {
                if (prevCount !== null) updateWishlistCount(prevCount);
                if (card) card.style.opacity = '1';
                showTempMessage('danger', 'Network error');
                button.disabled = false;
            }
        });
    });
}

// ─── Product Card Navigation ──────────────────────────────────────────────────

/**
 * Enables click-to-navigate on product cards while preserving button interactions.
 * Clicking anywhere on a card (except buttons/forms) navigates to the product details.
 * Form submissions and button clicks are handled normally via stopPropagation.
 */
function initProductCardNavigation() {
    document.querySelectorAll('.product-card').forEach(function (card) {
        card.addEventListener('click', function (e) {
            // Don't navigate if clicking on interactive elements
            if (e.target.closest('form') || e.target.closest('button') || e.target.closest('a')) {
                return;
            }

            // Navigate to product details
            const href = card.getAttribute('data-href');
            if (href) {
                window.location.href = href;
            }
        });

        // Stop propagation on forms and buttons to prevent card navigation
        card.querySelectorAll('form, button').forEach(function (elem) {
            elem.addEventListener('click', function (e) {
                e.stopPropagation();
            });
        });
    });
}

