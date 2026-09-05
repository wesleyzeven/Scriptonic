/*
 * Scriptonic motion pass: Lenis smooth scroll + GSAP ScrollTrigger.
 * Progressive enhancement only — without JS (or with prefers-reduced-motion)
 * every element stays fully visible and the page behaves as before.
 */
(function () {
  "use strict";

  if (!window.gsap || !window.ScrollTrigger) return;
  if (window.matchMedia("(prefers-reduced-motion: reduce)").matches) return;

  gsap.registerPlugin(ScrollTrigger);

  // Mobile URL-bar show/hide fires resize events during scroll; refreshing all
  // triggers on each one causes forced reflows mid-scroll (visible freezes).
  ScrollTrigger.config({ ignoreMobileResize: true });

  var finePointer = window.matchMedia("(hover: hover) and (pointer: fine)").matches;

  /* ---- Smooth scroll (desktop only — touch scrolling stays native) ------ */
  var lenis = null;
  if (window.Lenis && finePointer) {
    lenis = new Lenis({ duration: 1.05 });
    lenis.on("scroll", ScrollTrigger.update);
    gsap.ticker.add(function (time) { lenis.raf(time * 1000); });
    gsap.ticker.lagSmoothing(0);
  }

  /* ---- Reveals: headings, labels and cards ------------------------------ */
  var revealTargets = document.querySelectorAll("main section h2, main section .braced, main section > div > p:not(.braced), main .prose-block");
  revealTargets.forEach(function (el) {
    gsap.set(el, { y: 26, autoAlpha: 0 });
    gsap.to(el, {
      y: 0, autoAlpha: 1, duration: 0.7, ease: "power2.out",
      scrollTrigger: { trigger: el, start: "top 88%", once: true }
    });
  });

  var cards = Array.prototype.slice.call(document.querySelectorAll("main .card, main section .grid > div"));
  // Reveal a form as one unit (not row by row) and skip elements whose ancestor
  // is already in the set — nested reveals compound into visible jank.
  cards = cards.filter(function (el) {
    if (el.tagName !== "FORM" && el.closest("form")) return false;
    return !cards.some(function (other) { return other !== el && other.contains(el); });
  });
  cards.forEach(function (el) { gsap.set(el, { y: 34, autoAlpha: 0 }); });
  ScrollTrigger.batch(cards, {
    start: "top 88%",
    once: true,
    onEnter: function (batch) {
      gsap.to(batch, { y: 0, autoAlpha: 1, duration: 0.7, stagger: 0.09, ease: "power2.out" });
    }
  });

  /* ---- Typing effect on { braced } labels ------------------------------- */
  document.querySelectorAll("main .braced").forEach(function (el) {
    var full = el.textContent;
    if (!full || full.length > 40) return;
    var state = { n: full.length };
    ScrollTrigger.create({
      trigger: el, start: "top 90%", once: true,
      onEnter: function () {
        state.n = 0;
        el.textContent = " ";
        gsap.to(state, {
          n: full.length, duration: Math.min(0.05 * full.length + 0.2, 1), ease: "none",
          onUpdate: function () { el.textContent = full.slice(0, Math.round(state.n)) || " "; },
          onComplete: function () { el.textContent = full; }
        });
      }
    });
  });

  /* ---- Giant scrubbing braces around the portfolio section ------------- */
  var braceL = document.querySelector("[data-brace-l]");
  var braceR = document.querySelector("[data-brace-r]");
  if (braceL && braceR) {
    var braceSection = braceL.closest("section");
    gsap.fromTo(braceL, { xPercent: 40, autoAlpha: 0 }, {
      xPercent: 0, autoAlpha: 1, ease: "none",
      scrollTrigger: { trigger: braceSection, start: "top 90%", end: "top 25%", scrub: true }
    });
    gsap.fromTo(braceR, { xPercent: -40, autoAlpha: 0 }, {
      xPercent: 0, autoAlpha: 1, ease: "none",
      scrollTrigger: { trigger: braceSection, start: "top 90%", end: "top 25%", scrub: true }
    });
    // Parallax runs symmetrically around 0 so the braces sit exactly centred
    // when the section itself is centred in the viewport.
    gsap.fromTo([braceL, braceR], { yPercent: -10 }, {
      yPercent: 10, ease: "none",
      scrollTrigger: { trigger: braceSection, start: "top bottom", end: "bottom top", scrub: true }
    });
  }

  /* ---- Process section: sticky wireframe that assembles a website ------- */
  var processSection = document.getElementById("process");
  if (processSection) {
    var steps = processSection.querySelectorAll("[data-step]");
    var svg = document.getElementById("process-wireframe");

    var setStage = function (stage) {
      steps.forEach(function (s, i) {
        s.classList.toggle("is-active", i === stage);
      });
      if (!svg) return;
      // Stage 0: dashed outlines draw in. 1: blocks solidify. 2: site goes live.
      var outlines = svg.querySelectorAll("[data-wf-outline]");
      var fills = svg.querySelectorAll("[data-wf-fill]");
      var live = svg.querySelectorAll("[data-wf-live]");
      gsap.to(outlines, { opacity: stage >= 0 ? 1 : 0, duration: 0.4, stagger: 0.06 });
      gsap.to(fills, {
        opacity: stage >= 1 ? 1 : 0, scale: stage >= 1 ? 1 : 0.92,
        transformOrigin: "center", duration: 0.45, stagger: 0.07, ease: "power2.out"
      });
      gsap.to(live, { opacity: stage >= 2 ? 1 : 0, duration: 0.4, stagger: 0.1 });
      gsap.to(svg, {
        filter: stage >= 2 ? "drop-shadow(0 0 18px rgba(109,143,181,0.45))" : "drop-shadow(0 0 0px rgba(109,143,181,0))",
        duration: 0.6
      });
    };

    steps.forEach(function (stepEl, i) {
      ScrollTrigger.create({
        trigger: stepEl,
        start: "top 62%",
        end: "bottom 38%",
        onEnter: function () { setStage(i); },
        onEnterBack: function () { setStage(i); },
        // Ranges of neighbouring steps overlap, so a step scrolled back out of
        // the top must hand the stage back to its predecessor explicitly.
        onLeaveBack: function () { if (i > 0) setStage(i - 1); }
      });
    });
    setStage(0);

    if (svg) {
      var pulse = svg.querySelector("[data-wf-livedot]");
      if (pulse) {
        gsap.to(pulse, { opacity: 0.25, repeat: -1, yoyo: true, duration: 0.9, ease: "sine.inOut" });
      }
    }

    /* Snap to the nearest step once scrolling settles, so a visitor never
       parks halfway between two steps with the wireframe mid-transition.
       Desktop only: that is where the sticky two-column layout lives. */
    var desktop = window.matchMedia("(min-width: 64rem)");
    var stepTargets = [];
    var snapTimer = null;
    var snapping = false;

    var maxScroll = function () {
      return Math.max(0, document.documentElement.scrollHeight - window.innerHeight);
    };

    // Measured right before snapping, not on refresh: the header is taller
    // while the page sits at the top, so positions taken at load are off by
    // the shrink once the visitor has scrolled down.
    var measureSteps = function () {
      var scrollY = window.scrollY;
      var limit = maxScroll();
      stepTargets = Array.prototype.map.call(steps, function (s) {
        var r = s.getBoundingClientRect();
        // Scroll position at which this step is vertically centred.
        return Math.min(limit, Math.round(scrollY + r.top + r.height / 2 - window.innerHeight / 2));
      });
    };

    var scrollTo = function (y) {
      snapping = true;
      if (lenis) {
        lenis.scrollTo(y, { duration: 0.7 });
      } else {
        window.scrollTo({ top: y, behavior: "smooth" });
      }
      // A wheel/touch during the snap cancels it without any callback, so
      // release the guard on a timer rather than on completion.
      setTimeout(function () { snapping = false; }, 800);
    };

    var snapToNearest = function () {
      if (!desktop.matches || snapping) return;
      measureSteps();
      if (!stepTargets.length) return;
      var y = window.scrollY;
      // Only act while the visitor is actually "in" the steps: half a step
      // before the first and after the last. Beyond that the page is theirs.
      var slack = (stepTargets.length > 1 ? stepTargets[1] - stepTargets[0] : window.innerHeight * 0.5) / 2;
      if (y < stepTargets[0] - slack || y > stepTargets[stepTargets.length - 1] + slack) return;
      var nearest = stepTargets[0];
      stepTargets.forEach(function (t) { if (Math.abs(t - y) < Math.abs(nearest - y)) nearest = t; });
      if (Math.abs(nearest - y) > 4) scrollTo(nearest);
    };

    ScrollTrigger.create({
      trigger: processSection,
      start: "top bottom",
      end: "bottom top",
      onUpdate: function () {
        clearTimeout(snapTimer);
        snapTimer = setTimeout(snapToNearest, 160);
      },
      onLeave: function () { clearTimeout(snapTimer); },
      onLeaveBack: function () { clearTimeout(snapTimer); }
    });
  }

  /* ---- Magnetic buttons (fine pointers only) ---------------------------- */
  if (finePointer) {
    document.querySelectorAll(".btn-primary, .btn-secondary, .btn-light").forEach(function (btn) {
      var qx = gsap.quickTo(btn, "x", { duration: 0.35, ease: "power3.out" });
      var qy = gsap.quickTo(btn, "y", { duration: 0.35, ease: "power3.out" });
      btn.addEventListener("pointermove", function (e) {
        var r = btn.getBoundingClientRect();
        qx((e.clientX - (r.left + r.width / 2)) * 0.18);
        qy((e.clientY - (r.top + r.height / 2)) * 0.28);
      });
      btn.addEventListener("pointerleave", function () { qx(0); qy(0); });
    });
  }

  /* Recalculate after images/fonts settle. */
  window.addEventListener("load", function () { ScrollTrigger.refresh(); });
})();
