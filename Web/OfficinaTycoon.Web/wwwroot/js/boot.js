// Registered from an external file (not inline) so the Content-Security-Policy can forbid inline scripts.
if ('serviceWorker' in navigator) {
  navigator.serviceWorker.register('service-worker.js').catch(() => { /* offline support is optional */ });
}
