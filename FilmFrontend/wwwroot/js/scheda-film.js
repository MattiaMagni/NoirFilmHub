(function () {
  const statusEl = document.getElementById("scheda-status");
  const detailsEl = document.getElementById("scheda-details");
  const dateStripEl = document.getElementById("show-date-strip");
  const showBodyEl = document.getElementById("show-body");
  const goToShowsBtn = document.getElementById("goto-shows-btn");

  let filmId = null;
  let selectedCinemaId = null;
  let selectedDate = null;
  let cachedCalendar = [];
  let selectedCinema = null;

  function setStatus(message, kind) {
    statusEl.className = `status ${kind}`;
    statusEl.textContent = message;
  }

  function getFilmId() {
    const params = new URLSearchParams(window.location.search);
    const id = Number(params.get("idFilm"));
    return id > 0 ? id : null;
  }

  function getStoredCinemaId() {
    const raw = localStorage.getItem("selected_cinema_id");
    const parsed = Number(raw);
    return parsed > 0 ? parsed : null;
  }

  async function getCinemaFromProfile() {
    if (!window.AuthService || !window.AuthService.isAuthenticated()) {
      return null;
    }
    try {
      const profileCinema = await window.ApiClient.get("/auth/me/cinema-preferito");
      return profileCinema && profileCinema.cinemaPreferitoId ? Number(profileCinema.cinemaPreferitoId) : null;
    } catch {
      return null;
    }
  }

  function formatDate(value) {
    const d = new Date(value);
    if (Number.isNaN(d.getTime())) {
      return "-";
    }
    const day = String(d.getDate()).padStart(2, "0");
    const month = String(d.getMonth() + 1).padStart(2, "0");
    const year = d.getFullYear();
    return `${day}/${month}/${year}`;
  }

  function buildFilmDetails(film) {
    const categories = Array.isArray(film.categorie) && film.categorie.length
      ? film.categorie.map((c) => `<span class="tag info">${c}</span>`).join(" ")
      : "<span class='tag info'>Senza categoria</span>";

    detailsEl.innerHTML = `
      <article class="panel film-sheet">
        <div class="film-sheet-grid">
          <div class="film-sheet-media">
            ${film.copertinaPath ? `<img src="${film.copertinaPath}" alt="Copertina ${film.titolo}">` : "<div class='card-media'><span>Copertina non disponibile</span></div>"}
          </div>
          <div>
            <h2>${film.titolo}</h2>
            <p class="subtle">${film.descrizioneLunga || "Descrizione non disponibile."}</p>
            <div class="actions">${categories}</div>
            <p class="subtle"><strong>Durata:</strong> ${film.durata || "-"} min</p>
            <p class="subtle"><strong>Data rilascio:</strong> ${formatDate(film.dataUscita || film.dataProduzione)}</p>
            <p class="subtle"><strong>Regista:</strong> ${film.regista || "N/D"}</p>
            <p class="subtle"><strong>Cast:</strong> ${film.castPrincipale || "N/D"}</p>
            ${film.filmatoPath ? `<p><a class="button secondary" href="${film.filmatoPath}" target="_blank" rel="noopener noreferrer">Guarda trailer</a></p>` : ""}
          </div>
        </div>
      </article>
    `;
  }

  function renderDateStrip() {
    const days = window.DateUtils.nextDays(14);
    dateStripEl.innerHTML = days
      .map((item, index) => {
        const active = (selectedDate && selectedDate === item.iso) || (!selectedDate && index === 0);
        return `<button class="btn-small secondary ${active ? "active" : ""}" data-date="${item.iso}">${item.label}</button>`;
      })
      .join("");
    if (!selectedDate && days[0]) {
      selectedDate = days[0].iso;
    }
  }

  function bookingUrl(item) {
    return `/acquista.html?idCinema=${item.cinemaId}&idFilm=${filmId}&idSala=${item.salaId}&idShow=${item.proiezioneId}`;
  }

  function renderShowByDate() {
    const selected = cachedCalendar.find((x) => x.data === selectedDate);
    if (!selected) {
      showBodyEl.innerHTML = "<p class='subtle'>Nessuno show per la data selezionata.</p>";
      return;
    }

    if (!selectedCinemaId) {
      showBodyEl.innerHTML = "<p class='subtle'>Seleziona prima un cinema dalla pagina Programmazione per visualizzare gli show.</p>";
      return;
    }

    const cinemaInfo = `
      <div class="panel">
        <h3>${selected.cinemaNome} - ${selected.citta}</h3>
        <p class="subtle">${selected.indirizzo}</p>
      </div>
    `;

    const tipologie = selected.tipologie
      .map((tipologia) => {
        const buttons = tipologia.orari
          .map((item) => {
            return `<button class="btn-small primary showtime-btn" data-proiezione-id="${item.proiezioneId}" data-sala-id="${item.salaId}" data-url="${bookingUrl(item)}">${item.ora}</button>`;
          })
          .join("");

        return `
          <div class="panel">
            <h4>${tipologia.tipologiaSala}</h4>
            <div class="actions">${buttons}</div>
          </div>
        `;
      })
      .join("");

    showBodyEl.innerHTML = `${cinemaInfo}${tipologie}`;
  }

  function bindShowActions() {
    showBodyEl.addEventListener("click", (event) => {
      const button = event.target.closest(".showtime-btn");
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
          const callback = encodeURIComponent(url);
          window.location.replace(`/login.html?callback=${callback}`);
        }
        return;
      }

      window.location.href = url;
    });

    dateStripEl.addEventListener("click", (event) => {
      const button = event.target.closest("button[data-date]");
      if (!button) {
        return;
      }
      selectedDate = button.getAttribute("data-date");
      dateStripEl.querySelectorAll("button[data-date]").forEach((b) => b.classList.toggle("active", b === button));
      renderShowByDate();
    });
  }

  async function loadFilm() {
    setStatus("Caricamento dettaglio film...", "info");
    try {
      const detail = await window.ApiClient.get(`/programmazione/films/${filmId}?cinemaId=${selectedCinemaId || ""}`);
      buildFilmDetails(detail);

      const calendar = Array.isArray(detail.calendario) ? detail.calendario : [];
      selectedCinema = {
        nome: detail.cinemaNome || "Cinema selezionato",
        citta: detail.citta || "",
        indirizzo: detail.indirizzo || ""
      };
      cachedCalendar = calendar.map((entry) => ({
        data: window.DateUtils.toIsoDate(entry.data),
        tipologie: Array.isArray(entry.tipologie) ? entry.tipologie : [],
        cinemaNome: entry.cinemaNome || selectedCinema.nome,
        citta: entry.citta || selectedCinema.citta,
        indirizzo: entry.indirizzo || selectedCinema.indirizzo
      }));

      if (cachedCalendar.length > 0) {
        const first = cachedCalendar[0];
        selectedDate = first.data;
      }
      renderDateStrip();
      renderShowByDate();
      setStatus("Dettaglio film caricato.", "success");
    } catch (error) {
      setStatus(`Errore caricamento: ${error.message}`, "error");
      detailsEl.innerHTML = "";
      dateStripEl.innerHTML = "";
      showBodyEl.innerHTML = "";
    }
  }

  async function initSchedaFilmPage() {
    filmId = getFilmId();
    if (!filmId) {
      setStatus("Parametro idFilm mancante.", "error");
      return;
    }

    selectedCinemaId = await getCinemaFromProfile() || getStoredCinemaId();

    goToShowsBtn.addEventListener("click", () => {
      const section = document.getElementById("shows-section");
      if (section) {
        section.scrollIntoView({ behavior: "smooth", block: "start" });
      }
    });

    bindShowActions();
    await loadFilm();
  }

  window.initSchedaFilmPage = initSchedaFilmPage;
})();
