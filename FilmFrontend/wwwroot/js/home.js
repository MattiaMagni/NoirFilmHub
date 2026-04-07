(function () {
  const listEl = document.getElementById("home-films-list");
  const statusEl = document.getElementById("home-status");

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
      <article class="card home-show-card">
        <div class="card-media">${cover}</div>
        <div class="card-body">
          <div class="home-show-head">
            <h3 class="home-show-title">${film.titolo || "Senza titolo"}</h3>
          </div>
          <div class="home-show-meta">
            <p><span>Durata</span><strong>${film.durata || "-"} min</strong></p>
            <p><span>ID regista</span><strong>#${film.registaId || "-"}</strong></p>
          </div>
        </div>
      </article>
    `;
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
        setStatus("Nessun film disponibile in programmazione.", "info");
        return;
      }

      listEl.innerHTML = films.slice(0, 8).map(renderFilmCard).join("");
      setStatus("Film caricati correttamente.", "success");
    } catch (error) {
      listEl.innerHTML = "";
      setStatus(`Errore caricamento film: ${error.message}`, "error");
    }
  }

  window.loadHomeFilms = loadHomeFilms;
})();
