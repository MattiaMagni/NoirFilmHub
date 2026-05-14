(function () {
  const STORAGE_KEY = "filmhub_theme";
  const DARK = "dark";
  const LIGHT = "light";
  const TRANSITION_CLASS = "theme-transition";
  let bound = false;

  function normalizeTheme(value) {
    const v = String(value || "").toLowerCase();
    return v === LIGHT ? LIGHT : DARK;
  }

  function getStoredTheme() {
    try {
      return normalizeTheme(localStorage.getItem(STORAGE_KEY));
    } catch {
      return DARK;
    }
  }

  function saveTheme(theme) {
    try {
      localStorage.setItem(STORAGE_KEY, normalizeTheme(theme));
    } catch {
    }
  }

  function getCurrentTheme() {
    return normalizeTheme(document.documentElement.getAttribute("data-theme") || getStoredTheme());
  }

  function withTransition() {
    document.documentElement.classList.add(TRANSITION_CLASS);
    if (document.body) {
      document.body.classList.add("theme-switching");
    }
    window.setTimeout(() => {
      document.documentElement.classList.remove(TRANSITION_CLASS);
      if (document.body) {
        document.body.classList.remove("theme-switching");
      }
    }, 360);
  }

  function applyTheme(theme, animate) {
    const normalized = normalizeTheme(theme);
    if (animate) {
      withTransition();
    }
    document.documentElement.setAttribute("data-theme", normalized);
    saveTheme(normalized);
    updateThemeButton(normalized);
  }

  function toggleTheme() {
    const current = getCurrentTheme();
    applyTheme(current === DARK ? LIGHT : DARK, true);
  }

  function updateThemeButton(theme) {
    const isDark = normalizeTheme(theme) === DARK;
    const label = isDark ? "Passa al tema chiaro" : "Passa al tema scuro";
    document.querySelectorAll("#theme-toggle").forEach((button) => {
      button.innerHTML = isDark
        ? '<svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><circle cx="12" cy="12" r="5"/><line x1="12" y1="1" x2="12" y2="3"/><line x1="12" y1="21" x2="12" y2="23"/><line x1="4.22" y1="4.22" x2="5.64" y2="5.64"/><line x1="18.36" y1="18.36" x2="19.78" y2="19.78"/><line x1="1" y1="12" x2="3" y2="12"/><line x1="21" y1="12" x2="23" y2="12"/><line x1="4.22" y1="19.78" x2="5.64" y2="18.36"/><line x1="18.36" y1="5.64" x2="19.78" y2="4.22"/></svg>'
        : '<svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M21 12.79A9 9 0 1 1 11.21 3 7 7 0 0 0 21 12.79z"/></svg>';
      button.setAttribute("aria-label", label);
    });
  }

  function initThemeToggle() {
    if (!bound) {
      document.addEventListener("click", (event) => {
        const button = event.target.closest("#theme-toggle");
        if (!button) {
          return;
        }
        event.preventDefault();
        toggleTheme();
      });
      bound = true;
    }
    updateThemeButton(getCurrentTheme());
  }

  function bootstrapTheme() {
    applyTheme(getStoredTheme());
    if (document.readyState === "loading") {
      document.addEventListener("DOMContentLoaded", () => {
        initThemeToggle();
      }, { once: true });
      return;
    }

    initThemeToggle();
  }

  window.ThemeService = {
    applyTheme,
    toggleTheme,
    initThemeToggle,
    bootstrapTheme
  };

  bootstrapTheme();
})();
