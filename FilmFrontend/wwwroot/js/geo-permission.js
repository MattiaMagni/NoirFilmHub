(function () {
  function requestGeolocation() {
    if (!navigator.geolocation) return Promise.resolve(null);
    return new Promise((resolve) => {
      navigator.geolocation.getCurrentPosition(
        function (pos) { resolve(pos.coords); },
        function () { resolve(null); },
        { enableHighAccuracy: false, timeout: 8000, maximumAge: 300000 }
      );
    }).catch(function () { return null; });
  }

  function getCachedCoords() {
    try {
      var raw = sessionStorage.getItem("geo_coords_cache");
      if (!raw) return null;
      var c = JSON.parse(raw);
      if (!c.lat || !c.lng) return null;
      return { latitude: c.lat, longitude: c.lng };
    } catch { return null; }
  }

  function setCachedCoords(coords) {
    if (!coords || !coords.latitude) return;
    try {
      sessionStorage.setItem("geo_coords_cache", JSON.stringify({ lat: coords.latitude, lng: coords.longitude, ts: Date.now() }));
    } catch {}
  }

  function showGeoPopup() {
    return new Promise((resolve) => {
      var existing = document.getElementById("geo-permission-overlay");
      if (existing) existing.remove();

      var overlay = document.createElement("div");
      overlay.id = "geo-permission-overlay";
      overlay.className = "geo-popup-overlay";
      overlay.innerHTML =
        '<div class="geo-popup-card">' +
          '<p class="geo-popup-icon">&#x1f4cd;</p>' +
          '<h3>Vuoi vedere i cinema piu vicini a te?</h3>' +
          '<p class="subtle">Attiva la tua posizione per ordinare i cinema per distanza.</p>' +
          '<div class="geo-popup-actions">' +
            '<button class="button primary" id="geo-popup-yes">Attiva posizione</button>' +
            '<button class="button secondary" id="geo-popup-no">No, grazie</button>' +
          '</div>' +
        '</div>';

      document.body.appendChild(overlay);

      overlay.querySelector("#geo-popup-yes").onclick = function () {
        overlay.remove();
        localStorage.removeItem("geo_enabled");
        sessionStorage.setItem("geo_popup_accepted", "1");
        resolve(true);
      };

      overlay.querySelector("#geo-popup-no").onclick = function () {
        overlay.remove();
        sessionStorage.setItem("geo_popup_dismissed", "1");
        resolve(false);
      };

      overlay.onclick = function (e) {
        if (e.target === overlay) {
          overlay.remove();
          sessionStorage.setItem("geo_popup_dismissed", "1");
          resolve(false);
        }
      };
    });
  }

  function isGeoDisabled() {
    return localStorage.getItem("geo_enabled") === "0";
  }

  async function requestGeoWithPopup() {
    if (sessionStorage.getItem("geo_popup_accepted") === "1") {
      if (isGeoDisabled()) return null;
      var cached = getCachedCoords();
      if (cached) {
        requestGeolocation().then(function (fresh) {
          if (fresh) setCachedCoords(fresh);
        });
        return cached;
      }
      var coords = await requestGeolocation();
      if (coords) setCachedCoords(coords);
      return coords;
    }

    if (sessionStorage.getItem("geo_popup_dismissed") === "1") return null;

    var granted = await showGeoPopup();
    if (!granted) return null;

    if (isGeoDisabled()) return null;

    var coords = await requestGeolocation();
    if (coords) setCachedCoords(coords);
    return coords;
  }

  window.GeoPermission = {
    requestGeoWithPopup: requestGeoWithPopup,
    requestGeolocation: requestGeolocation,
    showGeoPopup: showGeoPopup
  };
})();
