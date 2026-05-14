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
    const icon = isDark ? "☀️" : "🌙";
    const label = isDark ? "Passa al tema chiaro" : "Passa al tema scuro";
    document.querySelectorAll("#theme-toggle").forEach((button) => {
      button.textContent = icon;
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
