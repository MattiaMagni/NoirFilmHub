(function () {
  async function requestGeolocation() {
    if (!navigator.geolocation) return null;
    try {
      return await new Promise((resolve) => {
        navigator.geolocation.getCurrentPosition(
          (pos) => resolve(pos.coords),
          () => resolve(null),
          { enableHighAccuracy: false, timeout: 8000, maximumAge: 120000 }
        );
      });
    } catch {
      return null;
    }
  }

  function showGeoPopup() {
    return new Promise((resolve) => {
      if (localStorage.getItem("geo_enabled") === "0") {
        resolve(false);
        return;
      }

      if (sessionStorage.getItem("geo_popup_dismissed") === "1") {
        resolve(false);
        return;
      }

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

  async function requestGeoWithPopup() {
    var granted = await showGeoPopup();
    if (!granted) return null;
    var coords = await requestGeolocation();
    if (coords) {
      sessionStorage.setItem("geo_popup_dismissed", "1");
    }
    return coords;
  }

  window.GeoPermission = {
    requestGeoWithPopup: requestGeoWithPopup,
    requestGeolocation: requestGeolocation,
    showGeoPopup: showGeoPopup
  };
})();
