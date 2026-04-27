(function () {
  const listEl = document.getElementById("home-films-list");
  const statusEl = document.getElementById("home-status");
  const kpiVisibleEl = document.getElementById("home-kpi-visible");
  const kpiTotalEl = document.getElementById("home-kpi-total");
  const heroEl = document.getElementById("home-hero");
  const heroPrevBtn = document.getElementById("hero-nav-prev");
  const heroNextBtn = document.getElementById("hero-nav-next");
  const heroTitleEl = document.getElementById("hero-feature-title");
  const heroSubtitleEl = document.getElementById("hero-feature-subtitle");
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
  let heroRotationFilms = [];
  let heroRotationIndex = 0;
  let heroRotationTimer = null;

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
    const regista = registiById.get(Number(film.registaId)) || "Regia non disponibile";
    const year = film.dataProduzione ? String(film.dataProduzione).slice(0, 4) : "-";

    return `
      <article class="home-poster-card" role="button" tabindex="0" data-film-id="${film.id}">
        <div class="card-media">${cover}</div>
        <div class="home-poster-overlay">
          <h3 class="home-poster-title">${film.titolo || "Senza titolo"}</h3>
        </div>
        <div class="home-poster-caption">
          <h4>${film.titolo || "Senza titolo"}</h4>
          <p>${regista} / ${year}</p>
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

  function normalizeTmdbImage(url, preferredSize) {
    const raw = String(url || "").trim();
    if (!raw || !raw.includes("image.tmdb.org/t/p/")) {
      return raw;
    }
    const sizes = ["original", "w1280", "w780", "w500"];
    const target = sizes.includes(preferredSize) ? preferredSize : "w1280";
    return raw.replace(/\/t\/p\/(original|w\d+)\//, `/t/p/${target}/`);
  }

  function updateHeroFeature(film) {
    if (!film) {
      return;
    }

    if (heroTitleEl) {
      heroTitleEl.textContent = film.titolo || "Noir Film Hub";
    }

    if (heroSubtitleEl) {
      const durataLabel = film.durata ? `${film.durata} min` : "durata non disponibile";
      heroSubtitleEl.textContent = `${film.titolo || "Titolo non disponibile"} - ${durataLabel}.`;
    }

    const image = normalizeTmdbImage(film.backdropPath || "", "w1280")
      || normalizeTmdbImage(film.copertinaPath || "", "w780")
      || film.copertinaPath
      || film.filmatoPath;
    if (heroEl && image) {
      heroEl.style.setProperty("--hero-feature-image", `url("${image}")`);
    }
  }

  function stopHeroRotation() {
    if (heroRotationTimer) {
      clearInterval(heroRotationTimer);
      heroRotationTimer = null;
    }
  }

  function startHeroRotation() {
    stopHeroRotation();
    if (!Array.isArray(heroRotationFilms) || heroRotationFilms.length <= 1) {
      return;
    }

    heroRotationTimer = setInterval(() => {
      heroRotationIndex = (heroRotationIndex + 1) % heroRotationFilms.length;
      updateHeroFeature(heroRotationFilms[heroRotationIndex]);
    }, 6000);
  }

  function showPreviousHeroFilm() {
    if (!Array.isArray(heroRotationFilms) || heroRotationFilms.length === 0) {
      return;
    }
    heroRotationIndex = (heroRotationIndex - 1 + heroRotationFilms.length) % heroRotationFilms.length;
    updateHeroFeature(heroRotationFilms[heroRotationIndex]);
    startHeroRotation();
  }

  function showNextHeroFilm() {
    if (!Array.isArray(heroRotationFilms) || heroRotationFilms.length === 0) {
      return;
    }
    heroRotationIndex = (heroRotationIndex + 1) % heroRotationFilms.length;
    updateHeroFeature(heroRotationFilms[heroRotationIndex]);
    startHeroRotation();
  }

  function bindHeroRotationEvents() {
    if (!heroEl) {
      return;
    }

    heroEl.addEventListener("mouseenter", stopHeroRotation);
    heroEl.addEventListener("mouseleave", startHeroRotation);
    heroEl.addEventListener("focusin", stopHeroRotation);
    heroEl.addEventListener("focusout", startHeroRotation);

    if (heroPrevBtn) {
      heroPrevBtn.addEventListener("click", showPreviousHeroFilm);
    }
    if (heroNextBtn) {
      heroNextBtn.addEventListener("click", showNextHeroFilm);
    }
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
      try {
        await ensureRegistiMap();
      } catch {
        registiById = new Map();
      }
      heroRotationFilms = films
        .slice()
        .sort((a, b) => {
          const aDate = Date.parse(a.dataUscita || a.dataProduzione || 0) || 0;
          const bDate = Date.parse(b.dataUscita || b.dataProduzione || 0) || 0;
          return bDate - aDate || Number(b.id || 0) - Number(a.id || 0);
        })
        .slice(0, 3);
      heroRotationIndex = 0;
      updateHeroFeature(heroRotationFilms[0]);
      startHeroRotation();
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
  bindHeroRotationEvents();
  window.loadHomeFilms = loadHomeFilms;
})();
