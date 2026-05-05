(function () {
  const statusEl = document.getElementById("programmazione-status");
  const cardsEl = document.getElementById("programmazione-cards");
  const tabsEl = document.getElementById("programmazione-tabs");
  const searchEl = document.getElementById("programmazione-search");
  const categoryEl = document.getElementById("programmazione-category");
  const cinemaBadgeEl = document.getElementById("cinema-selezionato-badge");
  const chooseCinemaSelect = document.getElementById("choose-cinema-select");

  let selectedTab = "featured";
  let selectedCinemaId = null;
  let geoPosition = null;

  function setStatus(message, kind) {
    statusEl.className = `status ${kind}`;
    statusEl.textContent = message;
  }

  function getStoredCinemaId() {
    const raw = localStorage.getItem("selected_cinema_id");
    const parsed = Number(raw);
    return parsed > 0 ? parsed : null;
  }

  function setStoredCinemaId(cinemaId) {
    if (cinemaId && cinemaId > 0) {
      localStorage.setItem("selected_cinema_id", String(cinemaId));
      return;
    }
    localStorage.removeItem("selected_cinema_id");
  }

  async function syncCinemaForUser(cinemaId) {
    if (!window.AuthService || !window.AuthService.isAuthenticated()) {
      return;
    }
    try {
      await window.ApiClient.put("/auth/me/cinema-preferito", { cinemaId });
    } catch {
    }
  }

  async function loadCinemaFromUserIfAny() {
    if (!window.AuthService || !window.AuthService.isAuthenticated()) {
      return null;
    }
    try {
      const result = await window.ApiClient.get("/auth/me/cinema-preferito");
      if (result && result.cinemaPreferitoId) {
        return Number(result.cinemaPreferitoId);
      }
      return null;
    } catch {
      return null;
    }
  }

  function queryString() {
    const params = new URLSearchParams();
    params.set("tab", selectedTab);

    const search = (searchEl.value || "").trim();
    if (search) {
      params.set("search", search);
    }

    const categoria = Number(categoryEl.value);
    if (categoria > 0) {
      params.set("categoria", String(categoria));
    }

    if (selectedCinemaId) {
      params.set("cinemaId", String(selectedCinemaId));
    }

    return params.toString();
  }

  function filmCard(item) {
    const availability = item.presenteNelCinemaSelezionato
      ? "<span class='tag accent'>Nel tuo cinema</span>"
      : selectedCinemaId
        ? "<span class='tag secondary'>Non nel tuo cinema</span>"
        : "<span class='tag secondary'>Cinema non selezionato</span>";

    const categories = Array.isArray(item.categorie) && item.categorie.length
      ? item.categorie.map((c) => `<span class='tag info'>${c}</span>`).join(" ")
      : "<span class='tag info'>Senza categoria</span>";

    const dataUscita = item.dataUscita ? String(item.dataUscita).slice(0, 10) : "Data non disponibile";
    const durata = item.durata ? `${item.durata} min` : "Durata non disponibile";
    const regista = item.regista ? `Regia: ${item.regista}` : "Regista non disponibile";

    return `
      <article class="card schedule-film-card" data-film-id="${item.id}" role="button" tabindex="0">
        <div class="card-media">
          ${item.copertinaPath ? `<img src="${item.copertinaPath}" alt="Copertina ${item.titolo}">` : "<span>Copertina non disponibile</span>"}
        </div>
        <div class="card-body">
          <h3>${item.titolo}</h3>
          <p class="subtle">${durata}</p>
          <p class="subtle">${regista}</p>
          <p class="subtle">Uscita: ${dataUscita}</p>
          <div class="actions">${availability}</div>
          <div class="actions">${categories}</div>
        </div>
      </article>
    `;
  }

  async function loadFilms() {
    setStatus("Caricamento programmazione...", "info");
    try {
      const query = queryString();
      const list = await window.ApiClient.get(`/programmazione/films?${query}`);
      const items = Array.isArray(list) ? list : [];
      if (!items.length) {
        cardsEl.innerHTML = "<div class='status info'>Nessun film trovato con i filtri selezionati.</div>";
        setStatus("Nessun risultato.", "info");
        return;
      }

      cardsEl.innerHTML = items.map(filmCard).join("");
      setStatus(`Caricati ${items.length} film.`, "success");
    } catch (error) {
      cardsEl.innerHTML = "";
      setStatus(`Errore caricamento: ${error.message}`, "error");
    }
  }

  async function loadCategories() {
    try {
      const categories = await window.ApiClient.get("/categorie");
      const options = (Array.isArray(categories) ? categories : [])
        .map((c) => `<option value="${c.id}">${c.nome}</option>`)
        .join("");
      categoryEl.innerHTML = `<option value="">Tutte le categorie</option>${options}`;
    } catch {
      categoryEl.innerHTML = "<option value=''>Tutte le categorie</option>";
    }
  }

  async function resolveCinemaLabel() {
    if (!selectedCinemaId) {
      cinemaBadgeEl.textContent = "Cinema non selezionato";
      return;
    }

    try {
      const cinema = await window.ApiClient.get(`/cinemas/${selectedCinemaId}`);
      cinemaBadgeEl.textContent = `${cinema.nome} - ${cinema.citta}`;
    } catch {
      cinemaBadgeEl.textContent = "Cinema selezionato non disponibile";
    }
  }

  async function requestGeolocation() {
    if (!navigator.geolocation) {
      return null;
    }
    return await new Promise((resolve) => {
      navigator.geolocation.getCurrentPosition(
        (position) => resolve(position.coords),
        () => resolve(null),
        { enableHighAccuracy: false, timeout: 3500, maximumAge: 60000 }
      );
    });
  }

  async function loadCinemaSelectOptions() {
    if (!chooseCinemaSelect) {
      return;
    }

    try {
      let cinemas;
      if (geoPosition && geoPosition.latitude != null && geoPosition.longitude != null) {
        cinemas = await window.ApiClient.get(`/cinemas/nearby?lat=${geoPosition.latitude}&lng=${geoPosition.longitude}`);
      } else {
        cinemas = await window.ApiClient.get("/cinemas");
      }

      const list = Array.isArray(cinemas) ? cinemas : [];
      const options = ["<option value=''>Scegli cinema</option>"];
      list.forEach((c) => {
        const distanza = c.distanzaKm != null ? ` (${Number(c.distanzaKm).toFixed(1)} km)` : "";
        const selected = Number(c.id) === Number(selectedCinemaId) ? " selected" : "";
        options.push(`<option value="${c.id}"${selected}>${c.nome} - ${c.citta}${distanza}</option>`);
      });

      chooseCinemaSelect.innerHTML = options.join("");
    } catch (error) {
      chooseCinemaSelect.innerHTML = "<option value=''>Cinema non disponibile</option>";
      setStatus(`Errore caricamento cinema: ${error.message}`, "error");
    }
  }

  function bindEvents() {
    tabsEl.addEventListener("click", async (event) => {
      const button = event.target.closest("button[data-tab]");
      if (!button) {
        return;
      }

      selectedTab = button.dataset.tab;
      tabsEl.querySelectorAll("button[data-tab]").forEach((b) => {
        b.classList.toggle("active", b === button);
      });
      await loadFilms();
    });

    searchEl.addEventListener("input", debounce(loadFilms, 280));
    categoryEl.addEventListener("change", loadFilms);

    cardsEl.addEventListener("click", (event) => {
      const card = event.target.closest("[data-film-id]");
      if (!card) {
        return;
      }
      const filmId = Number(card.dataset.filmId);
      if (!filmId) {
        return;
      }
      window.location.href = `/scheda-film.html?idFilm=${filmId}`;
    });

    cardsEl.addEventListener("keydown", (event) => {
      if (event.key !== "Enter" && event.key !== " ") {
        return;
      }
      const card = event.target.closest("[data-film-id]");
      if (!card) {
        return;
      }
      event.preventDefault();
      const filmId = Number(card.dataset.filmId);
      if (filmId) {
        window.location.href = `/scheda-film.html?idFilm=${filmId}`;
      }
    });

    if (chooseCinemaSelect) {
      chooseCinemaSelect.addEventListener("change", async () => {
        const cinemaId = Number(chooseCinemaSelect.value);
        if (!cinemaId) {
          selectedCinemaId = null;
          setStoredCinemaId(null);
          await resolveCinemaLabel();
          await loadFilms();
          return;
        }

        selectedCinemaId = cinemaId;
        setStoredCinemaId(cinemaId);
        await syncCinemaForUser(cinemaId);
        await resolveCinemaLabel();
        await loadFilms();
      });
    }

  }

  function debounce(fn, delay) {
    let timer = null;
    return function debounced(...args) {
      if (timer) {
        clearTimeout(timer);
      }
      timer = setTimeout(() => fn.apply(this, args), delay);
    };
  }

  async function initProgrammazionePage() {
    if (!cardsEl || !tabsEl || !searchEl || !categoryEl) {
      return;
    }

    geoPosition = await requestGeolocation();

    const userCinemaId = await loadCinemaFromUserIfAny();
    selectedCinemaId = userCinemaId || getStoredCinemaId();

    await loadCategories();
    await loadCinemaSelectOptions();
    await resolveCinemaLabel();
    bindEvents();
    await loadFilms();
  }

  window.initProgrammazionePage = initProgrammazionePage;
})();
