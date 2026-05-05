(function () {
  const statusEl = document.getElementById("tmdb-admin-status");
  const tableBody = document.getElementById("tmdb-admin-body");
  const runBatchBtn = document.getElementById("tmdb-run-batch");
  const refreshBtn = document.getElementById("tmdb-refresh");

  function setStatus(message, kind) {
    statusEl.className = `status ${kind}`;
    statusEl.textContent = message;
  }

  function renderRows(items) {
    if (!items.length) {
      tableBody.innerHTML = "<tr><td colspan='6' class='subtle'>Nessun film da sincronizzare.</td></tr>";
      return;
    }

    tableBody.innerHTML = items
      .map((f) => `
        <tr>
          <td>${f.id}</td>
          <td>${f.titolo}</td>
          <td>${f.tmdbMovieId || "-"}</td>
          <td>${f.tmdbSyncStato || "NotSynced"}</td>
          <td>${f.ultimaSyncTmdbUtc ? String(f.ultimaSyncTmdbUtc).slice(0, 19).replace("T", " ") : "-"}</td>
          <td>
            <button class="btn-small primary tmdb-sync-film" data-id="${f.id}">Sync</button>
          </td>
        </tr>
      `)
      .join("");
  }

  async function loadMissing() {
    setStatus("Caricamento stato TMDB...", "info");
    try {
      const [status, missing] = await Promise.all([
        window.ApiClient.get("/tmdb/status"),
        window.ApiClient.get("/tmdb/missing")
      ]);

      if (!status.configured) {
        setStatus("TMDB non configurato: inserisci TMDB_API_READ_TOKEN nel backend.", "error");
      } else {
        setStatus(`TMDB configurato. Lingua: ${status.language}.`, "success");
      }

      renderRows(Array.isArray(missing) ? missing : []);
    } catch (error) {
      tableBody.innerHTML = "";
      setStatus(`Errore caricamento TMDB: ${error.message}`, "error");
    }
  }

  async function syncFilm(filmId) {
    setStatus(`Sync TMDB film #${filmId} in corso...`, "info");
    try {
      await window.ApiClient.post(`/tmdb/sync/film/${filmId}`, {});
      setStatus(`Sync TMDB film #${filmId} completata.`, "success");
      await loadMissing();
    } catch (error) {
      setStatus(`Errore sync film #${filmId}: ${error.message}`, "error");
    }
  }

  function bindEvents() {
    runBatchBtn.addEventListener("click", async () => {
      setStatus("Sync batch TMDB in corso...", "info");
      try {
        const result = await window.ApiClient.post("/tmdb/sync/films", {});
        setStatus(`Sync batch completata: success ${result.success}, failed ${result.failed}.`, "success");
        await loadMissing();
      } catch (error) {
        setStatus(`Errore sync batch: ${error.message}`, "error");
      }
    });

    refreshBtn.addEventListener("click", loadMissing);

    tableBody.addEventListener("click", async (event) => {
      const button = event.target.closest(".tmdb-sync-film");
      if (!button) {
        return;
      }
      const filmId = Number(button.dataset.id);
      if (!filmId) {
        return;
      }
      await syncFilm(filmId);
    });
  }

  async function initTmdbAdminPage() {
    bindEvents();
    await loadMissing();
  }

  window.initTmdbAdminPage = initTmdbAdminPage;
})();
