// Lightweight visual effects on a full-screen canvas (no libraries, no network): sparks, coins flying to the balance,
// confetti, rings. Disabled when the player picks "reduced effects" or the OS asks for reduced motion.
(function () {
  'use strict';
  let canvas = null, ctx = null, dpr = 1, parts = [], running = false, last = 0, enabled = true;
  const reduce = window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches;
  const GOLD = ['#ffd27a', '#ffb347', '#ffe9b0'];
  const CONFETTI = ['#ffaa28', '#5aa0e6', '#6ec878', '#e65a50', '#ebd250', '#aa78e6', '#78dcf5'];

  function ensure() {
    if (canvas) return;
    canvas = document.createElement('canvas');
    canvas.className = 'fx-layer';
    canvas.setAttribute('aria-hidden', 'true');
    document.body.appendChild(canvas);
    ctx = canvas.getContext('2d');
    resize();
    window.addEventListener('resize', resize);
  }
  function resize() {
    dpr = Math.min(window.devicePixelRatio || 1, 2);
    canvas.width = Math.round(innerWidth * dpr);
    canvas.height = Math.round(innerHeight * dpr);
    canvas.style.width = innerWidth + 'px';
    canvas.style.height = innerHeight + 'px';
  }
  const on = () => enabled && !reduce;
  function add(p) {
    if (parts.length > 450) return;
    parts.push(p);
    if (!running) { running = true; last = performance.now(); requestAnimationFrame(loop); }
  }
  function loop(t) {
    const dt = Math.min(0.05, (t - last) / 1000); last = t;
    ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
    ctx.clearRect(0, 0, innerWidth, innerHeight);
    for (let i = parts.length - 1; i >= 0; i--) {
      const p = parts[i];
      p.age += dt;
      if (p.age >= p.life) { if (p.done) p.done(); parts.splice(i, 1); continue; }
      if (p.age < 0) continue;            // staggered start
      step(p, dt); draw(p);
    }
    if (parts.length) requestAnimationFrame(loop);
    else { running = false; ctx.clearRect(0, 0, innerWidth, innerHeight); }
  }
  function step(p, dt) {
    if (p.kind === 'fly') {
      const k = easeInOut(Math.min(1, p.age / p.life));
      const mx = (p.x0 + p.x1) / 2 + p.bend, my = Math.min(p.y0, p.y1) - 60;
      p.x = q(p.x0, mx, p.x1, k); p.y = q(p.y0, my, p.y1, k);
      return;
    }
    p.vy += (p.g || 0) * dt;
    p.vx *= p.drag || 1; p.vy *= p.drag || 1;
    p.x += p.vx * dt; p.y += p.vy * dt;
    if (p.spin) p.rot += p.spin * dt;
  }
  function draw(p) {
    const a = p.kind === 'fly' ? 1 : Math.max(0, 1 - p.age / p.life);
    ctx.globalAlpha = a;
    switch (p.kind) {
      case 'spark':
        ctx.strokeStyle = p.color; ctx.lineWidth = 2; ctx.lineCap = 'round';
        ctx.beginPath(); ctx.moveTo(p.x, p.y); ctx.lineTo(p.x - p.vx * 0.04, p.y - p.vy * 0.04); ctx.stroke();
        break;
      case 'coin': case 'fly':
        ctx.fillStyle = '#c98a14'; ctx.beginPath(); ctx.arc(p.x, p.y, p.r, 0, Math.PI * 2); ctx.fill();
        ctx.fillStyle = p.color; ctx.beginPath(); ctx.arc(p.x - p.r * 0.12, p.y - p.r * 0.12, p.r * 0.78, 0, Math.PI * 2); ctx.fill();
        ctx.fillStyle = '#fff6'; ctx.fillRect(p.x - p.r * 0.45, p.y - p.r * 0.5, p.r * 0.25, p.r * 0.8);
        break;
      case 'confetti':
        ctx.save(); ctx.translate(p.x, p.y); ctx.rotate(p.rot);
        ctx.fillStyle = p.color; ctx.fillRect(-p.r, -p.r * 0.45, p.r * 2, p.r * 0.9); ctx.restore();
        break;
      case 'ring': {
        const k = p.age / p.life;
        ctx.strokeStyle = p.color; ctx.lineWidth = 3 * (1 - k) + 0.5;
        ctx.beginPath(); ctx.arc(p.x, p.y, p.r + k * p.grow, 0, Math.PI * 2); ctx.stroke();
        break;
      }
    }
    ctx.globalAlpha = 1;
  }
  const q = (a, b, c, t) => (1 - t) * (1 - t) * a + 2 * (1 - t) * t * b + t * t * c;
  const easeInOut = t => t < 0.5 ? 2 * t * t : 1 - Math.pow(-2 * t + 2, 2) / 2;
  const rnd = (a, b) => a + Math.random() * (b - a);
  const pick = arr => arr[(Math.random() * arr.length) | 0];
  function center(sel) {
    const el = typeof sel === 'string' ? document.querySelector(sel) : sel;
    if (!el) return null;
    const r = el.getBoundingClientRect();
    return { x: r.left + r.width / 2, y: r.top + r.height / 2, el };
  }

  // ---- public API ----------------------------------------------------------------------------
  function burst(x, y, kind) {
    if (!on()) return; ensure();
    const big = kind === 'frenzy' || kind === 'build';
    const n = kind === 'tap' ? 7 : big ? 22 : 12;
    for (let i = 0; i < n; i++) {
      const ang = rnd(0, Math.PI * 2), sp = rnd(120, big ? 420 : 300);
      add({ kind: 'spark', x, y, vx: Math.cos(ang) * sp, vy: Math.sin(ang) * sp - 60, g: 500, drag: 0.98, age: 0, life: rnd(0.25, 0.55), color: pick(GOLD) });
    }
    const coins = kind === 'tap' ? 2 : big ? 6 : 3;
    for (let i = 0; i < coins; i++)
      add({ kind: 'coin', x, y, vx: rnd(-140, 140), vy: rnd(-380, -220), g: 900, age: 0, life: rnd(0.6, 0.9), r: rnd(4, 7), color: pick(GOLD) });
    if (big) add({ kind: 'ring', x, y, r: 6, grow: 70, age: 0, life: 0.5, color: kind === 'frenzy' ? '#ff7a1a' : '#ffd27a' });
  }
  function burstAt(sel, kind) { const c = center(sel); if (c) burst(c.x, c.y, kind); }
  function flyCoins(fromSel, toSel, count) {
    if (!on()) { bump(toSel); return; }
    ensure();
    const a = center(fromSel), b = center(toSel);
    if (!a || !b) return;
    const n = Math.min(18, Math.max(3, count | 0));
    for (let i = 0; i < n; i++) {
      add({ kind: 'fly', x0: a.x + rnd(-30, 30), y0: a.y + rnd(-12, 12), x1: b.x + rnd(-14, 14), y1: b.y, bend: rnd(-90, 90),
            x: a.x, y: a.y, age: -i * 0.04, life: rnd(0.65, 0.9), r: rnd(5, 8), color: pick(GOLD),
            done: i === n - 1 ? () => bump(toSel) : null });
    }
  }
  function confetti(count) {
    if (!on()) return; ensure();
    const n = Math.min(160, count | 0 || 90);
    for (let i = 0; i < n; i++)
      add({ kind: 'confetti', x: rnd(0, innerWidth), y: rnd(-80, -10), vx: rnd(-60, 60), vy: rnd(120, 320), g: 140, drag: 0.995,
            age: 0, life: rnd(1.6, 2.6), r: rnd(3, 6), rot: rnd(0, 6), spin: rnd(-8, 8), color: pick(CONFETTI) });
  }
  function bump(sel) {
    const el = typeof sel === 'string' ? document.querySelector(sel) : sel;
    if (!el) return;
    el.classList.remove('bump'); void el.offsetWidth; el.classList.add('bump');
  }
  function flash() {
    if (!on()) return;
    const el = document.querySelector('.app');
    if (!el) return;
    el.classList.remove('flash'); void el.offsetWidth; el.classList.add('flash');
  }
  function setEnabled(v) { enabled = !!v; if (!enabled) { parts = []; } }
  function scrollTo(sel) { const el = document.querySelector(sel); if (el) el.scrollIntoView({ block: 'center', behavior: on() ? 'smooth' : 'auto' }); }

  window.officinaFx = { burst, burstAt, flyCoins, confetti, bump, flash, setEnabled, scrollTo };
})();
