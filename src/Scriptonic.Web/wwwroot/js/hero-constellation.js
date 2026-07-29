/*
 * Scriptonic hero constellation.
 * Code glyphs drift over the hero, scatter away from the cursor and — when the
 * visitor goes idle — settle into the { S } logo mark. Canvas 2D, no deps.
 * Skipped entirely under prefers-reduced-motion (a static formation is drawn once).
 */
(function () {
  "use strict";

  var canvas = document.getElementById("hero-canvas");
  if (!canvas || !canvas.getContext) return;

  // If the stylesheet that positions the canvas absolute didn't load (stale
  // cache, blocked CSS), the canvas sits in flow and sizing it to its parent
  // would grow the hero unboundedly. Refuse to run rather than break layout.
  if (getComputedStyle(canvas).position !== "absolute") {
    canvas.width = canvas.height = 0;
    return;
  }

  var ctx = canvas.getContext("2d");
  var reducedMotion = window.matchMedia("(prefers-reduced-motion: reduce)").matches;

  var GLYPHS = ["{", "}", "{", "}", ";", "=", ">", "<", "/", "(", ")", "$"];
  var COLORS = ["#6d8fb5", "#4f709a", "#9db4cf", "#35516f"];
  var LINK_DIST = 110;
  var IDLE_AFTER_MS = 2600;

  // Coarse-pointer devices get a lower pixel-density cap: fill-rate is the
  // bottleneck on phones and the drift field doesn't need retina crispness.
  var coarsePointer = window.matchMedia("(hover: none), (pointer: coarse)").matches;
  var dpr = Math.min(window.devicePixelRatio || 1, coarsePointer ? 1.5 : 2);
  var width = 0, height = 0;
  var particles = [];
  var homes = [];
  var spriteCache = {};
  var pointer = { x: -9999, y: -9999, speed: 0, lastX: 0, lastY: 0, lastMove: 0 };
  var focus = 0; // 0 = free drift, 1 = fully assembled logo
  var running = false, rafId = 0, visible = true, tick = 0;

  function sprite(glyph, color, size) {
    var key = glyph + "|" + color + "|" + size;
    if (spriteCache[key]) return spriteCache[key];
    var pad = 4, s = document.createElement("canvas");
    s.width = (size + pad * 2) * dpr;
    s.height = (size + pad * 2) * dpr;
    var c = s.getContext("2d");
    c.scale(dpr, dpr);
    c.font = "bold " + size + 'px "Courier Prime", "Courier New", monospace';
    c.textAlign = "center";
    c.textBaseline = "middle";
    c.fillStyle = color;
    c.fillText(glyph, size / 2 + pad, size / 2 + pad);
    spriteCache[key] = s;
    return s;
  }

  /* Sample "{ S }" rendered offscreen into target points for the idle formation.
     Sampling (getImageData) is expensive, so results are cached per count and
     only invalidated when the brand font arrives. */
  var sampleCache = null;
  var SAMPLE_W = 480, SAMPLE_H = 240;
  function samplePoints(count) {
    if (sampleCache && sampleCache.count === count) return sampleCache.pts;
    var off = document.createElement("canvas");
    var ow = SAMPLE_W, oh = SAMPLE_H;
    off.width = ow; off.height = oh;
    var oc = off.getContext("2d");
    oc.font = 'bold 170px "Courier Prime", "Courier New", monospace';
    oc.textAlign = "center";
    oc.textBaseline = "middle";
    oc.fillStyle = "#fff";
    oc.fillText("{ S }", ow / 2, oh / 2);
    var data = oc.getImageData(0, 0, ow, oh).data;
    var pts = [];
    for (var step = 5; step < 14; step++) {
      pts.length = 0;
      for (var y = 0; y < oh; y += step)
        for (var x = 0; x < ow; x += step)
          if (data[(y * ow + x) * 4 + 3] > 128) pts.push([x, y]);
      if (pts.length <= count) break;
    }
    sampleCache = { count: count, pts: pts };
    return pts;
  }

  function buildHomes(count) {
    var ow = SAMPLE_W, oh = SAMPLE_H;
    var pts = samplePoints(count);
    // Map sampled points into a box inside the hero: right of the copy on wide
    // screens, centered behind it on narrow ones.
    var wide = width >= 1024;
    var boxW = Math.min(wide ? width * 0.34 : width * 0.8, 560);
    var boxH = boxW * (oh / ow);
    var cx = wide ? width * 0.76 : width * 0.5;
    var cy = height * 0.5;
    homes = pts.map(function (p) {
      return {
        x: cx + (p[0] / ow - 0.5) * boxW,
        y: cy + (p[1] / oh - 0.5) * boxH
      };
    });
  }

  function makeParticles() {
    var target = Math.round(Math.min(Math.max(width * height / 9000, 60), 190));
    particles = [];
    for (var i = 0; i < target; i++) {
      var size = 11 + Math.random() * 11;
      particles.push({
        x: Math.random() * width,
        y: Math.random() * height,
        vx: (Math.random() - 0.5) * 0.4,
        vy: (Math.random() - 0.5) * 0.4,
        glyph: GLYPHS[(Math.random() * GLYPHS.length) | 0],
        color: COLORS[(Math.random() * COLORS.length) | 0],
        size: Math.round(size),
        alpha: 0.35 + Math.random() * 0.5,
        rot: Math.random() * Math.PI * 2,
        rotSpeed: (Math.random() - 0.5) * 0.004,
        seed: Math.random() * 1000,
        home: null
      });
    }
    buildHomes(particles.length);
    for (var j = 0; j < particles.length; j++)
      particles[j].home = homes.length ? homes[j % homes.length] : null;
  }

  function resize(force) {
    var rect = canvas.parentElement.getBoundingClientRect();
    var w = Math.max(rect.width, 1);
    var h = Math.max(rect.height, 1);
    // Mobile URL-bar resizes fire this without the hero actually changing;
    // rebuilding the particle field there causes a mid-scroll hitch.
    if (!force && Math.abs(w - width) < 2 && Math.abs(h - height) < 2) return;
    width = w;
    height = h;
    canvas.width = width * dpr;
    canvas.height = height * dpr;
    ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
    makeParticles();
    render(); // first frame immediately, even before rAF kicks in
  }

  function drawStatic() {
    resize();
    focus = 1;
    for (var i = 0; i < particles.length; i++) {
      var p = particles[i];
      if (p.home) { p.x = p.home.x; p.y = p.home.y; }
      p.rot = 0;
    }
    render();
  }

  function render() {
    ctx.clearRect(0, 0, width, height);
    var linkAlpha = 0.14 * (1 - focus * 0.55);
    ctx.lineWidth = 1;
    for (var i = 0; i < particles.length; i++) {
      var a = particles[i];
      for (var j = i + 1; j < particles.length; j++) {
        var b = particles[j];
        var dx = a.x - b.x, dy = a.y - b.y;
        var d2 = dx * dx + dy * dy;
        if (d2 < LINK_DIST * LINK_DIST) {
          var t = 1 - Math.sqrt(d2) / LINK_DIST;
          ctx.strokeStyle = "rgba(109, 143, 181," + (t * linkAlpha).toFixed(3) + ")";
          ctx.beginPath();
          ctx.moveTo(a.x, a.y);
          ctx.lineTo(b.x, b.y);
          ctx.stroke();
        }
      }
    }
    for (var k = 0; k < particles.length; k++) {
      var p = particles[k];
      var img = sprite(p.glyph, p.color, p.size);
      ctx.globalAlpha = p.alpha * (0.6 + focus * 0.4);
      ctx.save();
      ctx.translate(p.x, p.y);
      ctx.rotate(p.rot * (1 - focus));
      ctx.drawImage(img, -img.width / (2 * dpr), -img.height / (2 * dpr), img.width / dpr, img.height / dpr);
      ctx.restore();
    }
    ctx.globalAlpha = 1;
  }

  function step() {
    tick++;
    var idleFor = performance.now() - pointer.lastMove;
    var wantFocus = idleFor > IDLE_AFTER_MS ? 1 : 0;
    focus += (wantFocus - focus) * 0.02;

    var spring = 0.0006 + focus * focus * 0.012;
    var noiseAmp = 0.016 * (1 - focus);
    var push = Math.min(pointer.speed, 40);

    for (var i = 0; i < particles.length; i++) {
      var p = particles[i];
      // Gentle wander
      p.vx += Math.sin(tick * 0.01 + p.seed) * noiseAmp;
      p.vy += Math.cos(tick * 0.008 + p.seed * 1.7) * noiseAmp;
      // Spring toward formation slot
      if (p.home) {
        p.vx += (p.home.x - p.x) * spring;
        p.vy += (p.home.y - p.y) * spring;
      }
      // Cursor repulsion, scaled by cursor speed
      var dx = p.x - pointer.x, dy = p.y - pointer.y;
      var d2 = dx * dx + dy * dy;
      var radius = 90 + push * 3;
      if (d2 < radius * radius && d2 > 0.01) {
        var d = Math.sqrt(d2);
        var f = ((radius - d) / radius) * (0.4 + push * 0.05);
        p.vx += (dx / d) * f;
        p.vy += (dy / d) * f;
      }
      p.vx *= 0.94;
      p.vy *= 0.94;
      p.x += p.vx;
      p.y += p.vy;
      p.rot += p.rotSpeed;
      // Soft wrap at edges while drifting free
      if (focus < 0.5) {
        if (p.x < -30) p.x = width + 30; else if (p.x > width + 30) p.x = -30;
        if (p.y < -30) p.y = height + 30; else if (p.y > height + 30) p.y = -30;
      }
    }
    pointer.speed *= 0.9;
    render();
    if (running) rafId = requestAnimationFrame(step);
  }

  function setRunning(on) {
    if (on === running) return;
    running = on;
    if (on) rafId = requestAnimationFrame(step);
    else cancelAnimationFrame(rafId);
  }

  function onPointerMove(e) {
    var rect = canvas.getBoundingClientRect();
    var x = e.clientX - rect.left, y = e.clientY - rect.top;
    var dx = x - pointer.lastX, dy = y - pointer.lastY;
    pointer.speed = Math.min(pointer.speed + Math.sqrt(dx * dx + dy * dy) * 0.4, 60);
    pointer.x = x; pointer.y = y;
    pointer.lastX = x; pointer.lastY = y;
    pointer.lastMove = performance.now();
  }

  if (reducedMotion) {
    // Assembled, motionless logo — still on-brand, no animation.
    if (document.fonts && document.fonts.ready) document.fonts.ready.then(drawStatic);
    else drawStatic();
    window.addEventListener("resize", drawStatic);
    return;
  }

  resize();
  pointer.lastMove = performance.now() - IDLE_AFTER_MS; // start assembled
  // Re-sample formation once the brand font is in, so the S uses Courier Prime.
  if (document.fonts && document.fonts.ready) document.fonts.ready.then(function () { spriteCache = {}; sampleCache = null; resize(true); });

  var resizeTimer = 0;
  function queueResize() {
    clearTimeout(resizeTimer);
    resizeTimer = setTimeout(resize, 150);
  }
  // ResizeObserver catches late layout (fonts, CSS) that window.resize misses.
  if ("ResizeObserver" in window) {
    var lastW = width, lastH = height;
    new ResizeObserver(function () {
      var r = canvas.parentElement.getBoundingClientRect();
      if (Math.abs(r.width - lastW) > 1 || Math.abs(r.height - lastH) > 1) {
        lastW = r.width; lastH = r.height;
        queueResize();
      }
    }).observe(canvas.parentElement);
  }
  window.addEventListener("resize", queueResize);
  window.addEventListener("pointermove", onPointerMove, { passive: true });
  window.addEventListener("pointerdown", onPointerMove, { passive: true });

  if ("IntersectionObserver" in window) {
    new IntersectionObserver(function (entries) {
      visible = entries[0].isIntersecting;
      setRunning(visible && !document.hidden);
    }).observe(canvas);
  }
  document.addEventListener("visibilitychange", function () {
    setRunning(visible && !document.hidden);
  });

  setRunning(true);
})();
