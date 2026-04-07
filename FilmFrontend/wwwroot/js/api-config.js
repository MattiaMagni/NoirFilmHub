(function () {
  const host = window.location.hostname || "localhost";
  const overrideGlobal = window.__API_BASE_URL__;
  const overrideStorage = localStorage.getItem("API_BASE_URL_OVERRIDE");

  const fallback = `http://${host}:5000`;
  const apiBaseUrl = overrideGlobal || overrideStorage || fallback;

  window.AppConfig = {
    API_BASE_URL: apiBaseUrl
  };
})();
