// AutoCo Service Worker — v2.5.23
// Blazor Server requereix connexió al servidor, però podem:
//   1. Instal·lar l'app com a PWA (standalone)
//   2. Mostrar pàgina offline quan no hi ha xarxa
//
// NOTA: site.css / app.js / charts.js NO es cachen aquí.
// Blazor Server necessita xarxa per funcionar; aquests fitxers els gestiona
// la caché HTTP normal (ETag/Last-Modified), que s'invalida automàticament.
// Només es cachen recursos necessaris per mostrar la pàgina d'error offline.
// IMPORTANT: Actualitzar CACHE_NAME quan canviïn els STATIC_ASSETS (rarament).

const CACHE_NAME = 'autoco-v2.5.23';

const STATIC_ASSETS = [
    '/offline.html',
    '/favicon.ico',
    '/images/logo2.png',
];

// ── Instal·lació: pre-cache assets estàtics ──────────────────────────────────
self.addEventListener('install', event => {
    event.waitUntil(
        caches.open(CACHE_NAME).then(cache => cache.addAll(STATIC_ASSETS))
    );
    self.skipWaiting();
});

// ── Activació: elimina caches antigues ───────────────────────────────────────
self.addEventListener('activate', event => {
    event.waitUntil(
        caches.keys().then(keys =>
            Promise.all(keys.filter(k => k !== CACHE_NAME).map(k => caches.delete(k)))
        )
    );
    self.clients.claim();
});

// ── Fetch: network-first per a tot excepte assets estàtics ───────────────────
self.addEventListener('fetch', event => {
    const { request } = event;
    const url = new URL(request.url);

    // Ignora peticions no GET i cross-origin (SignalR WebSocket, etc.)
    if (request.method !== 'GET' || url.origin !== self.location.origin) return;

    // Ignora Blazor i SignalR (requereixen xarxa sempre)
    if (url.pathname.startsWith('/_blazor') ||
        url.pathname.startsWith('/_framework') ||
        url.pathname.startsWith('/_content') ||
        url.pathname.startsWith('/api/')) return;

    // Assets estàtics: cache-first
    if (STATIC_ASSETS.includes(url.pathname)) {
        event.respondWith(
            caches.match(request).then(cached => cached || fetch(request))
        );
        return;
    }

    // Resta (pàgines Blazor): network-first, fallback offline
    event.respondWith(
        fetch(request).catch(() => caches.match('/offline.html'))
    );
});
