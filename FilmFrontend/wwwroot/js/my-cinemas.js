(function () {
  const statusEl = document.getElementById("my-cinemas-status");
  const listEl = document.getElementById("my-cinemas-list");
  const detailShellEl = document.getElementById("my-cinemas-detail-shell");
  const detailTitleEl = document.getElementById("my-cinemas-detail-title");
  const dateStripEl = document.getElementById("my-cinemas-date-strip");
  const dayBodyEl = document.getElementById("my-cinemas-day-body");

  let selectedCinemaId = null;
  let selectedDayIso = null;

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

    return `
      <article class="card cinema-card">
        <div class="card-body">
          <h3>${cinema.nome}</h3>
          <p class="subtle">${cinema.citta} - ${cinema.indirizzo}</p>
          <div class="actions">${tipologie}</div>
          <p><a class="button secondary" href="/my-cinemas.html?idCinema=${cinema.id}">Apri programmazione</a></p>
        </div>
      </article>
    `;
  }

  async function loadCinemaList() {
    setStatus("Caricamento cinema...", "info");
    try {
      const cinemas = await window.ApiClient.get("/my-cinemas");
      const items = Array.isArray(cinemas) ? cinemas : [];
      if (!items.length) {
        listEl.innerHTML = "<p class='subtle'>Nessun cinema disponibile.</p>";
        setStatus("Nessun cinema trovato.", "info");
        return;
      }
      listEl.innerHTML = items.map(renderCinemaCard).join("");
      setStatus(`Caricati ${items.length} cinema.`, "success");
    } catch (error) {
      listEl.innerHTML = "";
      setStatus(`Errore caricamento cinema: ${error.message}`, "error");
    }
  }

  function renderDateStrip() {
    const days = window.DateUtils.nextDays(10);
    dateStripEl.innerHTML = days
      .map((day, index) => {
        const active = (selectedDayIso && selectedDayIso === day.iso) || (!selectedDayIso && index === 0);
        return `<button class="btn-small secondary ${active ? "active" : ""}" data-day="${day.iso}">${day.label}</button>`;
      })
      .join("");

    if (!selectedDayIso && days[0]) {
      selectedDayIso = days[0].iso;
    }
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
      const detail = await window.ApiClient.get(`/my-cinemas/${selectedCinemaId}/programmazione?day=${selectedDayIso}`);
      detailTitleEl.textContent = `${detail.cinema.nome} - ${detail.cinema.citta}`;

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

  async function initMyCinemasPage() {
    selectedCinemaId = getCinemaIdFromQuery();
    if (!selectedCinemaId) {
      detailShellEl.classList.add("hidden");
      await loadCinemaList();
      return;
    }

    listEl.innerHTML = "";
    detailShellEl.classList.remove("hidden");
    renderDateStrip();
    bindDetailEvents();
    await loadCinemaProgrammazione();
  }

  window.initMyCinemasPage = initMyCinemasPage;
})();
