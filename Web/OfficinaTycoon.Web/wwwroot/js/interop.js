// Small JS bridge for the Blazor app. No third-party code, no network calls.
(function () {
  'use strict';

  const storage = {
    get(key) { try { return window.localStorage.getItem(key); } catch (e) { return null; } },
    // Errors (e.g. quota exceeded, private mode) propagate to .NET, where SaveManager reports a failed save.
    set(key, value) { window.localStorage.setItem(key, value); },
    remove(key) { try { window.localStorage.removeItem(key); } catch (e) { /* ignore */ } }
  };

  const lifecycle = {
    register(dotnetRef) {
      document.addEventListener('visibilitychange', () => {
        dotnetRef.invokeMethod('OnVisibilityChanged', document.visibilityState === 'hidden');
      });
      // iOS may freeze/kill a home-screen app without further events: save on pagehide too.
      window.addEventListener('pagehide', () => dotnetRef.invokeMethod('OnPageHide'));
      // Ask the browser to keep our storage (best effort; not supported everywhere).
      if (navigator.storage && navigator.storage.persist) { navigator.storage.persist().catch(() => {}); }
    }
  };

  // Very short, soft synthesized cues (no audio files, nothing casino-like). iOS unlocks audio on the first tap.
  let ctx = null;
  const notes = {
    tap: [[760, 0.045]], buy: [[520, 0.07], [660, 0.07]], upgrade: [[440, 0.07], [550, 0.07], [660, 0.07]],
    order: [[523, 0.09], [659, 0.09], [784, 0.09]], unlock: [[392, 0.08], [523, 0.08], [659, 0.08], [784, 0.08]],
    reward: [[660, 0.09], [880, 0.09]], event: [[330, 0.12], [392, 0.12]], error: [[200, 0.12]]
  };
  let lastTap = 0;
  function sfx(name) {
    try {
      const seq = notes[name];
      if (!seq) return;
      const now = performance.now();
      if (name === 'tap') { if (now - lastTap < 60) return; lastTap = now; }
      if (!ctx) { const AC = window.AudioContext || window.webkitAudioContext; if (!AC) return; ctx = new AC(); }
      if (ctx.state === 'suspended') ctx.resume();
      let t = ctx.currentTime;
      for (const [freq, dur] of seq) {
        const osc = ctx.createOscillator();
        const gain = ctx.createGain();
        osc.type = 'sine';
        osc.frequency.value = freq;
        gain.gain.setValueAtTime(0.0001, t);
        gain.gain.exponentialRampToValueAtTime(0.12, t + 0.008);
        gain.gain.exponentialRampToValueAtTime(0.0001, t + dur);
        osc.connect(gain).connect(ctx.destination);
        osc.start(t);
        osc.stop(t + dur + 0.02);
        t += dur;
      }
    } catch (e) { /* audio is optional */ }
  }

  function isIos() {
    return /iPad|iPhone|iPod/.test(navigator.userAgent) || (navigator.platform === 'MacIntel' && navigator.maxTouchPoints > 1);
  }
  function isStandalone() {
    return window.navigator.standalone === true || window.matchMedia('(display-mode: standalone)').matches;
  }
  async function copyText(text) {
    try { await navigator.clipboard.writeText(text); return true; } catch (e) { return false; }
  }
  function queryFlag(name) {
    return new URLSearchParams(window.location.search).get(name) === '1';
  }

  // Invite links: share sheet when available (iPhone), otherwise copy to clipboard.
  async function share(title, text, url) {
    try {
      if (navigator.share) { await navigator.share({ title, text, url }); return 'shared'; }
    } catch (e) { if (e && e.name === 'AbortError') return 'cancelled'; }
    return (await copyText(text + ' ' + url)) ? 'copied' : 'failed';
  }
  function queryValue(name) { return new URLSearchParams(window.location.search).get(name) || ''; }
  function clearQuery(name) {
    try {
      const u = new URL(window.location.href); u.searchParams.delete(name);
      window.history.replaceState(null, '', u.pathname + u.search + u.hash);
    } catch (e) { /* ignore */ }
  }
  function pageUrl() { return window.location.origin + window.location.pathname; }

  window.officina = { storage, lifecycle, sfx, isIos, isStandalone, copyText, queryFlag, share, queryValue, clearQuery, pageUrl };
})();
