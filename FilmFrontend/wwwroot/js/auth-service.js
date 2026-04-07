(function () {
  const ACCESS_TOKEN_KEY = "filmhub_access_token";
  const REFRESH_TOKEN_KEY = "filmhub_refresh_token";
  const USER_KEY = "filmhub_user";

  function normalizePath(path) {
    const value = String(path || "").trim();
    if (!value) {
      return "/index.html";
    }
    if (/^https?:\/\//i.test(value)) {
      return "/index.html";
    }
    return value.startsWith("/") ? value : `/${value}`;
  }

  function parseJwt(token) {
    try {
      const payload = token.split(".")[1];
      if (!payload) {
        return null;
      }
      const normalized = payload.replace(/-/g, "+").replace(/_/g, "/");
      const json = decodeURIComponent(
        atob(normalized)
          .split("")
          .map((c) => "%" + ("00" + c.charCodeAt(0).toString(16)).slice(-2))
          .join("")
      );
      return JSON.parse(json);
    } catch {
      return null;
    }
  }

  function getAccessToken() {
    return localStorage.getItem(ACCESS_TOKEN_KEY);
  }

  function getRefreshToken() {
    return localStorage.getItem(REFRESH_TOKEN_KEY);
  }

  function setSession(loginResponse) {
    localStorage.setItem(ACCESS_TOKEN_KEY, loginResponse.accessToken);
    localStorage.setItem(REFRESH_TOKEN_KEY, loginResponse.refreshToken);
    localStorage.setItem(USER_KEY, JSON.stringify(loginResponse.utente || null));
  }

  function clearSession() {
    localStorage.removeItem(ACCESS_TOKEN_KEY);
    localStorage.removeItem(REFRESH_TOKEN_KEY);
    localStorage.removeItem(USER_KEY);
  }

  function tokenIsExpired(token) {
    const payload = parseJwt(token);
    if (!payload || !payload.exp) {
      return true;
    }
    const now = Math.floor(Date.now() / 1000);
    return payload.exp <= now + 10;
  }

  function getCurrentUser() {
    const raw = localStorage.getItem(USER_KEY);
    if (!raw) {
      return null;
    }
    try {
      return JSON.parse(raw);
    } catch {
      return null;
    }
  }

  function isAuthenticated() {
    const token = getAccessToken();
    return !!token && !tokenIsExpired(token);
  }

  function getCurrentRole() {
    const user = getCurrentUser();
    if (user && user.ruolo) {
      return String(user.ruolo).toLowerCase();
    }

    const token = getAccessToken();
    if (!token) {
      return null;
    }
    const payload = parseJwt(token);
    if (!payload) {
      return null;
    }
    return String(payload.role || payload.ruolo || payload["http://schemas.microsoft.com/ws/2008/06/identity/claims/role"] || "").toLowerCase() || null;
  }

  function hasRole(allowedRoles) {
    const role = getCurrentRole();
    if (!role) {
      return false;
    }
    const list = Array.isArray(allowedRoles) ? allowedRoles : [allowedRoles];
    return list.map((x) => String(x).toLowerCase()).includes(role);
  }

  async function register(userData) {
    return window.ApiClientRaw.post("/auth/register", userData);
  }

  async function login(email, password) {
    const result = await window.ApiClientRaw.post("/auth/login", { email, password });
    setSession(result);
    return result;
  }

  async function refreshAccessToken() {
    const refreshToken = getRefreshToken();
    if (!refreshToken) {
      clearSession();
      throw new Error("Sessione scaduta");
    }

    const result = await window.ApiClientRaw.post("/auth/refresh", { refreshToken });
    setSession(result);
    return result.accessToken;
  }

  async function ensureValidAccessToken() {
    const token = getAccessToken();
    if (!token) {
      return null;
    }

    if (!tokenIsExpired(token)) {
      return token;
    }

    try {
      return await refreshAccessToken();
    } catch {
      clearSession();
      return null;
    }
  }

  async function logout() {
    try {
      const token = getAccessToken();
      if (token) {
        await window.ApiClientRaw.post(
          "/auth/logout",
          {},
          {
            Authorization: `Bearer ${token}`
          }
        );
      }
    } catch {
    } finally {
      clearSession();
    }
  }

  window.AuthService = {
    login,
    register,
    logout,
    refreshAccessToken,
    ensureValidAccessToken,
    getAccessToken,
    getRefreshToken,
    getCurrentUser,
    getCurrentRole,
    isAuthenticated,
    hasRole,
    clearSession,
    saveRedirect(path) {
      sessionStorage.setItem("auth_redirect_after_login", normalizePath(path || window.location.pathname));
    },
    consumeRedirect() {
      const value = sessionStorage.getItem("auth_redirect_after_login");
      if (value) {
        sessionStorage.removeItem("auth_redirect_after_login");
      }
      return normalizePath(value);
    }
  };
})();
