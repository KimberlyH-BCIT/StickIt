/* Custom JavaScript enhancements for ELKH Swagger UI */

document.addEventListener('DOMContentLoaded', function() {
    // Add custom analytics tracking for API documentation usage
    if (typeof gtag !== 'undefined') {
        // Track API documentation page views
        gtag('event', 'page_view', {
            page_title: 'API Documentation',
            page_location: window.location.href
        });
    }

    // Add copy-to-clipboard functionality for code examples
    function addCopyButtons() {
        const codeBlocks = document.querySelectorAll('.highlight-code pre');
        codeBlocks.forEach(function(block) {
            if (!block.querySelector('.copy-btn')) {
                const button = document.createElement('button');
                button.className = 'copy-btn';
                button.textContent = '📋 Copy';
                button.style.cssText = `
                    position: absolute;
                    top: 5px;
                    right: 5px;
                    background: #3498db;
                    color: white;
                    border: none;
                    padding: 4px 8px;
                    border-radius: 3px;
                    font-size: 12px;
                    cursor: pointer;
                    z-index: 10;
                `;
                
                button.addEventListener('click', function() {
                    const text = block.textContent;
                    navigator.clipboard.writeText(text).then(function() {
                        button.textContent = '✅ Copied!';
                        setTimeout(function() {
                            button.textContent = '📋 Copy';
                        }, 2000);
                    });
                });
                
                block.style.position = 'relative';
                block.appendChild(button);
            }
        });
    }

    // Add performance metrics display
    function addPerformanceMetrics() {
        const operations = document.querySelectorAll('.opblock');
        operations.forEach(function(op) {
            const summary = op.querySelector('.opblock-summary');
            if (summary && !summary.querySelector('.perf-badge')) {
                const badge = document.createElement('span');
                badge.className = 'perf-badge';
                badge.textContent = '⚡ Fast';
                badge.style.cssText = `
                    background: #27ae60;
                    color: white;
                    font-size: 10px;
                    padding: 2px 6px;
                    border-radius: 3px;
                    margin-left: 8px;
                    font-weight: bold;
                `;
                summary.appendChild(badge);
            }
        });
    }

    // Add API version compatibility warnings
    function addCompatibilityWarnings() {
        const v1Operations = document.querySelectorAll('[data-path*="v1"]');
        v1Operations.forEach(function(op) {
            if (!op.querySelector('.compat-warning')) {
                const warning = document.createElement('div');
                warning.className = 'compat-warning';
                warning.innerHTML = `
                    <div style="background: #fff3cd; border: 1px solid #ffeaa7; padding: 8px 12px; margin: 8px 0; border-radius: 4px; font-size: 12px;">
                        📢 <strong>API v1.x</strong> - Consider upgrading to v2.0 for enhanced features and better performance.
                        <a href="#/v2" style="color: #3498db; text-decoration: underline;">View v2.0 Documentation</a>
                    </div>
                `;
                const opblock = op.querySelector('.opblock-body');
                if (opblock) {
                    opblock.insertBefore(warning, opblock.firstChild);
                }
            }
        });
    }

    // Add interactive examples
    function addInteractiveExamples() {
        const tryItButtons = document.querySelectorAll('.try-out__btn');
        tryItButtons.forEach(function(btn) {
            btn.addEventListener('click', function() {
                // Track try-it-out usage
                if (typeof gtag !== 'undefined') {
                    const operation = btn.closest('.opblock');
                    const method = operation?.classList[1]?.replace('opblock-', '') || 'unknown';
                    const path = operation?.querySelector('.opblock-summary-path')?.textContent || 'unknown';
                    
                    gtag('event', 'api_try_it_out', {
                        method: method.toUpperCase(),
                        path: path,
                        category: 'API Documentation'
                    });
                }
            });
        });
    }

    // Add search functionality enhancement
    function enhanceSearch() {
        const searchInput = document.querySelector('.filter-input');
        if (searchInput) {
            searchInput.placeholder = '🔍 Search endpoints, methods, or responses...';
            searchInput.style.fontSize = '14px';
            searchInput.style.padding = '8px 12px';
        }
    }

    // Add keyboard shortcuts
    function addKeyboardShortcuts() {
        document.addEventListener('keydown', function(e) {
            // Ctrl/Cmd + K to focus search
            if ((e.ctrlKey || e.metaKey) && e.key === 'k') {
                e.preventDefault();
                const searchInput = document.querySelector('.filter-input');
                if (searchInput) {
                    searchInput.focus();
                }
            }
            
            // Escape to clear search
            if (e.key === 'Escape') {
                const searchInput = document.querySelector('.filter-input');
                if (searchInput && document.activeElement === searchInput) {
                    searchInput.value = '';
                    searchInput.dispatchEvent(new Event('input'));
                }
            }
        });
        
        // Add keyboard shortcut info
        const info = document.querySelector('.info');
        if (info && !info.querySelector('.keyboard-shortcuts')) {
            const shortcuts = document.createElement('div');
            shortcuts.className = 'keyboard-shortcuts';
            shortcuts.innerHTML = `
                <details style="margin-top: 16px; font-size: 12px; color: #666;">
                    <summary style="cursor: pointer; font-weight: bold;">⌨️ Keyboard Shortcuts</summary>
                    <div style="margin-top: 8px; padding-left: 16px;">
                        <div><kbd>Ctrl/Cmd + K</kbd> - Focus search</div>
                        <div><kbd>Escape</kbd> - Clear search</div>
                        <div><kbd>Tab</kbd> - Navigate between sections</div>
                    </div>
                </details>
            `;
            info.appendChild(shortcuts);
        }
    }

    // Add response time simulation
    function addResponseTimeSimulation() {
        const executeButtons = document.querySelectorAll('.btn.execute');
        executeButtons.forEach(function(btn) {
            const originalHandler = btn.onclick;
            btn.addEventListener('click', function() {
                // Add loading indicator
                const loader = document.createElement('div');
                loader.innerHTML = '⏱️ Simulating response time...';
                loader.style.cssText = 'font-size: 12px; color: #666; margin-top: 4px;';
                btn.parentNode.appendChild(loader);
                
                setTimeout(function() {
                    loader.remove();
                }, 1000);
            });
        });
    }

    // Initialize all enhancements
    setTimeout(function() {
        addCopyButtons();
        addPerformanceMetrics();
        addCompatibilityWarnings();
        addInteractiveExamples();
        enhanceSearch();
        addKeyboardShortcuts();
        addResponseTimeSimulation();
    }, 1000);

    // Re-run enhancements when Swagger UI updates
    const observer = new MutationObserver(function(mutations) {
        mutations.forEach(function(mutation) {
            if (mutation.addedNodes.length > 0) {
                setTimeout(function() {
                    addCopyButtons();
                    addPerformanceMetrics();
                    addCompatibilityWarnings();
                }, 500);
            }
        });
    });

    observer.observe(document.body, {
        childList: true,
        subtree: true
    });

    console.log('🚀 ELKH Swagger UI enhancements loaded successfully!');
});