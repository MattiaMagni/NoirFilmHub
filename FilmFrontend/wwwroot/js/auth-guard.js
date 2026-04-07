(function () {
  function redirectToLogin() {
    if (window.AuthService) {
      window.AuthService.saveRedirect(window.location.pathname);
    }
    window.location.replace("/login.html");
  }

  function redirectToHome() {
    window.location.replace("/index.html");
  }

  function requireAuth() {
    if (!window.AuthService || !window.AuthService.isAuthenticated()) {
      redirectToLogin();
      return false;
    }
    return true;
  }

  function requireRole(allowedRoles) {
    if (!requireAuth()) {
      return false;
    }

    if (!window.AuthService.hasRole(allowedRoles)) {
      redirectToHome();
      return false;
    }

    return true;
  }

  async function redirectIfAuthenticated(targetPath) {
    if (!window.AuthService) {
      return false;
    }

    try {
      await window.AuthService.ensureValidAccessToken();
    } catch {
    }

    if (!window.AuthService.isAuthenticated()) {
      return false;
    }

    const fallback = targetPath || "/profile.html";
    const saved = window.AuthService.consumeRedirect();
    const lowerSaved = (saved || "").toLowerCase();
    const destination = saved && lowerSaved !== "/login.html" && lowerSaved !== "/register.html"
      ? saved
      : fallback;

    window.location.replace(destination);
    return true;
  }

  window.AuthGuard = {
    requireAuth,
    requireRole,
    redirectIfAuthenticated
  };
})();
