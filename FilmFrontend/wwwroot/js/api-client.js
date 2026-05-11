(function () {
  const baseUrl = (window.AppConfig && window.AppConfig.API_BASE_URL) || "";

  async function parseResponse(response) {
    const contentType = response.headers.get("content-type") || "";
    if (contentType.includes("application/json")) return await response.json();
    if (response.status === 204) return null;
    return await response.text();
  }

  function shouldIncludeBody(method, data) {
    return method !== "GET" && method !== "DELETE" && data !== undefined;
  }

  function createOptions(method, data, headers) {
    const options = { method, headers: { ...(headers || {}) } };
    if (shouldIncludeBody(method, data)) {
      if (!options.headers["Content-Type"]) options.headers["Content-Type"] = "application/json";
      options.body = JSON.stringify(data ?? {});
    }
    return options;
  }

  async function rawRequest(path, options) {
    const response = await fetch(baseUrl + path, options);
    if (response.status === 429) {
      const retryAfter = parseInt(response.headers.get("Retry-After") || "60", 10);
      throw { status: 429, message: `Troppi tentativi. Riprova tra ${retryAfter} secondi.`, retryAfter };
    }
    const payload = await parseResponse(response);
    if (!response.ok) {
      throw {
        status: response.status,
        message: (payload && (payload.error || payload.message)) || "Errore durante la chiamata API",
        details: payload
      };
    }
    return payload;
  }

  async function request(path, options, retryOn401) {
    const opt = options || {};
    const method = opt.method || "GET";
    const headers = { ...(opt.headers || {}) };
    if (opt.body !== undefined && !headers["Content-Type"]) headers["Content-Type"] = "application/json";

    if (window.AuthService) {
      const token = await window.AuthService.ensureValidAccessToken();
      if (token && !headers.Authorization) headers.Authorization = `Bearer ${token}`;
    }

    try {
      return await rawRequest(path, { ...opt, method, headers });
    } catch (error) {
      if (error && error.status === 401 && retryOn401 !== false && window.AuthService) {
        try {
          const token = await window.AuthService.refreshAccessToken();
          const retryHeaders = { ...headers, Authorization: `Bearer ${token}` };
          return await rawRequest(path, { ...opt, method, headers: retryHeaders });
        } catch {
          window.AuthService.clearSession();
          const currentPath = window.location.pathname.toLowerCase();
          if (currentPath !== "/login.html" && currentPath !== "/register.html") {
            window.AuthService.saveRedirect(window.location.pathname);
            window.location.replace("/login.html");
          }
          throw error;
        }
      }
      if (error && error.status === 403) {
        const currentPath = window.location.pathname.toLowerCase();
        if (currentPath !== "/index.html") window.location.replace("/index.html");
      }
      throw error;
    }
  }

  window.ApiClientRaw = {
    get: (path, headers) => request(path, createOptions("GET", undefined, headers), false),
    post: (path, data, headers) => request(path, createOptions("POST", data, headers), false),
    put: (path, data, headers) => request(path, createOptions("PUT", data, headers), false),
    delete: (path, headers) => request(path, createOptions("DELETE", undefined, headers), false)
  };

  window.ApiClient = {
    get: (path) => request(path, createOptions("GET")),
    post: (path, data) => request(path, createOptions("POST", data)),
    put: (path, data) => request(path, createOptions("PUT", data)),
    delete: (path) => request(path, createOptions("DELETE"))
  };
})();
