// In development, always fetch from the network and do not enable offline support.
// This is because caching would make development dangerous (old assets staying around).
self.addEventListener('fetch', () => { });
