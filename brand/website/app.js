/* ============================================================================
   File Backup — brand showcase interactions
   No dependencies, no build step. Every feature degrades to a usable page.
   ========================================================================= */
(function () {
  'use strict';

  var root = document.documentElement;
  var reduceMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches;

  /* ------------------------------------------------------------------ *
   * 1. Theme
   * ------------------------------------------------------------------ */
  var THEME_KEY = 'filebackup.theme';
  var themeToggle = document.getElementById('theme-toggle');
  var themeLabel = themeToggle && themeToggle.querySelector('[data-theme-label]');
  var themeMeta = document.querySelector('meta[name="theme-color"]');

  function systemTheme() {
    return window.matchMedia('(prefers-color-scheme: light)').matches ? 'light' : 'dark';
  }

  function readStoredTheme() {
    try {
      return window.localStorage.getItem(THEME_KEY);
    } catch (err) {
      return null; // private mode / file:// restrictions
    }
  }

  function applyTheme(theme) {
    root.setAttribute('data-theme', theme);

    if (themeToggle) {
      // The label names the theme you are switching *to*.
      var next = theme === 'dark' ? 'light' : 'dark';
      themeToggle.setAttribute('aria-pressed', String(theme === 'light'));
      themeToggle.setAttribute('aria-label', 'Switch to ' + next + ' theme');
      if (themeLabel) themeLabel.textContent = next.charAt(0).toUpperCase() + next.slice(1);
    }

    if (themeMeta) {
      themeMeta.setAttribute('content', theme === 'light' ? '#FFFFFF' : '#12152E');
    }
  }

  applyTheme(readStoredTheme() || systemTheme());

  if (themeToggle) {
    themeToggle.addEventListener('click', function () {
      var next = root.getAttribute('data-theme') === 'dark' ? 'light' : 'dark';
      applyTheme(next);
      try { window.localStorage.setItem(THEME_KEY, next); } catch (err) { /* ignore */ }
    });
  }

  // Follow the OS only while the visitor has not made an explicit choice.
  var systemQuery = window.matchMedia('(prefers-color-scheme: light)');
  var onSystemChange = function (event) {
    if (!readStoredTheme()) applyTheme(event.matches ? 'light' : 'dark');
  };
  if (systemQuery.addEventListener) systemQuery.addEventListener('change', onSystemChange);
  else if (systemQuery.addListener) systemQuery.addListener(onSystemChange);

  /* ------------------------------------------------------------------ *
   * 2. Mobile navigation
   * ------------------------------------------------------------------ */
  var navToggle = document.getElementById('nav-toggle');
  var primaryNav = document.getElementById('primary-nav');
  var mobileQuery = window.matchMedia('(max-width: 860px)');

  function setNav(open) {
    document.body.setAttribute('data-nav', open ? 'open' : 'closed');
    if (navToggle) navToggle.setAttribute('aria-expanded', String(open));
  }

  setNav(false);

  if (navToggle) {
    navToggle.addEventListener('click', function () {
      setNav(document.body.getAttribute('data-nav') !== 'open');
    });
  }

  if (primaryNav) {
    primaryNav.addEventListener('click', function (event) {
      if (event.target.closest('a')) setNav(false);
    });
  }

  document.addEventListener('keydown', function (event) {
    if (event.key === 'Escape' && document.body.getAttribute('data-nav') === 'open') {
      setNav(false);
      if (navToggle) navToggle.focus();
    }
  });

  var onBreakpoint = function () {
    if (!mobileQuery.matches) setNav(false);
  };
  if (mobileQuery.addEventListener) mobileQuery.addEventListener('change', onBreakpoint);
  else if (mobileQuery.addListener) mobileQuery.addListener(onBreakpoint);

  /* ------------------------------------------------------------------ *
   * 3. Header elevation
   * ------------------------------------------------------------------ */
  var header = document.getElementById('site-header');
  var onScroll = function () {
    if (header) header.classList.toggle('is-scrolled', window.scrollY > 8);
  };
  onScroll();
  window.addEventListener('scroll', onScroll, { passive: true });

  /* ------------------------------------------------------------------ *
   * 4. Scroll spy
   * ------------------------------------------------------------------ */
  var navLinks = Array.prototype.slice.call(
    document.querySelectorAll('.primary-nav__list a[href^="#"]')
  );
  var sections = navLinks
    .map(function (link) { return document.getElementById(link.hash.slice(1)); })
    .filter(Boolean);

  function markActive(id) {
    navLinks.forEach(function (link) {
      var active = link.hash === '#' + id;
      link.classList.toggle('is-active', active);
      if (active) link.setAttribute('aria-current', 'true');
      else link.removeAttribute('aria-current');
    });
  }

  if (sections.length && 'IntersectionObserver' in window) {
    // Track every section's visible ratio and light up the one nearest the top.
    var ratios = new Map();

    var spy = new IntersectionObserver(function (entries) {
      entries.forEach(function (entry) {
        ratios.set(entry.target.id, entry.isIntersecting ? entry.intersectionRatio : 0);
      });

      var bestId = null;
      var bestRatio = 0;
      ratios.forEach(function (ratio, id) {
        if (ratio > bestRatio) { bestRatio = ratio; bestId = id; }
      });
      if (bestId) markActive(bestId);
    }, {
      rootMargin: '-25% 0px -60% 0px',
      threshold: [0, 0.15, 0.35, 0.6, 0.9]
    });

    sections.forEach(function (section) { spy.observe(section); });
  }

  /* ------------------------------------------------------------------ *
   * 5. Reveal on scroll
   * ------------------------------------------------------------------ */
  var revealables = document.querySelectorAll('.reveal');

  if (reduceMotion || !('IntersectionObserver' in window)) {
    Array.prototype.forEach.call(revealables, function (el) { el.classList.add('is-visible'); });
  } else {
    var revealer = new IntersectionObserver(function (entries, observer) {
      entries.forEach(function (entry) {
        if (!entry.isIntersecting) return;
        entry.target.classList.add('is-visible');
        observer.unobserve(entry.target);
      });
    }, { rootMargin: '0px 0px -12% 0px', threshold: 0.06 });

    Array.prototype.forEach.call(revealables, function (el) { revealer.observe(el); });
  }

  /* ------------------------------------------------------------------ *
   * 6. Segmented (radio) controls — icon surface + preview size
   * ------------------------------------------------------------------ */
  var iconGrid = document.getElementById('icon-grid');

  function initSegmented(group, onChange) {
    if (!group) return;
    var buttons = Array.prototype.slice.call(group.querySelectorAll('[role="radio"]'));
    if (!buttons.length) return;

    function select(button, focus) {
      buttons.forEach(function (candidate) {
        candidate.setAttribute('aria-checked', String(candidate === button));
      });
      if (focus) button.focus();
      onChange(button);
    }

    buttons.forEach(function (button) {
      button.addEventListener('click', function () { select(button, false); });
    });

    group.addEventListener('keydown', function (event) {
      var index = buttons.indexOf(document.activeElement);
      if (index === -1) return;

      var next = null;
      if (event.key === 'ArrowRight' || event.key === 'ArrowDown') next = (index + 1) % buttons.length;
      else if (event.key === 'ArrowLeft' || event.key === 'ArrowUp') next = (index - 1 + buttons.length) % buttons.length;
      else if (event.key === 'Home') next = 0;
      else if (event.key === 'End') next = buttons.length - 1;
      else return;

      event.preventDefault();
      select(buttons[next], true);
    });
  }

  var surfaceGroup = document.querySelector('[data-group="icon-surface"]');
  var sizeGroup = document.querySelector('[data-group="icon-size"]');

  if (iconGrid) {
    var syncSurface = function (value) { iconGrid.setAttribute('data-surface', value); };
    var syncSize = function (value) {
      iconGrid.setAttribute('data-size', value);
      iconGrid.style.setProperty('--preview', value + 'px');
    };

    // Apply the markup defaults so CSS and JS agree on first paint.
    syncSurface(iconGrid.getAttribute('data-surface') || 'dark');
    syncSize(iconGrid.getAttribute('data-size') || '48');

    initSegmented(surfaceGroup, function (button) {
      syncSurface(button.getAttribute('data-surface'));
    });
    initSegmented(sizeGroup, function (button) {
      syncSize(button.getAttribute('data-size'));
    });
  }

  /* ------------------------------------------------------------------ *
   * 7. Tabs (How it works)
   * ------------------------------------------------------------------ */
  var tabsRoot = document.getElementById('how-tabs');

  if (tabsRoot) {
    var tabs = Array.prototype.slice.call(tabsRoot.querySelectorAll('[role="tab"]'));

    function activateTab(tab, focus) {
      tabs.forEach(function (candidate) {
        var selected = candidate === tab;
        candidate.setAttribute('aria-selected', String(selected));
        candidate.tabIndex = selected ? 0 : -1;

        var panel = document.getElementById(candidate.getAttribute('aria-controls'));
        if (panel) panel.hidden = !selected;
      });
      if (focus) tab.focus();
    }

    tabs.forEach(function (tab) {
      tab.addEventListener('click', function () { activateTab(tab, false); });
    });

    tabsRoot.querySelector('[role="tablist"]').addEventListener('keydown', function (event) {
      var index = tabs.indexOf(document.activeElement);
      if (index === -1) return;

      var next = null;
      if (event.key === 'ArrowRight') next = (index + 1) % tabs.length;
      else if (event.key === 'ArrowLeft') next = (index - 1 + tabs.length) % tabs.length;
      else if (event.key === 'Home') next = 0;
      else if (event.key === 'End') next = tabs.length - 1;
      else return;

      event.preventDefault();
      activateTab(tabs[next], true);
    });
  }

  /* ------------------------------------------------------------------ *
   * 8. Copy a hex value to the clipboard
   * ------------------------------------------------------------------ */
  var toast = document.getElementById('toast');
  var toastTimer = null;

  function showToast(message) {
    if (!toast) return;
    toast.textContent = message;
    toast.classList.add('is-visible');
    window.clearTimeout(toastTimer);
    toastTimer = window.setTimeout(function () {
      toast.classList.remove('is-visible');
    }, 1900);
  }

  // navigator.clipboard is unavailable on file:// (not a secure context), so keep a
  // textarea + execCommand path as the fallback.
  function legacyCopy(text) {
    var field = document.createElement('textarea');
    field.value = text;
    field.setAttribute('readonly', '');
    field.style.position = 'fixed';
    field.style.top = '-1000px';
    document.body.appendChild(field);
    field.select();
    var ok = false;
    try { ok = document.execCommand('copy'); } catch (err) { ok = false; }
    document.body.removeChild(field);
    return ok;
  }

  function copy(text, successMessage) {
    if (navigator.clipboard && navigator.clipboard.writeText) {
      navigator.clipboard.writeText(text).then(
        function () { showToast(successMessage); },
        function () { showToast(legacyCopy(text) ? successMessage : text); }
      );
      return;
    }
    showToast(legacyCopy(text) ? successMessage : text);
  }

  Array.prototype.forEach.call(document.querySelectorAll('.swatch'), function (swatch) {
    swatch.addEventListener('click', function () {
      var hex = swatch.getAttribute('data-hex');
      copy(hex, hex + ' copied');
    });
  });

  /* ------------------------------------------------------------------ *
   * 9. Asset download buttons
   * ------------------------------------------------------------------ */
  Array.prototype.forEach.call(document.querySelectorAll('.asset__dl'), function (link) {
    link.addEventListener('click', function () {
      var name = link.getAttribute('href').split('/').pop();
      showToast('Downloading ' + name);
    });
  });
})();
