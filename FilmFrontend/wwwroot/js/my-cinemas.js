(function () {
  const statusEl = document.getElementById("my-cinemas-status");
  const listEl = document.getElementById("my-cinemas-list");
  const detailShellEl = document.getElementById("my-cinemas-detail-shell");
  const detailTitleEl = document.getElementById("my-cinemas-detail-title");
  const dateStripEl = document.getElementById("my-cinemas-date-strip");
  const dayBodyEl = document.getElementById("my-cinemas-day-body");
  const filtersEl = document.getElementById("my-cinemas-filters");
  const filterCityEl = document.getElementById("cinema-filter-city");
  const filterTipologiaEl = document.getElementById("cinema-filter-tipologia");
  const filterDistanceRowEl = document.getElementById("cinema-filter-distance-row");
  const filterRadiusEl = document.getElementById("cinema-filter-radius");
  const filterRadiusValueEl = document.getElementById("cinema-filter-radius-value");

  let selectedCinemaId = null;
  let selectedDayIso = null;
  let availableDays = [];

  let geoPosition = null;
  let cityDebounce = null;
  let radiusDebounce = null;

  function setStatus(message, kind) {
    statusEl.className = `status ${kind}`;
    statusEl.textContent = message;
  }

  function getCinemaIdFromQuery() {
    const params = new URLSearchParams(window.location.search);
    const raw = Number(params.get("idCinema") || params.get("IdCinema"));
    return raw > 0 ? raw : null;
  }

  function bookingUrl(show, filmId) {
    return `/acquista.html?idCinema=${selectedCinemaId}&idFilm=${filmId}&idSala=${show.salaId}&idShow=${show.proiezioneId}`;
  }

  function renderCinemaCard(cinema) {
    const tipologie = Array.isArray(cinema.tipologieSala) && cinema.tipologieSala.length
      ? cinema.tipologieSala.map((t) => `<span class='tag info'>${t}</span>`).join(" ")
      : "<span class='tag info'>Tipologia non definita</span>";

    const distanceBadge = cinema.distanzaKm != null
      ? `<span class='tag accent'>${Number(cinema.distanzaKm).toFixed(1)} km</span>`
      : "";

    return `
      <article class="card cinema-card">
        <div class="card-body">
          <h3>${cinema.nome}${distanceBadge ? ` <span class="cinema-distance-badge">${distanceBadge}</span>` : ""}</h3>
          <p class="subtle">${cinema.citta} - ${cinema.indirizzo}</p>
          <div class="actions">${tipologie}${distanceBadge}</div>
          <p><a class="button secondary" href="/my-cinemas.html?idCinema=${cinema.id}">Apri programmazione</a></p>
        </div>
      </article>
    `;
  }

  function buildQueryString() {
    const params = new URLSearchParams();

    const city = (filterCityEl && filterCityEl.value || "").trim();
    if (city) {
      params.set("citta", city);
    }

    if (filterTipologiaEl && filterTipologiaEl.value) {
      params.set("tipologiaSala", filterTipologiaEl.value);
    }

    if (geoPosition && geoPosition.latitude != null && geoPosition.longitude != null) {
      params.set("lat", String(geoPosition.latitude));
      params.set("lng", String(geoPosition.longitude));

      if (filterRadiusEl) {
        const raggio = Number(filterRadiusEl.value);
        if (raggio > 0 && raggio < 200) {
          params.set("raggio", String(raggio));
        }
      }
    }

    return params.toString();
  }

  async function loadCinemaList() {
    setStatus("Caricamento cinema...", "info");
    try {
      const query = buildQueryString();
      const cinemas = await window.ApiClient.get(`/my-cinemas${query ? '?' + query : ''}`);
      const items = Array.isArray(cinemas) ? cinemas : [];
      if (!items.length) {
        listEl.innerHTML = "<p class='subtle'>Nessun cinema disponibile con i filtri selezionati.</p>";
        setStatus("Nessun cinema trovato.", "info");
        return;
      }
      listEl.innerHTML = items.map(renderCinemaCard).join("");
      const geoText = geoPosition ? " (ordinati per distanza)" : "";
      setStatus(`Caricati ${items.length} cinema${geoText}.`, "success");
    } catch (error) {
      listEl.innerHTML = "";
      setStatus(`Errore caricamento cinema: ${error.message}`, "error");
    }
  }

  async function loadTipologie() {
    if (!filterTipologiaEl) {
      return;
    }
    try {
      const tipologie = await window.ApiClient.get("/my-cinemas/tipologie");
      const list = Array.isArray(tipologie) ? tipologie : [];
      const options = ["<option value=''>Tutte le tipologie</option>"];
      list.forEach((t) => {
        options.push(`<option value="${t}">${t}</option>`);
      });
      filterTipologiaEl.innerHTML = options.join("");
    } catch {
      filterTipologiaEl.innerHTML = "<option value=''>Tutte le tipologie</option>";
    }
  }

  function renderDateStrip() {
    const days = availableDays
      .map((iso) => ({ iso, label: window.DateUtils.formatDatePill(iso) }))
      .filter((day) => day.iso);

    if (!days.length) {
      dateStripEl.innerHTML = "";
      selectedDayIso = null;
      return;
    }

    if (!selectedDayIso || !days.some((day) => day.iso === selectedDayIso)) {
      selectedDayIso = days[0].iso;
    }

    dateStripEl.innerHTML = days
      .map((day) => {
        const active = selectedDayIso === day.iso;
        return `<button class="btn-small secondary ${active ? "active" : ""}" data-day="${day.iso}">${day.label}</button>`;
      })
      .join("");
  }

  function buildFilmBlock(item) {
    const tipologie = Array.isArray(item.tipologie) ? item.tipologie : [];
    const tipologieHtml = tipologie.map((tipologia) => {
      const buttons = (tipologia.orari || [])
        .map((show) => `<button class="btn-small primary cinema-showtime" data-film-id="${item.filmId}" data-url="${bookingUrl(show, item.filmId)}">${show.ora}</button>`)
        .join("");

      return `
        <div class="panel">
          <h4>${tipologia.tipologiaSala}</h4>
          <div class="actions">${buttons}</div>
        </div>
      `;
    }).join("");

    return `
      <article class="panel my-cinemas-film-row">
        <div class="my-cinemas-film-grid">
          <div class="my-cinemas-film-media">
            ${item.copertinaPath ? `<img src="${item.copertinaPath}" alt="Copertina ${item.titolo}">` : "<div class='card-media'><span>Copertina non disponibile</span></div>"}
          </div>
          <div>
            <h3>${item.titolo}</h3>
            <p class="subtle">${(item.descrizioneLunga || "Descrizione non disponibile").slice(0, 240)}</p>
            ${tipologieHtml}
          </div>
        </div>
      </article>
    `;
  }

  async function loadCinemaProgrammazione() {
    if (!selectedCinemaId) {
      return;
    }

    setStatus("Caricamento programmazione cinema...", "info");
    try {
      const params = new URLSearchParams();
      if (selectedDayIso) {
        params.set("day", selectedDayIso);
      }
      const detail = await window.ApiClient.get(
        `/my-cinemas/${selectedCinemaId}/programmazione${params.toString() ? '?' + params : ''}`
      );
      detailTitleEl.textContent = `${detail.cinema.nome} - ${detail.cinema.citta}`;

      availableDays = Array.isArray(detail.availableDays)
        ? detail.availableDays.map((value) => window.DateUtils.toIsoDate(value)).filter(Boolean)
        : [];
      renderDateStrip();

      if (!availableDays.length) {
        dayBodyEl.innerHTML = "<p class='subtle'>Nessuna proiezione disponibile nei prossimi giorni.</p>";
        setStatus("Nessuna data con proiezioni disponibile per questo cinema.", "info");
        return;
      }

      const films = Array.isArray(detail.programmazione) ? detail.programmazione : [];
      if (!films.length) {
        dayBodyEl.innerHTML = "<p class='subtle'>Nessuna programmazione disponibile per questo giorno.</p>";
        setStatus("Nessuna programmazione trovata per la data selezionata.", "info");
        return;
      }

      dayBodyEl.innerHTML = films.map(buildFilmBlock).join("");
      setStatus("Programmazione caricata.", "success");
    } catch (error) {
      dayBodyEl.innerHTML = "";
      setStatus(`Errore caricamento programmazione: ${error.message}`, "error");
    }
  }

  function bindDetailEvents() {
    dateStripEl.addEventListener("click", async (event) => {
      const button = event.target.closest("button[data-day]");
      if (!button) {
        return;
      }

      selectedDayIso = button.getAttribute("data-day");
      dateStripEl.querySelectorAll("button[data-day]").forEach((b) => b.classList.toggle("active", b === button));
      await loadCinemaProgrammazione();
    });

    dayBodyEl.addEventListener("click", (event) => {
      const button = event.target.closest(".cinema-showtime");
      if (!button) {
        return;
      }

      const url = button.getAttribute("data-url");
      if (!url) {
        return;
      }

      if (!window.AuthService || !window.AuthService.isAuthenticated()) {
        if (window.ProgrammazioneShared) {
          window.ProgrammazioneShared.redirectToLoginForDestination(url);
        } else {
          window.location.replace(`/login.html?callback=${encodeURIComponent(url)}`);
        }
        return;
      }

      window.location.href = url;
    });
  }

  function bindFilterEvents() {
    if (filterCityEl) {
      filterCityEl.addEventListener("input", () => {
        if (cityDebounce) clearTimeout(cityDebounce);
        cityDebounce = setTimeout(() => loadCinemaList(), 300);
      });
    }

    if (filterTipologiaEl) {
      filterTipologiaEl.addEventListener("change", () => loadCinemaList());
    }

    if (filterRadiusEl) {
      filterRadiusEl.addEventListener("input", () => {
        if (filterRadiusValueEl) {
          filterRadiusValueEl.textContent = `${filterRadiusEl.value} km`;
        }
        if (radiusDebounce) clearTimeout(radiusDebounce);
        radiusDebounce = setTimeout(() => loadCinemaList(), 400);
      });
    }
  }

  async function initMyCinemasPage() {
    selectedCinemaId = getCinemaIdFromQuery();

    if (!selectedCinemaId) {
      detailShellEl.classList.add("hidden");
      if (filtersEl) filtersEl.style.display = "";

      geoPosition = await window.GeoPermission.requestGeoWithPopup();
      if (geoPosition) {
        if (filterDistanceRowEl) filterDistanceRowEl.classList.remove("hidden");
        setStatus("Posizione ottenuta, caricamento cinema...", "info");
      }
      if (filterRadiusValueEl && filterRadiusEl) {
        filterRadiusValueEl.textContent = `${filterRadiusEl.value} km`;
      }

      await loadTipologie();
      bindFilterEvents();
      await loadCinemaList();
      return;
    }

    if (filtersEl) filtersEl.style.display = "none";
    listEl.innerHTML = "";
    detailShellEl.classList.remove("hidden");
    bindDetailEvents();

    selectedDayIso = selectedDayIso || window.DateUtils.toIsoDate(new Date());
    await loadCinemaProgrammazione();
  }

  window.initMyCinemasPage = initMyCinemasPage;
})();
