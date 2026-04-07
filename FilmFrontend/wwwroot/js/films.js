(function () {
  let editingId = null;
  let categorie = [];
  let prefillRegistaId = null;

  const tableBody = document.getElementById("films-table-body");
  const form = document.getElementById("film-form");
  const statusEl = document.getElementById("films-status");
  const submitBtn = document.getElementById("film-submit");
  const cancelBtn = document.getElementById("film-cancel");
  const categorieBox = document.getElementById("film-categorie-options");

  function setStatus(message, kind) {
    statusEl.className = "status " + kind;
    statusEl.textContent = message;
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
        <label style="display:inline-flex;align-items:center;gap:6px;">
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
      return "RegistaId non valido";
    }
    if (!payload.durata || payload.durata <= 0) {
      return "Durata deve essere > 0";
    }
    if (!payload.dataProduzione) {
      return "DataProduzione obbligatoria";
    }
    return null;
  }

  function renderRows(items) {
    if (!items.length) {
      tableBody.innerHTML = "<tr><td colspan='10' class='subtle'>Nessun film trovato.</td></tr>";
      return;
    }

    tableBody.innerHTML = items
      .map(
        (f) => `
      <tr>
        <td>${f.id}</td>
        <td>
          ${f.copertinaPath
            ? `<img class="thumb-poster" src="${f.copertinaPath}" alt="Copertina ${f.titolo || "film"}" onerror="this.style.display='none';this.nextElementSibling.classList.remove('hidden');"> <span class="subtle hidden">n/d</span>`
            : `<span class="subtle">n/d</span>`}
        </td>
        <td>${f.titolo || ""}</td>
        <td>${(f.dataProduzione || "").slice(0, 10)}</td>
        <td>${f.registaId}</td>
        <td>${f.durata}</td>
        <td class="subtle">${f.copertinaPath || "-"}</td>
        <td>${f.filmatoPath || "-"}</td>
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
      setStatus("Film caricati.", "success");
    } catch (error) {
      tableBody.innerHTML = "";
      setStatus(`Errore: ${error.message}`, "error");
    }
  }

  async function submitForm(event) {
    event.preventDefault();

    const payload = {
      titolo: form.titolo.value.trim(),
      dataProduzione: form.dataProduzione.value,
      registaId: Number(form.registaId.value),
      durata: Number(form.durata.value),
      copertinaPath: form.copertinaPath.value.trim() || null,
      filmatoPath: form.filmatoPath.value.trim() || null,
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

    if (action === "edit") {
      try {
        const film = await window.ApiClient.get(`/films/${id}`);
        editingId = film.id;
        form.titolo.value = film.titolo || "";
        form.dataProduzione.value = (film.dataProduzione || "").slice(0, 10);
        form.registaId.value = film.registaId || "";
        form.durata.value = film.durata || "";
        form.copertinaPath.value = film.copertinaPath || "";
        form.filmatoPath.value = film.filmatoPath || "";
        setSelectedCategorieIds(film.categorieIds || []);
        submitBtn.textContent = "Salva modifiche";
        cancelBtn.classList.remove("hidden");
        setStatus(`Modifica film #${id}`, "info");
      } catch (error) {
        setStatus(`Errore: ${error.message}`, "error");
      }
    }

    if (action === "delete") {
      const confirmed = window.confirm(`Confermi eliminazione del film ${id}?`);
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
    await loadCategorie();
    await loadFilms();
  }

  window.initFilmsPage = initFilmsPage;
})();
