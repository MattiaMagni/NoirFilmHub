(function () {
  const listEl = document.getElementById("home-films-list");
  const statusEl = document.getElementById("home-status");
  const kpiVisibleEl = document.getElementById("home-kpi-visible");
  const kpiTotalEl = document.getElementById("home-kpi-total");
  const modalEl = document.getElementById("film-modal");
  const modalCloseEl = document.getElementById("film-modal-close");
  const modalTitleEl = document.getElementById("film-modal-title");
  const modalPosterEl = document.getElementById("film-modal-poster");
  const modalPosterFallbackEl = document.getElementById("film-modal-poster-fallback");
  const modalRegistaEl = document.getElementById("film-modal-regista");
  const modalDurataEl = document.getElementById("film-modal-durata");
  const modalDataEl = document.getElementById("film-modal-data");
  const modalTrailerWrapEl = document.getElementById("film-modal-trailer-wrap");
  const modalTrailerEl = document.getElementById("film-modal-trailer");

  let cachedFilms = [];
  let registiById = new Map();
  let lastFocusedEl = null;

  function setStatus(message, kind) {
    if (!statusEl) {
      return;
    }
    statusEl.className = "status " + kind;
    statusEl.textContent = message;
  }

  function renderFilmCard(film) {
    const cover = film.copertinaPath
      ? `<img src="${film.copertinaPath}" alt="Copertina ${film.titolo}">`
      : "<span>Copertina non disponibile</span>";

    return `
      <article class="home-poster-card" role="button" tabindex="0" data-film-id="${film.id}">
        <div class="card-media">${cover}</div>
        <div class="home-poster-overlay">
          <h3 class="home-poster-title">${film.titolo || "Senza titolo"}</h3>
        </div>
      </article>
    `;
  }

  function formatDate(value) {
    if (!value) {
      return "-";
    }
    return String(value).slice(0, 10);
  }

  async function ensureRegistiMap() {
    if (registiById.size > 0) {
      return;
    }
    const registi = await window.ApiClient.get("/registi");
    registiById = new Map(
      (Array.isArray(registi) ? registi : []).map((r) => [
        Number(r.id),
        [r.nome, r.cognome].filter(Boolean).join(" ").trim() || `Regista #${r.id}`
      ])
    );
  }

  function closeFilmModal() {
    if (!modalEl) {
      return;
    }
    modalEl.classList.add("hidden");
    document.body.classList.remove("modal-open");
    if (lastFocusedEl && typeof lastFocusedEl.focus === "function") {
      lastFocusedEl.focus();
    }
  }

  function openFilmModal(film) {
    if (!modalEl || !film) {
      return;
    }

    if (modalTitleEl) {
      modalTitleEl.textContent = film.titolo || "Senza titolo";
    }
    if (modalRegistaEl) {
      modalRegistaEl.textContent = registiById.get(Number(film.registaId)) || "Regista non disponibile";
    }
    if (modalDurataEl) {
      modalDurataEl.textContent = film.durata ? `${film.durata} min` : "-";
    }
    if (modalDataEl) {
      modalDataEl.textContent = formatDate(film.dataProduzione);
    }

    if (modalPosterEl && modalPosterFallbackEl) {
      if (film.copertinaPath) {
        modalPosterEl.src = film.copertinaPath;
        modalPosterEl.alt = `Poster ${film.titolo || "film"}`;
        modalPosterEl.classList.remove("hidden");
        modalPosterFallbackEl.classList.add("hidden");
      } else {
        modalPosterEl.removeAttribute("src");
        modalPosterEl.classList.add("hidden");
        modalPosterFallbackEl.classList.remove("hidden");
      }
    }

    if (modalTrailerWrapEl && modalTrailerEl) {
      if (film.filmatoPath) {
        modalTrailerEl.href = film.filmatoPath;
        modalTrailerWrapEl.classList.remove("hidden");
      } else {
        modalTrailerEl.removeAttribute("href");
        modalTrailerWrapEl.classList.add("hidden");
      }
    }

    modalEl.classList.remove("hidden");
    document.body.classList.add("modal-open");
    if (modalCloseEl) {
      modalCloseEl.focus();
    }
  }

  async function onFilmCardActivate(target) {
    const card = target.closest("[data-film-id]");
    if (!card) {
      return;
    }

    const id = Number(card.getAttribute("data-film-id"));
    const film = cachedFilms.find((x) => Number(x.id) === id);
    if (!film) {
      return;
    }

    lastFocusedEl = card;
    try {
      await ensureRegistiMap();
    } catch {
      registiById = new Map();
    }
    openFilmModal(film);
  }

  function bindModalEvents() {
    if (!modalEl || !listEl) {
      return;
    }

    listEl.addEventListener("click", (event) => {
      onFilmCardActivate(event.target);
    });

    listEl.addEventListener("keydown", (event) => {
      if (event.key !== "Enter" && event.key !== " ") {
        return;
      }
      const trigger = event.target.closest("[data-film-id]");
      if (!trigger) {
        return;
      }
      event.preventDefault();
      onFilmCardActivate(trigger);
    });

    if (modalCloseEl) {
      modalCloseEl.addEventListener("click", closeFilmModal);
    }

    modalEl.addEventListener("click", (event) => {
      if (event.target === modalEl) {
        closeFilmModal();
      }
    });

    document.addEventListener("keydown", (event) => {
      if (event.key === "Escape" && !modalEl.classList.contains("hidden")) {
        closeFilmModal();
      }
    });
  }

  async function loadHomeFilms() {
    if (!listEl) {
      return;
    }

    setStatus("Caricamento film in corso...", "info");

    try {
      const films = await window.ApiClient.get("/films");
      if (!Array.isArray(films) || films.length === 0) {
        listEl.innerHTML = "";
        if (kpiVisibleEl) {
          kpiVisibleEl.textContent = "0";
        }
        if (kpiTotalEl) {
          kpiTotalEl.textContent = "0";
        }
        setStatus("Nessun film disponibile in programmazione.", "info");
        return;
      }

      const visible = films.slice(0, 12);
      cachedFilms = visible;
      listEl.innerHTML = visible.map(renderFilmCard).join("");
      if (kpiVisibleEl) {
        kpiVisibleEl.textContent = String(visible.length);
      }
      if (kpiTotalEl) {
        kpiTotalEl.textContent = String(films.length);
      }
      setStatus("Film caricati correttamente.", "success");
    } catch (error) {
      listEl.innerHTML = "";
      if (kpiVisibleEl) {
        kpiVisibleEl.textContent = "0";
      }
      if (kpiTotalEl) {
        kpiTotalEl.textContent = "0";
      }
      setStatus(`Errore caricamento film: ${error.message}`, "error");
    }
  }

  bindModalEvents();
  window.loadHomeFilms = loadHomeFilms;
})();
