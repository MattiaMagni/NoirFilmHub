(function () {
  let editingId = null;
  let categorie = [];
  let prefillRegistaId = null;
  let quickAddItems = [];

  const tableBody = document.getElementById("films-table-body");
  const form = document.getElementById("film-form");
  const statusEl = document.getElementById("films-status");
  const submitBtn = document.getElementById("film-submit");
  const cancelBtn = document.getElementById("film-cancel");
  const categorieBox = document.getElementById("film-categorie-options");
  const quickAddBtn = document.getElementById("film-quick-add");
  const quickAddModal = document.getElementById("film-quick-add-modal");
  const quickAddCloseBtn = document.getElementById("film-quick-add-close");
  const quickAddStatusEl = document.getElementById("film-quick-add-status");
  const quickAddListEl = document.getElementById("film-quick-add-list");
  const quickAddSubmitBtn = document.getElementById("film-quick-add-submit");
  const quickSelection = new Set();

  function normalizeTmdbImage(url, preferredSize) {
    const raw = String(url || "").trim();
    if (!raw || !raw.includes("image.tmdb.org/t/p/")) {
      return raw;
    }
    const sizes = ["original", "w1280", "w780", "w500"];
    const target = sizes.includes(preferredSize) ? preferredSize : "w1280";
    return raw.replace(/\/t\/p\/(original|w\d+)\//, `/t/p/${target}/`);
  }

  function setStatus(message, kind) {
    statusEl.className = "status " + kind;
    statusEl.textContent = message;
  }

  function setQuickAddStatus(message, kind) {
    if (!quickAddStatusEl) {
      return;
    }
    quickAddStatusEl.className = "status " + kind;
    quickAddStatusEl.textContent = message;
  }

  function selectedCategorieIds() {
    if (!categorieBox) {
      return [];
    }
    return Array.from(categorieBox.querySelectorAll("input[type='checkbox']:checked"))
      .map((x) => Number(x.value))
      .filter((x) => x > 0);
  }

  function setSelectedCategorieIds(ids) {
    const set = new Set((ids || []).map((x) => Number(x)));
    categorieBox.querySelectorAll("input[type='checkbox']").forEach((input) => {
      input.checked = set.has(Number(input.value));
    });
  }

  function renderCategorieOptions() {
    if (!categorieBox) {
      return;
    }
    if (!Array.isArray(categorie) || categorie.length === 0) {
      categorieBox.innerHTML = "<span class='subtle'>Nessuna categoria disponibile.</span>";
      return;
    }

    categorieBox.innerHTML = categorie
      .map((c) => `
        <label class="category-option">
          <input type="checkbox" value="${c.id}">
          <span>${c.nome}</span>
        </label>
      `)
      .join("");
  }

  async function loadCategorie() {
    try {
      const items = await window.ApiClient.get("/categorie");
      categorie = Array.isArray(items) ? items : [];
      renderCategorieOptions();
    } catch {
      categorie = [];
      renderCategorieOptions();
    }
  }

  function resetForm() {
    editingId = null;
    form.reset();
    if (prefillRegistaId) {
      form.registaId.value = String(prefillRegistaId);
    }
    setSelectedCategorieIds([]);
    submitBtn.textContent = "Crea film";
    cancelBtn.classList.add("hidden");
  }

  function validate(payload) {
    if (!payload.titolo) {
      return "Titolo obbligatorio";
    }
    if (!payload.registaId || payload.registaId <= 0) {
      return "Regista non valido";
    }
    if (!payload.durata || payload.durata <= 0) {
      return "Durata deve essere > 0";
    }
    if (!payload.dataProduzione) {
      return "Data di produzione obbligatoria";
    }
    return null;
  }

  function renderRows(items) {
    if (!items.length) {
      tableBody.innerHTML = "<tr><td colspan='13' class='subtle'>Nessun film presente in catalogo.</td></tr>";
      return;
    }

    tableBody.innerHTML = items
      .map(
        (f) => `
      <tr>
        <td>${f.id}</td>
        <td>
          ${f.copertinaPath
            ? `<img class="thumb-poster" src="${f.copertinaPath}" alt="Copertina ${f.titolo || "film"}" onerror="this.style.display='none';this.nextElementSibling.classList.add('show');"> <span class="subtle media-fallback">n/d</span>`
            : `<span class="subtle">n/d</span>`}
        </td>
        <td>${f.titolo || ""}</td>
        <td>${(f.dataProduzione || "").slice(0, 10)}</td>
        <td>${(f.dataUscita || "").slice(0, 10) || "-"}</td>
        <td>${f.registaId}</td>
        <td>${f.durata}</td>
        <td>${f.tmdbMovieId || "-"}</td>
        <td>${f.filmatoPath ? `<a href="${f.filmatoPath}" target="_blank" rel="noopener noreferrer">Apri</a>` : "-"}</td>
        <td class="subtle">${f.copertinaPath || "-"}</td>
        <td>${Array.isArray(f.categorie) && f.categorie.length ? f.categorie.join(", ") : "-"}</td>
        <td>
          <div class="actions">
            <button class="btn-small secondary" data-action="edit" data-id="${f.id}">Modifica</button>
            <button class="btn-small danger" data-action="delete" data-id="${f.id}">Elimina</button>
          </div>
        </td>
      </tr>
    `
      )
      .join("");
  }

  async function loadFilms() {
    setStatus("Caricamento film...", "info");
    try {
      const items = await window.ApiClient.get("/films");
      renderRows(Array.isArray(items) ? items : []);
      setStatus("Catalogo film aggiornato.", "success");
    } catch (error) {
      tableBody.innerHTML = "";
      setStatus(`Errore: ${error.message}`, "error");
    }
  }

  async function submitForm(event) {
    event.preventDefault();

    const payload = {
      titolo: form.titolo.value.trim(),
      titoloOriginale: form.titoloOriginale.value.trim() || null,
      dataProduzione: form.dataProduzione.value,
      dataUscita: form.dataUscita.value || null,
      registaId: Number(form.registaId.value),
      durata: Number(form.durata.value),
      copertinaPath: form.copertinaPath.value.trim() || null,
      backdropPath: form.backdropPath.value.trim() || null,
      filmatoPath: form.filmatoPath.value.trim() || null,
      descrizioneLunga: form.descrizioneLunga.value.trim() || null,
      castPrincipale: form.castPrincipale.value.trim() || null,
      tmdbMovieId: form.tmdbMovieId.value ? Number(form.tmdbMovieId.value) : null,
      categorieIds: selectedCategorieIds()
    };

    const validationError = validate(payload);
    if (validationError) {
      setStatus(validationError, "error");
      return;
    }

    try {
      if (editingId) {
        await window.ApiClient.put(`/films/${editingId}`, payload);
        setStatus("Film aggiornato.", "success");
      } else {
        await window.ApiClient.post("/films", payload);
        setStatus("Film creato.", "success");
      }
      resetForm();
      await loadFilms();
    } catch (error) {
      setStatus(`Errore: ${error.message}`, "error");
    }
  }

  async function handleTableClick(event) {
    const button = event.target.closest("button[data-action]");
    if (!button) {
      return;
    }
    const id = button.dataset.id;
    const action = button.dataset.action;

    switch (action) {
      case "edit":
        try {
          const film = await window.ApiClient.get(`/films/${id}`);
          editingId = film.id;
          form.titolo.value = film.titolo || "";
          form.titoloOriginale.value = film.titoloOriginale || "";
          form.dataProduzione.value = (film.dataProduzione || "").slice(0, 10);
          form.dataUscita.value = (film.dataUscita || "").slice(0, 10);
          form.registaId.value = film.registaId || "";
          form.durata.value = film.durata || "";
          form.copertinaPath.value = film.copertinaPath || "";
          form.backdropPath.value = film.backdropPath || "";
          form.filmatoPath.value = film.filmatoPath || "";
          form.descrizioneLunga.value = film.descrizioneLunga || "";
          form.castPrincipale.value = film.castPrincipale || "";
          form.tmdbMovieId.value = film.tmdbMovieId || "";
          setSelectedCategorieIds(film.categorieIds || []);
          submitBtn.textContent = "Salva modifiche";
          cancelBtn.classList.remove("hidden");
          setStatus(`Modifica film #${id}`, "info");
        } catch (error) {
          setStatus(`Errore: ${error.message}`, "error");
        }
        return;
      case "delete": {
        const confirmed = window.confirm(`Confermi l'eliminazione del film ${id}?`);
        if (!confirmed) {
          return;
        }
        try {
          await window.ApiClient.delete(`/films/${id}`);
          setStatus("Film eliminato.", "success");
          await loadFilms();
        } catch (error) {
          setStatus(`Errore: ${error.message}`, "error");
        }
        return;
      }
      default:
        return;
    }
  }

  function openQuickAddModal() {
    if (!quickAddModal) {
      return;
    }
    quickAddModal.classList.remove("hidden");
    document.body.classList.add("modal-open");
  }

  function closeQuickAddModal() {
    if (!quickAddModal) {
      return;
    }
    quickAddModal.classList.add("hidden");
    document.body.classList.remove("modal-open");
    quickSelection.clear();
  }

  function toggleQuickSelection(tmdbId, disabled) {
    if (!tmdbId || disabled) {
      return;
    }

    if (quickSelection.has(tmdbId)) {
      quickSelection.delete(tmdbId);
    } else {
      quickSelection.add(tmdbId);
    }
    renderQuickAddItems(quickAddItems);
  }

  function renderQuickAddItems(items) {
    if (!quickAddListEl) {
      return;
    }

    if (!Array.isArray(items) || items.length === 0) {
      quickAddListEl.innerHTML = "<p class='subtle'>Nessuna uscita disponibile.</p>";
      return;
    }

    quickAddListEl.innerHTML = items.map((item) => {
      const release = item.dataUscita ? String(item.dataUscita).slice(0, 10) : "Data non disponibile";
      const disabled = !!item.alreadyInCatalog;
    const selected = quickSelection.has(item.tmdbMovieId);
      const disabledAttr = disabled ? "disabled" : "";
      const badge = item.alreadyInCatalog ? "<span class='tag info'>Gia presente</span>" : "<span class='tag accent'>Nuovo</span>";
      const poster = item.posterPath
        ? `<img src="${normalizeTmdbImage(item.posterPath, "w500")}" alt="Poster ${item.titolo}" style="width:100%;height:220px;object-fit:cover;border-radius:8px;">`
        : `<div class="card-media"><span>Poster non disponibile</span></div>`;

      return `
        <article class="card quick-glass-card quick-add-card ${selected ? "selected" : ""} ${disabled ? "disabled" : ""}" data-quick-card-id="${item.tmdbMovieId}" data-selected="${selected ? "1" : "0"}" ${disabledAttr}>
          <div class="card-body">
            <div style="width:100%;">
              ${poster}
              <h3 style="margin-top:10px;">${item.titolo || "Titolo non disponibile"}</h3>
              <p class="subtle">${release}</p>
              <div class="actions">${badge}</div>
            </div>
          </div>
        </article>
      `;
    }).join("");
  }

  async function loadQuickAddItems() {
    setQuickAddStatus("Caricamento ultime uscite TMDB...", "info");
    try {
      const response = await window.ApiClient.get("/tmdb/latest?limit=20&page=1");
      quickAddItems = Array.isArray(response.items) ? response.items : [];
      quickSelection.clear();
      renderQuickAddItems(quickAddItems);
      setQuickAddStatus(`Caricati ${quickAddItems.length} titoli da TMDB.`, "success");
    } catch (error) {
      quickAddItems = [];
      renderQuickAddItems([]);
      setQuickAddStatus(`Errore TMDB: ${error.message}`, "error");
    }
  }

  async function submitQuickAdd() {
    if (!quickAddListEl) {
      return;
    }

    const selectedIds = Array.from(quickSelection.values())
      .map((x) => Number(x))
      .filter((x) => x > 0);

    if (!selectedIds.length) {
      setQuickAddStatus("Seleziona almeno un film da importare.", "error");
      return;
    }

    setQuickAddStatus("Importazione in corso...", "info");
    try {
      const result = await window.ApiClient.post("/tmdb/import-latest", { tmdbMovieIds: selectedIds });
      const created = Number(result.created || 0);
      const skipped = Number(result.skippedExisting || 0);
      const failed = Number(result.failed || 0);
      setQuickAddStatus(`Import completato: aggiunti ${created}, gia presenti ${skipped}, errori ${failed}.`, "success");
      setStatus(`Aggiunta rapida completata: ${created} nuovi film in catalogo.`, "success");
      await loadFilms();
      await loadQuickAddItems();
    } catch (error) {
      setQuickAddStatus(`Errore import: ${error.message}`, "error");
    }
  }

  function bindQuickAddEvents() {
    if (quickAddBtn) {
      quickAddBtn.addEventListener("click", async () => {
        openQuickAddModal();
        await loadQuickAddItems();
      });
    }

    if (quickAddCloseBtn) {
      quickAddCloseBtn.addEventListener("click", closeQuickAddModal);
    }

    if (quickAddModal) {
      quickAddModal.addEventListener("click", (event) => {
        if (event.target === quickAddModal) {
          closeQuickAddModal();
        }
      });
    }

    if (quickAddListEl) {
      quickAddListEl.addEventListener("click", (event) => {
        const card = event.target.closest("[data-quick-card-id]");
        if (!card) {
          return;
        }
        const tmdbId = Number(card.getAttribute("data-quick-card-id"));
        const disabled = card.hasAttribute("disabled") || card.classList.contains("disabled");
        toggleQuickSelection(tmdbId, disabled);
      });
    }

    if (quickAddSubmitBtn) {
      quickAddSubmitBtn.addEventListener("click", submitQuickAdd);
    }
  }

  async function initFilmsPage() {
    if (!form || !tableBody) {
      return;
    }

    const params = new URLSearchParams(window.location.search);
    const queryRegistaId = Number(params.get("registaId"));
    if (Number.isInteger(queryRegistaId) && queryRegistaId > 0) {
      prefillRegistaId = queryRegistaId;
      form.registaId.value = String(prefillRegistaId);
      setStatus(`Creazione film per regista #${prefillRegistaId}.`, "info");
    }

    form.addEventListener("submit", submitForm);
    tableBody.addEventListener("click", handleTableClick);
    cancelBtn.addEventListener("click", resetForm);

    const tmdbSyncButton = document.getElementById("film-tmdb-sync");
    if (tmdbSyncButton) {
      tmdbSyncButton.addEventListener("click", async () => {
        if (!editingId) {
          setStatus("Apri prima un film in modifica per sincronizzarlo da TMDB.", "info");
          return;
        }
        setStatus("Sincronizzazione TMDB in corso...", "info");
        try {
          await window.ApiClient.post(`/tmdb/sync/film/${editingId}`, {});
          setStatus("Sincronizzazione TMDB completata.", "success");
          const reloaded = await window.ApiClient.get(`/films/${editingId}`);
          form.titoloOriginale.value = reloaded.titoloOriginale || "";
          form.dataUscita.value = (reloaded.dataUscita || "").slice(0, 10);
          form.backdropPath.value = normalizeTmdbImage(reloaded.backdropPath || "", "w1280");
          form.filmatoPath.value = reloaded.filmatoPath || "";
          form.descrizioneLunga.value = reloaded.descrizioneLunga || "";
          form.castPrincipale.value = reloaded.castPrincipale || "";
          form.tmdbMovieId.value = reloaded.tmdbMovieId || "";
          await loadFilms();
        } catch (error) {
          setStatus(`Errore TMDB: ${error.message}`, "error");
        }
      });
    }

    bindQuickAddEvents();
    await loadCategorie();
    await loadFilms();
  }

  window.initFilmsPage = initFilmsPage;
})();
