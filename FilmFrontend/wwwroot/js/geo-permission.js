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
      if (existing) {
        existing.remove();
      }

      var overlay = document.createElement("div");
      overlay.id = "geo-permission-overlay";
      overlay.className = "geo-popup-overlay";
      overlay.innerHTML =
        '<div class="geo-popup-card">' +
          '<p class="geo-popup-icon">&#x1f4cd;</p>' +
          '<h3>Vuoi vedere i cinema piu vicini a te?</h3>' +
          '<p class="subtle">Attiva la tua posizione per ordinare i cinema per distanza e scoprire quelli nella tua zona.</p>' +
          '<div class="geo-popup-actions">' +
            '<button class="button primary" id="geo-popup-yes">Attiva posizione</button>' +
            '<button class="button secondary" id="geo-popup-no">No, grazie</button>' +
          '</div>' +
        '</div>';

      document.body.appendChild(overlay);

      overlay.querySelector("#geo-popup-yes").onclick = function () {
        var card = overlay.querySelector(".geo-popup-card");
        if (card) card.innerHTML =
          '<p class="geo-popup-icon">&#x1f4cd;</p>' +
          '<h3>Recupero della posizione in corso...</h3>' +
          '<p class="subtle">Attendi il prompt del browser per autorizzare la geolocalizzazione.</p>';
        sessionStorage.setItem("geo_popup_dismissed", "1");
        setTimeout(function () { overlay.remove(); }, 600);
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
    return await requestGeolocation();
  }

  window.GeoPermission = {
    requestGeoWithPopup: requestGeoWithPopup,
    requestGeolocation: requestGeolocation,
    showGeoPopup: showGeoPopup
  };
})();
