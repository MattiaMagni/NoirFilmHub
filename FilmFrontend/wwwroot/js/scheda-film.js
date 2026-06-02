(function () {
  const statusEl = document.getElementById("scheda-status");
  const detailsEl = document.getElementById("scheda-details");
  const dateStripEl = document.getElementById("show-date-strip");
  const showBodyEl = document.getElementById("show-body");
  const goToShowsBtn = document.getElementById("goto-shows-btn");
  const bookingFilmEl = document.getElementById("booking-summary-film");
  const bookingDateEl = document.getElementById("booking-summary-date");
  const bookingTimeEl = document.getElementById("booking-summary-time");
  const bookingCtaBtn = document.getElementById("booking-cta-btn");

  let filmId = null;
  let selectedCinemaId = null;
  let selectedDate = null;
  let cachedCalendar = [];
  let selectedCinema = null;
  let selectedShow = null;

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
      ? film.categorie.join(" / ")
      : "Categoria non disponibile";

    const shortDescription = String(film.descrizioneLunga || "Descrizione non disponibile.").slice(0, 220);

    detailsEl.innerHTML = `
      <article class="panel film-sheet">
        <div class="film-sheet-grid">
          <div class="film-sheet-media">
            ${film.copertinaPath ? `<img src="${film.copertinaPath}" alt="Copertina ${film.titolo}">` : "<div class='card-media'><span>Copertina non disponibile</span></div>"}
          </div>
          <div>
            <h2>${film.titolo}</h2>
            <p class="subtle">${categories}</p>
            <p class="subtle">${shortDescription}</p>
          </div>
        </div>
      </article>
    `;
  }

  function renderDateStrip() {
    const days = (Array.isArray(cachedCalendar) ? cachedCalendar : [])
      .map((entry) => entry && entry.data)
      .filter(Boolean)
      .map((iso) => ({ iso, label: window.DateUtils.formatDatePill(iso) }));

    if (!days.length) {
      dateStripEl.innerHTML = "";
      selectedDate = null;
      return;
    }

    if (!selectedDate || !days.some((item) => item.iso === selectedDate)) {
      selectedDate = days[0].iso;
    }

    dateStripEl.innerHTML = days
      .map((item) => {
        const active = selectedDate === item.iso;
        return `<button class="btn-small secondary ${active ? "active" : ""}" data-date="${item.iso}">${item.label}</button>`;
      })
      .join("");
  }

  function bookingUrl(item) {
    return `/acquista.html?idCinema=${item.cinemaId}&idFilm=${filmId}&idSala=${item.salaId}&idShow=${item.proiezioneId}`;
  }

  function updateBookingSummary() {
    if (!bookingFilmEl || !bookingDateEl || !bookingTimeEl || !bookingCtaBtn) {
      return;
    }

    const filmTitle = detailsEl.querySelector("h2")?.textContent?.trim() || "-";
    bookingFilmEl.textContent = filmTitle;

    if (!selectedDate) {
      bookingDateEl.textContent = "-";
    } else {
      bookingDateEl.textContent = window.DateUtils.formatDatePill(selectedDate);
    }

    if (!selectedShow) {
      bookingTimeEl.textContent = "-";
      bookingCtaBtn.disabled = true;
      bookingCtaBtn.removeAttribute("data-url");
      return;
    }

    bookingTimeEl.textContent = selectedShow.ora || "-";
    bookingCtaBtn.disabled = false;
    bookingCtaBtn.setAttribute("data-url", selectedShow.url || "");
  }

  function renderShowByDate() {
    const selected = cachedCalendar.find((x) => x.data === selectedDate);
    selectedShow = null;
    if (!selected) {
      showBodyEl.innerHTML = "<p class='subtle'>Nessuno show per la data selezionata.</p>";
      updateBookingSummary();
      return;
    }

    if (!selectedCinemaId) {
      showBodyEl.innerHTML = "<p class='subtle'>Seleziona prima un cinema dalla pagina Programmazione per visualizzare gli show.</p>";
      updateBookingSummary();
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
        const now = new Date();
        const todayIso = window.DateUtils.toIsoDate(now);
        const isToday = selectedDate === todayIso;

        const visibleOrari = isToday
          ? tipologia.orari.filter(item => {
              const [h, m] = item.ora.split(':').map(Number);
              const showTime = new Date();
              showTime.setHours(h, m, 0, 0);
              return showTime > now;
            })
          : tipologia.orari;

        if (!visibleOrari.length) return '';

        const buttons = visibleOrari
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
    updateBookingSummary();
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

      const timeLabel = button.textContent ? button.textContent.trim() : "";
      selectedShow = { ora: timeLabel, url };
      showBodyEl.querySelectorAll(".showtime-btn").forEach((btn) => btn.classList.toggle("active", btn === button));
      updateBookingSummary();

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

    if (bookingCtaBtn) {
      bookingCtaBtn.addEventListener("click", () => {
        const url = bookingCtaBtn.getAttribute("data-url");
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
    }
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
      updateBookingSummary();
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
