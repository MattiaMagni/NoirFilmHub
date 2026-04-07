(function () {
  const baseUrl = (window.AppConfig && window.AppConfig.API_BASE_URL) || "";

  async function parseResponse(response) {
    const contentType = response.headers.get("content-type") || "";
    if (contentType.includes("application/json")) {
      return await response.json();
    }
    if (response.status === 204) {
      return null;
    }
    return await response.text();
  }

  async function rawRequest(path, options) {
    const response = await fetch(baseUrl + path, options);
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
    const headers = {
      "Content-Type": "application/json",
      ...(opt.headers || {})
    };

    if (window.AuthService) {
      const token = await window.AuthService.ensureValidAccessToken();
      if (token && !headers.Authorization) {
        headers.Authorization = `Bearer ${token}`;
      }
    }

    try {
      return await rawRequest(path, {
        ...opt,
        method,
        headers
      });
    } catch (error) {
      if (error && error.status === 401 && retryOn401 !== false && window.AuthService) {
        try {
          const token = await window.AuthService.refreshAccessToken();
          const retryHeaders = {
            ...headers,
            Authorization: `Bearer ${token}`
          };

          return await rawRequest(path, {
            ...opt,
            method,
            headers: retryHeaders
          });
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
        if (currentPath !== "/index.html") {
          window.location.replace("/index.html");
        }
      }

      throw error;
    }
  }

  window.ApiClientRaw = {
    get: (path, headers) => request(path, { method: "GET", headers }, false),
    post: (path, data, headers) => request(path, { method: "POST", body: JSON.stringify(data), headers }, false),
    put: (path, data, headers) => request(path, { method: "PUT", body: JSON.stringify(data), headers }, false),
    delete: (path, headers) => request(path, { method: "DELETE", headers }, false)
  };

  window.ApiClient = {
    get: (path) => request(path, { method: "GET" }),
    post: (path, data) => request(path, { method: "POST", body: JSON.stringify(data) }),
    put: (path, data) => request(path, { method: "PUT", body: JSON.stringify(data) }),
    delete: (path) => request(path, { method: "DELETE" })
  };
})();
