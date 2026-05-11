(function () {
  const ACCESS_TOKEN_KEY = "filmhub_access_token";
  const REFRESH_TOKEN_KEY = "filmhub_refresh_token";
  const USER_KEY = "filmhub_user";

  function normalizePath(path) {
    const value = String(path || "").trim();
    if (!value) return "/index.html";
    if (/^https?:\/\//i.test(value)) return "/index.html";
    return value.startsWith("/") ? value : `/${value}`;
  }

  function buildLoginUrl(destinationPath) {
    const destination = normalizePath(destinationPath || window.location.pathname + window.location.search + window.location.hash);
    return `/login.html?callback=${encodeURIComponent(destination)}`;
  }

  function getCallbackFromLocation() {
    try {
      const params = new URLSearchParams(window.location.search);
      const raw = params.get("callback");
      if (!raw) return null;
      return normalizePath(decodeURIComponent(raw));
    } catch { return null; }
  }

  function parseJwt(token) {
    try {
      const payload = token.split(".")[1];
      if (!payload) return null;
      const normalized = payload.replace(/-/g, "+").replace(/_/g, "/");
      const json = decodeURIComponent(atob(normalized).split("").map(c => "%" + ("00" + c.charCodeAt(0).toString(16)).slice(-2)).join(""));
      return JSON.parse(json);
    } catch { return null; }
  }

  function getAccessToken() { return sessionStorage.getItem(ACCESS_TOKEN_KEY) || localStorage.getItem(ACCESS_TOKEN_KEY); }
  function getRefreshToken() { return localStorage.getItem(REFRESH_TOKEN_KEY); }

  function setSession(loginResponse) {
    sessionStorage.setItem(ACCESS_TOKEN_KEY, loginResponse.accessToken);
    localStorage.setItem(REFRESH_TOKEN_KEY, loginResponse.refreshToken);
    localStorage.setItem(USER_KEY, JSON.stringify(loginResponse.utente || null));
  }

  function clearSession() {
    sessionStorage.removeItem(ACCESS_TOKEN_KEY);
    localStorage.removeItem(ACCESS_TOKEN_KEY);
    localStorage.removeItem(REFRESH_TOKEN_KEY);
    localStorage.removeItem(USER_KEY);
  }

  function tokenIsExpired(token) {
    const payload = parseJwt(token);
    if (!payload || !payload.exp) return true;
    return payload.exp <= Math.floor(Date.now() / 1000) + 10;
  }

  function getCurrentUser() {
    const raw = localStorage.getItem(USER_KEY);
    if (!raw) return null;
    try { return JSON.parse(raw); } catch { return null; }
  }

  function isAuthenticated() {
    const token = getAccessToken();
    return !!token && !tokenIsExpired(token);
  }

  function getCurrentRole() {
    const user = getCurrentUser();
    if (user && user.ruolo) return String(user.ruolo).toLowerCase();
    const token = getAccessToken();
    if (!token) return null;
    const payload = parseJwt(token);
    if (!payload) return null;
    return String(payload.role || payload.ruolo || "").toLowerCase() || null;
  }

  function hasRole(allowedRoles) {
    const role = getCurrentRole();
    if (!role) return false;
    const list = Array.isArray(allowedRoles) ? allowedRoles : [allowedRoles];
    return list.map(x => String(x).toLowerCase()).includes(role);
  }

  async function register(userData) { return window.ApiClientRaw.post("/auth/register", userData); }

  async function login(email, password) {
    const result = await window.ApiClientRaw.post("/auth/login", { email, password });
    setSession(result);
    return result;
  }

  async function refreshAccessToken() {
    const refreshToken = getRefreshToken();
    if (!refreshToken) { clearSession(); throw new Error("Sessione scaduta"); }
    const result = await window.ApiClientRaw.post("/auth/refresh", { refreshToken });
    setSession(result);
    return result.accessToken;
  }

  async function ensureValidAccessToken() {
    const token = getAccessToken();
    if (!token) return null;
    if (!tokenIsExpired(token)) return token;
    try { return await refreshAccessToken(); }
    catch { clearSession(); return null; }
  }

  async function logout(allDevices) {
    try {
      const token = getAccessToken();
      if (token) {
        await window.ApiClientRaw.post("/auth/logout", { allDevices: !!allDevices }, { Authorization: `Bearer ${token}` });
      }
    } catch {} finally { clearSession(); }
  }

  // --- Social Login ---
  async function initiateSocialLogin(provider, mode) {
    const callback = new URLSearchParams(window.location.search).get("callback") || "";
    const url = `/auth/external/${provider}?mode=${mode || "login"}&returnUrl=${encodeURIComponent(callback || "/index.html")}`;
    const result = await window.ApiClientRaw.get(url);
    if (result && result.authorizationUrl) {
      window.location.href = result.authorizationUrl;
    }
  }

  // --- Password Management ---
  async function changePassword(currentPassword, newPassword) {
    const result = await window.ApiClientRaw.post("/auth/me/change-password", { currentPassword, newPassword });
    setSession(result);
    return result;
  }

  async function forgotPassword(email) {
    return window.ApiClientRaw.post("/auth/forgot-password", { email });
  }

  async function resetPassword(email, token, newPassword) {
    const result = await window.ApiClientRaw.post("/auth/reset-password", { email, token, newPassword });
    if (result.accessToken) setSession(result);
    return result;
  }

  async function setupPassword(email, token, newPassword) {
    const result = await window.ApiClientRaw.post("/auth/setup-password", { email, token, newPassword });
    if (result.accessToken) setSession(result);
    return result;
  }

  async function requestPasswordSetup() {
    return window.ApiClient.post("/auth/me/request-password-setup", {});
  }

  // --- Session Management ---
  async function revokeAllSessions() {
    await window.ApiClient.post("/auth/revoke-all-sessions", {});
    clearSession();
    window.location.replace("/login.html");
  }

  // --- External Logins ---
  async function getExternalLogins() {
    return window.ApiClient.get("/auth/me/external-logins");
  }

  async function unlinkExternalLogin(loginId) {
    return window.ApiClient.delete(`/auth/me/external-logins/${loginId}`);
  }

  // --- Admin ---
  async function searchUsers(params) {
    const query = new URLSearchParams();
    if (params.search) query.set("search", params.search);
    if (params.ruolo) query.set("ruolo", params.ruolo);
    if (params.isDisabled !== undefined) query.set("isDisabled", params.isDisabled);
    if (params.hasLocalCredentials !== undefined) query.set("hasLocalCredentials", params.hasLocalCredentials);
    query.set("page", params.page || 1);
    query.set("pageSize", params.pageSize || 20);
    if (params.orderBy) query.set("orderBy", params.orderBy);
    if (params.orderDirection) query.set("orderDirection", params.orderDirection);
    return window.ApiClient.get(`/auth/admin/utenti?${query}`);
  }

  async function getUserDetail(userId) {
    return window.ApiClient.get(`/auth/admin/utenti/${userId}`);
  }

  async function changeUserRole(userId, ruolo) {
    return window.ApiClient.put(`/auth/admin/utenti/${userId}/ruolo`, { ruolo });
  }

  async function disableUser(userId) {
    return window.ApiClient.put(`/auth/admin/utenti/${userId}/disable`, {});
  }

  async function enableUser(userId) {
    return window.ApiClient.put(`/auth/admin/utenti/${userId}/enable`, {});
  }

  async function forcePasswordReset(userId) {
    return window.ApiClient.post(`/auth/admin/utenti/${userId}/force-password-reset`, {});
  }

  async function deleteUser(userId) {
    return window.ApiClient.delete(`/auth/admin/utenti/${userId}`);
  }

  async function inviteUser(email, ruolo, nome, cognome) {
    return window.ApiClient.post("/auth/admin/invite", { email, ruolo, nome, cognome, sendSetupEmail: true });
  }

  window.AuthService = {
    login, register, logout, refreshAccessToken, ensureValidAccessToken,
    getAccessToken, getRefreshToken, getCurrentUser, getCurrentRole,
    isAuthenticated, hasRole, clearSession, setSession,
    buildLoginUrl, getCallbackFromLocation,
    initiateSocialLogin,
    changePassword, forgotPassword, resetPassword, setupPassword, requestPasswordSetup,
    revokeAllSessions,
    getExternalLogins, unlinkExternalLogin,
    searchUsers, getUserDetail, changeUserRole, disableUser, enableUser,
    forcePasswordReset, deleteUser, inviteUser,
    saveRedirect(path) {
      sessionStorage.setItem("auth_redirect_after_login", normalizePath(path || window.location.pathname));
    },
    consumeRedirect() {
      const value = sessionStorage.getItem("auth_redirect_after_login");
      if (value) sessionStorage.removeItem("auth_redirect_after_login");
      return value ? normalizePath(value) : null;
    }
  };
})();
