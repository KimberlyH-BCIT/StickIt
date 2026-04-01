// =====================================================================
// SERVICE WORKER - Progressive Web App Support
// =====================================================================
//
// Provides offline capabilities and caching strategies for the ELKH
// sticker store. Implements cache-first strategy for static assets
// and network-first strategy for dynamic content.
//
// FEATURES:
// - Offline page fallback for navigation requests
// - Static asset caching (CSS, JS, images)
// - API response caching with expiration
// - Background sync for failed requests
// - Cache management and cleanup
// =====================================================================

const CACHE_NAME = 'elkh-pwa-v2.0.0';
const STATIC_CACHE_NAME = `${CACHE_NAME}-static`;
const DYNAMIC_CACHE_NAME = `${CACHE_NAME}-dynamic`;
const API_CACHE_NAME = `${CACHE_NAME}-api`;

// Assets to cache immediately on install
const STATIC_ASSETS = [
  '/',
  '/css/site.css',
  '/css/kawaii-theme.css',
  '/js/site.js',
  '/js/cart-ajax.js',
  '/js/kawaii-ui.js',
  '/js/lazy-loading.js',
  '/lib/bootstrap/dist/css/bootstrap.min.css',
  '/lib/bootstrap/dist/js/bootstrap.bundle.min.js',
  '/lib/jquery/dist/jquery.min.js',
  '/logo-stickit.png',
  '/offline.html'
];

// Routes that should fallback to offline page
const OFFLINE_FALLBACK_ROUTES = [
  '/Product',
  '/Cart',
  '/Wishlist',
  '/Home'
];

// API endpoints to cache
const API_CACHE_PATTERNS = [
  /\/api\/search/,
  /\/api\/products/,
  /\/api\/categories/
];

// Cache duration in milliseconds (24 hours)
const CACHE_DURATION = 24 * 60 * 60 * 1000;

// =====================================================================
// Service Worker Event Handlers
// =====================================================================

self.addEventListener('install', event => {
  console.log('Service Worker: Installing...');
  
  event.waitUntil(
    caches.open(STATIC_CACHE_NAME)
      .then(cache => {
        console.log('Service Worker: Caching static assets');
        return cache.addAll(STATIC_ASSETS);
      })
      .then(() => {
        console.log('Service Worker: Installation complete');
        return self.skipWaiting();
      })
      .catch(error => {
        console.error('Service Worker: Installation failed', error);
      })
  );
});

self.addEventListener('activate', event => {
  console.log('Service Worker: Activating...');

  event.waitUntil(
    caches.keys()
      .then(cacheNames => {
        // Clean up old caches during activation
        // This prevents storage bloat and ensures fresh cache structure
        return Promise.all(
          cacheNames.map(cacheName => {
            // Keep only current version caches, delete everything else
            if (cacheName !== STATIC_CACHE_NAME && 
                cacheName !== DYNAMIC_CACHE_NAME && 
                cacheName !== API_CACHE_NAME) {
              console.log('Service Worker: Deleting old cache', cacheName);
              return caches.delete(cacheName);
            }
          })
        );
      })
      .then(() => {
        console.log('Service Worker: Activation complete');
        // Take control of all clients immediately (don't wait for page reload)
        return self.clients.claim();
      })
  );
});

self.addEventListener('fetch', event => {
  const request = event.request;
  const url = new URL(request.url);

  // =====================================================================
  // FILTER OUT UNSUPPORTED URL SCHEMES
  // =====================================================================
  // Chrome extensions, browser extensions, and other special schemes
  // cannot be cached by Service Workers. Skip these to prevent errors.
  const unsupportedSchemes = ['chrome-extension:', 'moz-extension:', 'safari-extension:', 'edge-extension:'];
  if (unsupportedSchemes.some(scheme => request.url.startsWith(scheme))) {
    // Let the browser handle extension requests normally
    return;
  }

  // Only handle http/https requests - skip blob:, data:, file:, etc.
  if (!request.url.startsWith('http://') && !request.url.startsWith('https://')) {
    return;
  }

  // Only handle GET requests - POST/PUT/DELETE go directly to network
  // This prevents interference with form submissions and API mutations
  if (request.method !== 'GET') {
    return;
  }

  // Route requests to appropriate handlers based on URL patterns
  // Each handler implements different caching strategies for optimal performance
  if (url.pathname.startsWith('/api/')) {
    // API requests: Network-first with cache fallback for offline functionality
    event.respondWith(handleApiRequest(request));
  } else if (isStaticAsset(url.pathname)) {
    // Static assets: Cache-first for maximum performance
    event.respondWith(handleStaticAsset(request));
  } else if (isNavigationRequest(request)) {
    // Page navigation: Network-first with offline page fallback
    event.respondWith(handleNavigationRequest(request));
  } else {
    // Dynamic content: Network-first with cache as backup
    event.respondWith(handleDynamicRequest(request));
  }
});

// =====================================================================
// Request Handlers
// =====================================================================

