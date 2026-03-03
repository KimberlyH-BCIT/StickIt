// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.
document.addEventListener('turbo:load', initWishlistAjax);
document.addEventListener('DOMContentLoaded', initWishlistAjax);
document.addEventListener('DOMContentLoaded', formatAllPrices);
document.addEventListener('DOMContentLoaded', initProductAutocomplete);

// Highlight matched query substrings in suggestion text
function highlightMatch(text, query) {
    if (!query) return escapeHtml(text);
    try {
        // token-aware prefix highlighting: highlight token prefixes at word boundaries
        const tokens = query.split(/\s+/).map(t => t.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')).filter(t => t.length > 0);
        if (!tokens.length) return escapeHtml(text);
        const pattern = '\\b(' + tokens.join('|') + ')';
        const re = new RegExp(pattern, 'ig');
        return escapeHtml(text).replace(re, '<mark>$1</mark>');
    } catch {
        return escapeHtml(text);
    }
}

function escapeHtml(str) {
    return String(str).replace(/[&"'<>]/g, function (s) {
        return ({
            '&': '&amp;', '"': '&quot;', "'": '&#39;', '<': '&lt;', '>': '&gt;'
        })[s];
    });
}

function initProductAutocomplete() {
    const input = document.getElementById('productNameInput');
    if (!input) return;
    const box = document.getElementById('productNameSuggestions');
    let timer = null;
    // ensure ARIA attributes on input and suggestion container
    if (box && !box.hasAttribute('role')) {
        box.setAttribute('role', 'listbox');
        box.setAttribute('aria-label', 'Product suggestions');
        box.id = box.id || 'productNameSuggestions';
    }
    input.setAttribute('aria-autocomplete', 'list');
    input.setAttribute('aria-controls', box.id);
    input.addEventListener('input', function () {
        clearTimeout(timer);
        const q = input.value.trim();
        if (!q) { box.style.display = 'none'; return; }
        timer = setTimeout(async () => {
        try {
            const res = await fetch('/Product/SearchNames?q=' + encodeURIComponent(q));
                if (!res.ok) return;
                const arr = await res.json();
                box.innerHTML = '';
                arr.forEach((item, idx) => {
                    const a = document.createElement('a');
                    a.className = 'list-group-item list-group-item-action d-flex justify-content-between align-items-center';
                    a.href = '#';
                    a.dataset.index = idx;
                    a.dataset.id = item.id;

                    // build highlighted name HTML from server-provided match positions if available
                    let nameHtml = '';
                    if (item.matches && item.matches.length) {
                        try {
                            const nm = item.name || '';
                            // ensure matches are sorted
                            const matches = item.matches.map(m => ({ start: m.start, length: m.length })).sort((a,b) => a.start - b.start);
                            let last = 0;
                            const parts = [];
                            for (const m of matches) {
                                if (m.start > last) parts.push(escapeHtml(nm.substring(last, m.start)));
                                parts.push('<mark>' + escapeHtml(nm.substr(m.start, m.length)) + '</mark>');
                                last = m.start + m.length;
                            }
                            if (last < nm.length) parts.push(escapeHtml(nm.substr(last)));
                            nameHtml = parts.join('');
                        } catch (e) { nameHtml = escapeHtml(item.name || ''); }
                    } else {
                        nameHtml = highlightMatch(item.name || '', q);
                    }

                    a.innerHTML = `<div class="d-flex align-items-center"><img src="${item.thumbnail || '/images/placeholder.png'}" alt="" style="width:40px;height:40px;object-fit:cover;margin-right:8px;border-radius:4px;"> <span class="suggestion-name">${nameHtml}</span></div><small class="text-muted">${new Intl.NumberFormat(window.appCulture || undefined, { style: 'currency', currency: window.appCurrency || 'CAD' }).format(item.price)}</small>`;
                    a.addEventListener('click', function (e) { e.preventDefault(); input.value = item.name; box.style.display = 'none'; window.location.href = '/Product/Details/' + item.id; });
                    box.appendChild(a);
                });
    // keyboard navigation - attach once
    if (!input.dataset.autocompleteKeybound) {
        input.addEventListener('keydown', function (e) {
            const visible = box.style.display === 'block';
            if (!visible) return;
            const items = Array.from(box.querySelectorAll('.list-group-item'));
            if (!items.length) return;
            let idx = items.findIndex(it => it.classList.contains('active'));

            const pageStep = 5;

            if (e.key === 'ArrowDown') {
                e.preventDefault();
                if (idx >= 0) items[idx].classList.remove('active');
                idx = (idx + 1) % items.length;
                items[idx].classList.add('active');
                items[idx].scrollIntoView({ block: 'nearest' });
            } else if (e.key === 'ArrowUp') {
                e.preventDefault();
                if (idx >= 0) items[idx].classList.remove('active');
                // if none selected, move to last
                if (idx === -1) idx = items.length - 1;
                else idx = (idx - 1 + items.length) % items.length;
                items[idx].classList.add('active');
                items[idx].scrollIntoView({ block: 'nearest' });
            } else if (e.key === 'Enter') {
                e.preventDefault();
                if (idx >= 0) {
                    const it = items[idx];
                    const id = it.dataset.id;
                    const name = it.querySelector('span').textContent;
                    input.value = name;
                    box.style.display = 'none';
                    window.location.href = '/Product/Details/' + id;
                } else if (items.length === 1) {
                    // single result, go to it
                    const it = items[0];
                    const id = it.dataset.id;
                    input.value = it.querySelector('span').textContent;
                    box.style.display = 'none';
                    window.location.href = '/Product/Details/' + id;
                }
            } else if (e.key === 'Escape') {
                box.style.display = 'none';
            } else if (e.key === 'Home') {
                e.preventDefault();
                if (idx >= 0) items[idx].classList.remove('active');
                idx = 0;
                items[idx].classList.add('active');
                items[idx].scrollIntoView({ block: 'nearest' });
            } else if (e.key === 'End') {
                e.preventDefault();
                if (idx >= 0) items[idx].classList.remove('active');
                idx = items.length - 1;
                items[idx].classList.add('active');
                items[idx].scrollIntoView({ block: 'nearest' });
            } else if (e.key === 'PageDown') {
                e.preventDefault();
                if (idx >= 0) items[idx].classList.remove('active');
                if (idx === -1) idx = Math.min(pageStep - 1, items.length - 1);
                else idx = Math.min(items.length - 1, idx + pageStep);
                items[idx].classList.add('active');
                items[idx].scrollIntoView({ block: 'nearest' });
            } else if (e.key === 'PageUp') {
                e.preventDefault();
                if (idx >= 0) items[idx].classList.remove('active');
                if (idx === -1) idx = Math.max(0, items.length - pageStep);
                else idx = Math.max(0, idx - pageStep);
                items[idx].classList.add('active');
                items[idx].scrollIntoView({ block: 'nearest' });
            }
            // type-ahead within the visible list: handle printable characters
            if (e.key && e.key.length === 1 && /^[\w\p{L}]$/u.test(e.key)) {
                // append to buffer
                typeaheadBuffer += e.key;
                clearTimeout(typeaheadTimer);
                typeaheadTimer = setTimeout(() => { typeaheadBuffer = ''; }, 800);
                const buf = typeaheadBuffer.toLowerCase();
                // find first item whose text starts with buffer
                const found = items.findIndex(it => it.textContent.trim().toLowerCase().startsWith(buf));
                if (found >= 0) {
                    if (idx >= 0) items[idx].classList.remove('active');
                    items[found].classList.add('active');
                    items[found].scrollIntoView({ block: 'nearest' });
                }
            }
            // update aria-activedescendant and aria-selected
            const active = box.querySelector('.list-group-item.active');
            if (active) {
                input.setAttribute('aria-activedescendant', active.id);
                box.querySelectorAll('.list-group-item').forEach(it => it.setAttribute('aria-selected', it === active ? 'true' : 'false'));
            } else {
                input.removeAttribute('aria-activedescendant');
                box.querySelectorAll('.list-group-item').forEach(it => it.setAttribute('aria-selected', 'false'));
            }
        });
        input.dataset.autocompleteKeybound = '1';
    }
                box.style.display = arr.length ? 'block' : 'none';
            } catch { }
        }, 250);
    });
    document.addEventListener('click', function (e) { if (!input.contains(e.target) && !box.contains(e.target)) box.style.display = 'none'; });
}

function formatAllPrices() {
    const locale = window.appCulture || undefined;
    const currency = window.appCurrency || 'CAD';
    document.querySelectorAll('.price').forEach(el => {
        const v = parseFloat(el.dataset.price);
        if (!isNaN(v)) {
            el.textContent = new Intl.NumberFormat(locale, { style: 'currency', currency }).format(v);
        }
    });
}

function initWishlistAjax() {
    // Add handlers for add-to-wishlist forms
    document.querySelectorAll('.add-to-wishlist-form').forEach(form => {
        form.addEventListener('submit', async function (e) {
            e.preventDefault();
            const productId = form.dataset.productId || form.querySelector('input[name="productId"]').value;
            const token = form.querySelector('input[name="__RequestVerificationToken"]').value;
            const button = form.querySelector('.add-to-wishlist-button');

            // Optimistic UI: disable button and show spinner, increment count locally
            const previousText = button.innerHTML;
            button.disabled = true;
            button.innerHTML = '<span class="spinner-border spinner-border-sm" role="status" aria-hidden="true"></span> Adding...';
            const prevCount = getCurrentWishlistCount();
            if (prevCount !== null) updateWishlistCount(prevCount + 1);

            try {
                const res = await fetch(form.action, {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/x-www-form-urlencoded; charset=UTF-8' },
                    body: `_RequestVerificationToken=${encodeURIComponent(token)}&productId=${encodeURIComponent(productId)}`
                });

                const json = await res.json();

                if (!json.success) {
                    // revert optimistic count
                    if (prevCount !== null) updateWishlistCount(prevCount);
                    showTempMessage('warning', json.message || 'Failed to add to wishlist');
                } else {
                    showTempMessage('success', json.message || 'Added to wishlist');
                    // if server returned authoritative count, use it
                    if (typeof json.count !== 'undefined') updateWishlistCount(json.count);
                }
            } catch (err) {
                if (prevCount !== null) updateWishlistCount(prevCount);
                showTempMessage('danger', 'Network error');
            } finally {
                button.disabled = false;
                button.innerHTML = previousText;
            }
        });
    });

    // Remove handlers
    document.querySelectorAll('.remove-from-wishlist-form').forEach(form => {
        form.addEventListener('submit', async function (e) {
            e.preventDefault();
            const productId = form.dataset.productId || form.querySelector('input[name="productId"]').value;
            const token = form.querySelector('input[name="__RequestVerificationToken"]').value;
            const button = form.querySelector('.remove-from-wishlist-button');

            // Optimistic UI: remove card immediately and decrement count
            const card = form.closest('.col-md-4');
            const prevCount = getCurrentWishlistCount();
            if (card) card.style.transition = 'opacity 0.2s';
            if (prevCount !== null) updateWishlistCount(prevCount - 1);
            if (card) card.style.opacity = '0.5';
            button.disabled = true;

            try {
                const res = await fetch(form.action, {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/x-www-form-urlencoded; charset=UTF-8' },
                    body: `_RequestVerificationToken=${encodeURIComponent(token)}&productId=${encodeURIComponent(productId)}`
                });

                const json = await res.json();

                if (!json.success) {
                    // revert UI
                    if (prevCount !== null) updateWishlistCount(prevCount);
                    if (card) card.style.opacity = '1';
                    showTempMessage('warning', json.message || 'Failed to remove');
                    button.disabled = false;
                } else {
                    showTempMessage('success', json.message || 'Removed from wishlist');
                    if (card) card.remove();
                    if (typeof json.count !== 'undefined') updateWishlistCount(json.count);
                }
            } catch (err) {
                if (prevCount !== null) updateWishlistCount(prevCount);
                if (card) card.style.opacity = '1';
                showTempMessage('danger', 'Network error');
                button.disabled = false;
            }
        });
    });
}

// level: 'success', 'danger', 'warning', 'info'
function showTempMessage(level, text) {
    if (!text) return;
    const container = document.querySelector('.container');
    if (!container) return;
    const alert = document.createElement('div');
    let cls = 'alert-info';
    if (level === 'success') cls = 'alert-success';
    if (level === 'danger') cls = 'alert-danger';
    if (level === 'warning') cls = 'alert-warning';
    alert.className = `alert ${cls} alert-dismissible fade show`;
    alert.role = 'alert';
    alert.innerHTML = `${text} <button type="button" class="btn-close" data-bs-dismiss="alert" aria-label="Close"></button>`;
    container.insertAdjacentElement('afterbegin', alert);
    setTimeout(() => {
        alert.classList.remove('show');
        alert.classList.add('hide');
        alert.remove();
    }, 5000);
}

// Updates the wishlist count in the navbar. If count is undefined, does nothing (used when server doesn't return authoritative count).
function updateWishlistCount(count) {
    if (typeof count === 'undefined') return;
    const link = document.getElementById('my-wishlist-link');
    if (link) {
        link.textContent = `My Wishlist (${count})`;
    }
}

// Parses the current wishlist count from the navbar link. Returns null if it can't parse it (e.g. if user is not logged in).
function getCurrentWishlistCount() {
    const link = document.getElementById('my-wishlist-link');
    if (!link) return null;
    const m = link.textContent.match(/\((\d+)\)/);
    if (!m) return null;
    return parseInt(m[1], 10);
}
