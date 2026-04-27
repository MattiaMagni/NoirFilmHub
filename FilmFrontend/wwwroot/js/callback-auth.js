(function () {
  function requireAuth(destinationUrl) {
    const target = destinationUrl || `${window.location.pathname || "/index.html"}${window.location.search || ""}${window.location.hash || ""}`;
    if (window.AuthService && window.AuthService.isAuthenticated()) {
      return true;
    }

    if (window.AuthGuard && typeof window.AuthGuard.requireAuth === "function") {
      window.AuthGuard.requireAuth(target);
      return false;
    }

    const loginUrl = window.AuthService && typeof window.AuthService.buildLoginUrl === "function"
      ? window.AuthService.buildLoginUrl(target)
      : `/login.html?callback=${encodeURIComponent(target)}`;

    window.location.replace(loginUrl);
    return false;
  }

  window.CallbackAuth = {
    requireAuth
  };
})();