async function handleApiRequest(request) {
  const url = new URL(request.url);
  
  // Check if this API endpoint should be cached
  const shouldCache = API_CACHE_PATTERNS.some(pattern => pattern.test(url.pathname));
  
  if (!shouldCache) {
    return fetch(request);
  }
  
  try {
    // Network first for API requests
    const networkResponse = await fetch(request);
    
    if (networkResponse.ok) {
      const cache = await caches.open(API_CACHE_NAME);
      
      // Add expiration header
      const responseWithExpiry = new Response(networkResponse.body, {
        status: networkResponse.status,
        statusText: networkResponse.statusText,
        headers: {
          ...Object.fromEntries(networkResponse.headers.entries()),
          'sw-cache-timestamp': Date.now().toString()
        }
      });
      
      cache.put(request, responseWithExpiry.clone());
      return responseWithExpiry;
    }
    
    return networkResponse;
  } catch (error) {
    // Fallback to cache if network fails
    const cachedResponse = await getCachedResponse(request, API_CACHE_NAME);
    if (cachedResponse && !isCacheExpired(cachedResponse)) {
      console.log('Service Worker: Serving cached API response', request.url);
      return cachedResponse;
    }
    
    throw error;
  }
}

async function handleStaticAsset(request) {
  // Cache first for static assets
  const cachedResponse = await caches.match(request);
  if (cachedResponse) {
    return cachedResponse;
  }
  
  try {
    const networkResponse = await fetch(request);
    
    if (networkResponse.ok) {
      const cache = await caches.open(STATIC_CACHE_NAME);
      cache.put(request, networkResponse.clone());
    }
    
    return networkResponse;
  } catch (error) {
    console.error('Service Worker: Failed to fetch static asset', request.url, error);
    throw error;
  }
}

async function handleNavigationRequest(request) {
  try {
    // Try network first for navigation
    const networkResponse = await fetch(request);
    
    if (networkResponse.ok) {
      // Cache successful navigation responses
      const cache = await caches.open(DYNAMIC_CACHE_NAME);
      cache.put(request, networkResponse.clone());
    }
    
    return networkResponse;
  } catch (error) {
    // Check for cached version
    const cachedResponse = await caches.match(request);
    if (cachedResponse) {
      return cachedResponse;
    }
    
    // Fallback to offline page for supported routes
    const url = new URL(request.url);
    const isOfflineRoute = OFFLINE_FALLBACK_ROUTES.some(route => 
      url.pathname.startsWith(route)
    );
    
    if (isOfflineRoute) {
      return caches.match('/offline.html');
    }
    
    throw error;
  }
}

async function handleDynamicRequest(request) {
  // Network first, cache as backup
  try {
    const networkResponse = await fetch(request);
    
    if (networkResponse.ok) {
      const cache = await caches.open(DYNAMIC_CACHE_NAME);
      cache.put(request, networkResponse.clone());
    }
    
    return networkResponse;
  } catch (error) {
    const cachedResponse = await caches.match(request);
    if (cachedResponse) {
      return cachedResponse;
    }
    
    throw error;
  }
}

// =====================================================================
// Utility Functions
// =====================================================================

function isStaticAsset(pathname) {
  return pathname.includes('/css/') ||
         pathname.includes('/js/') ||
         pathname.includes('/lib/') ||
         pathname.includes('/images/') ||
         pathname.endsWith('.png') ||
         pathname.endsWith('.jpg') ||
         pathname.endsWith('.jpeg') ||
         pathname.endsWith('.gif') ||
         pathname.endsWith('.svg') ||
         pathname.endsWith('.webp') ||
         pathname.endsWith('.ico');
}

function isNavigationRequest(request) {
  return request.mode === 'navigate' ||
         (request.method === 'GET' && 
          request.headers.get('accept') && 
          request.headers.get('accept').includes('text/html'));
}

async function getCachedResponse(request, cacheName) {
  const cache = await caches.open(cacheName);
  return cache.match(request);
}

function isCacheExpired(response) {
  const cacheTimestamp = response.headers.get('sw-cache-timestamp');
  if (!cacheTimestamp) return false;
  
  const age = Date.now() - parseInt(cacheTimestamp);
  return age > CACHE_DURATION;
}

// =====================================================================
// Background Sync
// =====================================================================

self.addEventListener('sync', event => {
  if (event.tag === 'background-sync') {
    console.log('Service Worker: Background sync triggered');
    event.waitUntil(doBackgroundSync());
  }
});

async function doBackgroundSync() {
  // Implement background sync logic for failed requests
  // This could include syncing cart updates, analytics, etc.
  console.log('Service Worker: Performing background sync operations');
}

// =====================================================================
// Push Notifications (Future Enhancement)
// =====================================================================

self.addEventListener('push', event => {
  if (!event.data) return;
  
  const data = event.data.json();
  const title = data.title || 'ELKH Notification';
  const options = {
    body: data.body || 'You have a new notification',
    icon: '/logo-stickit.png',
    badge: '/logo-stickit.png',
    tag: data.tag || 'general',
    data: data.data || {}
  };
  
  event.waitUntil(
    self.registration.showNotification(title, options)
  );
});

self.addEventListener('notificationclick', event => {
  event.notification.close();
  
  const urlToOpen = event.notification.data.url || '/';
  
  event.waitUntil(
    self.clients.openWindow(urlToOpen)
  );
});

console.log('Service Worker: Script loaded');
