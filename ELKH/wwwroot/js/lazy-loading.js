/*
╔==================================================================================╗
║ LAZY LOADING MODULE - Modern Image Optimization                                  ║
╠==================================================================================╣
║ Provides high-performance lazy loading with modern browser APIs                 ║
║                                                                                  ║
║ FEATURES:                                                                        ║
║ • Intersection Observer API for efficient viewport detection                     ║
║ • WebP format detection with fallback support                                   ║
║ • Smooth fade-in transitions with blur effect removal                           ║
║ • Progressive image loading with placeholder handling                           ║
║ • Automatic retry mechanism for failed loads                                    ║
║ • Native lazy loading fallback for supported browsers                           ║
║                                                                                  ║
║ BROWSER SUPPORT:                                                                 ║
║ • Modern browsers: Full feature support                                         ║
║ • Legacy browsers: Graceful degradation to immediate loading                    ║
╚==================================================================================╝
*/

(function() {
    'use strict';

    // =========================================================================
    // Configuration and Browser Feature Detection
    // =========================================================================
    
    const CONFIG = {
        // Intersection Observer options
        rootMargin: '50px 0px',        // Load images 50px before they enter viewport
        threshold: 0.01,                // Trigger when 1% of image is visible
        
        // Loading behavior
        fadeInDuration: 300,            // Fade-in animation duration in ms
        retryAttempts: 3,              // Number of retry attempts for failed loads
        retryDelay: 1000,              // Delay between retry attempts in ms
        
        // Selectors
        lazyImageSelector: '.lazy-image',
        loadedClass: 'lazy-loaded',
        loadingClass: 'lazy-loading',
        errorClass: 'lazy-error',
        
        // WebP support
        supportsWebP: null             // Will be detected on init
    };

    // Feature detection
    const FEATURES = {
        intersectionObserver: 'IntersectionObserver' in window,
        nativeLazyLoading: 'loading' in HTMLImageElement.prototype,
        webP: null // Detected asynchronously
    };

    // =========================================================================
    // WebP Support Detection
    // =========================================================================

    /**
     * Asynchronously detects WebP support using a minimal test image
     * Returns Promise<boolean> indicating browser WebP capability
     */
    async function detectWebPSupport() {
        return new Promise((resolve) => {
            const webP = new Image();
            // Both load and error events resolve - we check dimensions for success
            webP.onload = webP.onerror = function() {
                // WebP test image should decode to 2x2 pixels if supported
                resolve(webP.height === 2);
            };
            // Minimal WebP test image (2x2 pixels, transparent)
            webP.src = 'data:image/webp;base64,UklGRjoAAABXRUJQVlA4IC4AAACyAgCdASoCAAIALmk0mk0iIiIiIgBoSygABc6WWgAA/veff/0PP8bA//LwYAAA';
        });
    }

    // =========================================================================
    // Lazy Loading Implementation
    // =========================================================================

    class LazyImageLoader {
        constructor() {
            this.observer = null;
            // Track loading state and retry attempts per image element
            this.images = new Map(); // Element -> { state, attempts, timestamp }
            this.retryTimeouts = new Map(); // Element -> timeoutId

            this.init();
        }

        async init() {
            // Detect WebP support
            FEATURES.webP = await detectWebPSupport();
            CONFIG.supportsWebP = FEATURES.webP;
            
            console.log('Lazy Loading: WebP support detected:', FEATURES.webP);
            
            // Initialize based on feature support
            if (FEATURES.intersectionObserver) {
                this.initIntersectionObserver();
            } else {
                this.fallbackToImmediateLoading();
            }
            
            this.findAndProcessImages();
        }

        initIntersectionObserver() {
            this.observer = new IntersectionObserver((entries) => {
                entries.forEach(entry => {
                    if (entry.isIntersecting) {
                        this.loadImage(entry.target);
                        this.observer?.unobserve(entry.target);
                    }
                });
            }, {
                rootMargin: CONFIG.rootMargin,
                threshold: CONFIG.threshold
            });
        }

        findAndProcessImages() {
            const images = document.querySelectorAll(CONFIG.lazyImageSelector);
            
            images.forEach(img => {
                if (FEATURES.intersectionObserver) {
                    this.observer?.observe(img);
                } else {
                    this.loadImage(img);
                }
            });
            
            console.log(`Lazy Loading: Found ${images.length} images to process`);
        }

        loadImage(img) {
            if (this.images.has(img)) {
                return; // Already processing this image
            }

            this.images.set(img, { attempts: 0, loading: true });
            img.classList.add(CONFIG.loadingClass);

            const dataSrc = img.getAttribute('data-src');
            const dataSrcset = img.getAttribute('data-srcset');

            if (!dataSrc) {
                console.warn('Lazy Loading: No data-src found for image', img);
                return;
            }

            // Choose the best source based on browser support
            const finalSrc = this.getBestImageSource(dataSrc);
            const finalSrcset = this.getBestSrcset(dataSrcset);

            this.attemptImageLoad(img, finalSrc, finalSrcset);
        }

        getBestImageSource(src) {
            if (!FEATURES.webP || !src.includes('.webp')) {
                return src;
            }
            
            // If browser doesn't support WebP, try to find alternative
            if (!CONFIG.supportsWebP) {
                const fallbackSrc = src.replace(/\.webp$/, '.jpg').replace(/\.webp$/, '.png');
                return fallbackSrc;
            }
            
            return src;
        }

        getBestSrcset(srcset) {
            if (!srcset || CONFIG.supportsWebP) {
                return srcset;
            }
            
            // Convert WebP srcset to fallback formats
            return srcset.replace(/\.webp\s/g, '.jpg ');
        }

        attemptImageLoad(img, src, srcset) {
            const imageState = this.images.get(img);
            if (!imageState) return;

            imageState.attempts++;

            const tempImage = new Image();
            
            // Set up event handlers
            tempImage.onload = () => this.handleImageLoad(img, tempImage, src, srcset);
            tempImage.onerror = () => this.handleImageError(img, src, srcset);
            
            // Start loading
            if (srcset) {
                tempImage.srcset = srcset;
            }
            tempImage.src = src;
        }

        handleImageLoad(img, tempImage, src, srcset) {
            const imageState = this.images.get(img);
            if (!imageState) return;

            // Update the actual image
            img.src = src;
            if (srcset) {
                img.srcset = srcset;
            }

            // Remove data attributes
            img.removeAttribute('data-src');
            img.removeAttribute('data-srcset');

            // Apply loaded styling
            img.classList.remove(CONFIG.loadingClass);
            img.classList.add(CONFIG.loadedClass);

            // Fade in animation
            this.animateFadeIn(img);

            // Clean up
            this.images.delete(img);
            this.clearRetryTimeout(img);

            console.log('Lazy Loading: Successfully loaded image', src);
        }

        handleImageError(img, src, srcset) {
            const imageState = this.images.get(img);
            if (!imageState) return;

            console.warn(`Lazy Loading: Failed to load image ${src} (attempt ${imageState.attempts})`);

            if (imageState.attempts < CONFIG.retryAttempts) {
                // Retry after delay
                const timeoutId = setTimeout(() => {
                    this.attemptImageLoad(img, src, srcset);
                }, CONFIG.retryDelay);
                
                this.retryTimeouts.set(img, timeoutId);
            } else {
                // Give up and mark as error
                img.classList.remove(CONFIG.loadingClass);
                img.classList.add(CONFIG.errorClass);
                
                // Try fallback to original src
                const originalSrc = img.getAttribute('data-src');
                if (originalSrc && originalSrc !== src) {
                    img.src = originalSrc;
                }

                this.images.delete(img);
                this.clearRetryTimeout(img);
                
                console.error('Lazy Loading: Gave up loading image after', CONFIG.retryAttempts, 'attempts:', src);
            }
        }

        animateFadeIn(img) {
            // Set initial state
            img.style.opacity = '0';
            img.style.transition = `opacity ${CONFIG.fadeInDuration}ms ease-in-out`;
            
            // Remove blur effect if present
            if (img.style.filter && img.style.filter.includes('blur')) {
                img.style.filter = img.style.filter.replace(/blur\([^)]*\)/g, '');
            }

            // Trigger fade in
            requestAnimationFrame(() => {
                img.style.opacity = '1';
            });

            // Clean up after animation
            setTimeout(() => {
                img.style.transition = '';
            }, CONFIG.fadeInDuration);
        }

        clearRetryTimeout(img) {
            const timeoutId = this.retryTimeouts.get(img);
            if (timeoutId) {
                clearTimeout(timeoutId);
                this.retryTimeouts.delete(img);
            }
        }

        fallbackToImmediateLoading() {
            console.log('Lazy Loading: Falling back to immediate loading (no IntersectionObserver)');
            
            const images = document.querySelectorAll(CONFIG.lazyImageSelector);
            images.forEach(img => {
                const dataSrc = img.getAttribute('data-src');
                if (dataSrc) {
                    img.src = dataSrc;
                    img.removeAttribute('data-src');
                    
                    const dataSrcset = img.getAttribute('data-srcset');
                    if (dataSrcset) {
                        img.srcset = dataSrcset;
                        img.removeAttribute('data-srcset');
                    }
                }
            });
        }

        // Public method to manually trigger loading of new images
        refresh() {
            this.findAndProcessImages();
        }

        // Cleanup method
        destroy() {
            if (this.observer) {
                this.observer.disconnect();
            }
            
            this.retryTimeouts.forEach(timeoutId => clearTimeout(timeoutId));
            this.retryTimeouts.clear();
            this.images.clear();
        }
    }

    // =========================================================================
    // CSS for Enhanced Loading Experience
    // =========================================================================
    
    function injectLazyLoadingCSS() {
        if (document.getElementById('lazy-loading-styles')) {
            return; // Already injected
        }

        const style = document.createElement('style');
        style.id = 'lazy-loading-styles';
        style.textContent = `
            .lazy-image {
                transition: opacity 300ms ease-in-out, filter 300ms ease-in-out;
            }
            
            .lazy-image:not(.lazy-loaded) {
                filter: blur(5px);
            }
            
            .lazy-loading {
                background: linear-gradient(90deg, #f0f0f0 25%, #e0e0e0 50%, #f0f0f0 75%);
                background-size: 200% 100%;
                animation: loading-shimmer 2s infinite;
            }
            
            .lazy-loaded {
                filter: none !important;
            }
            
            .lazy-error {
                background: #f5f5f5 url('data:image/svg+xml,<svg xmlns="http://www.w3.org/2000/svg" width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><circle cx="12" cy="12" r="10"/><path d="m9 9 6 6"/><path d="m15 9-6 6"/></svg>') center no-repeat;
                background-size: 24px 24px;
            }
            
            @keyframes loading-shimmer {
                0% { background-position: -200% 0; }
                100% { background-position: 200% 0; }
            }
            
            /* Responsive image optimization */
            .optimized-image {
                max-width: 100%;
                height: auto;
            }
        `;
        
        document.head.appendChild(style);
    }

    // =========================================================================
    // Initialization and Public API
    // =========================================================================
    
    let lazyLoader = null;

    function initLazyLoading() {
        if (lazyLoader) {
            lazyLoader.destroy();
        }
        
        injectLazyLoadingCSS();
        lazyLoader = new LazyImageLoader();
        
        // Make available globally for manual refresh
        window.LazyImageLoader = {
            refresh: () => lazyLoader?.refresh(),
            destroy: () => lazyLoader?.destroy(),
            config: CONFIG,
            features: FEATURES
        };
    }

    // Auto-initialize when DOM is ready
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', initLazyLoading);
    } else {
        initLazyLoading();
    }

    // Re-scan for images when new content is added
    const observer = new MutationObserver((mutations) => {
        let shouldRefresh = false;
        
        mutations.forEach(mutation => {
            mutation.addedNodes.forEach(node => {
                if (node.nodeType === Node.ELEMENT_NODE) {
                    const element = node;
                    if (element.matches && element.matches(CONFIG.lazyImageSelector)) {
                        shouldRefresh = true;
                    } else if (element.querySelector && element.querySelector(CONFIG.lazyImageSelector)) {
                        shouldRefresh = true;
                    }
                }
            });
        });
        
        if (shouldRefresh) {
            setTimeout(() => lazyLoader?.refresh(), 100);
        }
    });

    observer.observe(document.body, {
        childList: true,
        subtree: true
    });

    console.log('Lazy Loading: Module initialized');
})();