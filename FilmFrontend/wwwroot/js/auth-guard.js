(function () {
  const guestPages = ["/login.html", "/register.html"];

  function currentDestination() {
    return `${window.location.pathname || "/index.html"}${window.location.search || ""}${window.location.hash || ""}`;
  }

  function redirectToLogin(destinationUrl) {
    if (window.AuthService && typeof window.AuthService.buildLoginUrl === "function") {
      window.location.replace(window.AuthService.buildLoginUrl(destinationUrl || currentDestination()));
      return;
    }
    if (window.AuthService) window.AuthService.saveRedirect(destinationUrl || currentDestination());
    window.location.replace("/login.html");
  }

  function redirectToHome() { window.location.replace("/index.html"); }

  function requireAuth(destinationUrl) {
    if (!window.AuthService || !window.AuthService.isAuthenticated()) {
      redirectToLogin(destinationUrl);
      return false;
    }
    return true;
  }

  function requireRole(allowedRoles, destinationUrl) {
    if (!requireAuth(destinationUrl)) return false;
    if (!window.AuthService.hasRole(allowedRoles)) { redirectToHome(); return false; }
    return true;
  }

  function requireAdmin(destinationUrl) {
    return requireRole(["admin"], destinationUrl);
  }

  function requireNotDisabled(destinationUrl) {
    if (!requireAuth(destinationUrl)) return false;
    const user = window.AuthService.getCurrentUser();
    if (user && user.isDisabled) { redirectToHome(); return false; }
    return true;
  }

  async function redirectIfAuthenticated(targetPath) {
    if (!window.AuthService) return false;
    try { await window.AuthService.ensureValidAccessToken(); } catch {}
    if (!window.AuthService.isAuthenticated()) return false;
    const fallback = targetPath || "/profile.html";
    const callback = window.AuthService.getCallbackFromLocation ? window.AuthService.getCallbackFromLocation() : null;
    const saved = callback || window.AuthService.consumeRedirect();
    const lowerSaved = (saved || "").toLowerCase();
    const destination = saved && !guestPages.includes(lowerSaved) ? saved : fallback;
    window.location.replace(destination);
    return true;
  }

  window.AuthGuard = {
    requireAuth, requireRole, requireAdmin, requireNotDisabled, redirectIfAuthenticated
  };
})();
