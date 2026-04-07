(function () {
  let editingId = null;
  const tableBody = document.getElementById("proiezioni-table-body");
  const form = document.getElementById("proiezione-form");
  const statusEl = document.getElementById("proiezioni-status");
  const submitBtn = document.getElementById("proiezione-submit");
  const cancelBtn = document.getElementById("proiezione-cancel");
  const cinemaSelect = document.getElementById("cinemaId");
  const filmSelect = document.getElementById("filmId");

  function setStatus(message, kind) {
    statusEl.className = "status " + kind;
    statusEl.textContent = message;
  }

  function resetForm() {
    editingId = null;
    form.reset();
    submitBtn.textContent = "Crea proiezione";
    cancelBtn.classList.add("hidden");
  }

  function formatDate(value) {
    if (!value) {
      return "";
    }
    return value.slice(0, 10);
  }

  function formatTimeForInput(value) {
    if (!value) {
      return "";
    }
    const date = new Date(value);
    const hh = String(date.getHours()).padStart(2, "0");
    const mm = String(date.getMinutes()).padStart(2, "0");
    return `${hh}:${mm}`;
  }

  function toTimeIso(timeValue) {
    const parts = String(timeValue).split(":");
    const hh = Number(parts[0] || 0);
    const mm = Number(parts[1] || 0);
    return new Date(Date.UTC(1, 0, 1, hh, mm, 0)).toISOString();
  }

  function renderRows(items) {
    if (!items.length) {
      tableBody.innerHTML = "<tr><td colspan='6' class='subtle'>Nessuna proiezione trovata.</td></tr>";
      return;
    }

    tableBody.innerHTML = items
      .map(
        (p) => `
      <tr>
        <td>${p.id}</td>
        <td>${p.cinemaId}</td>
        <td>${p.filmId}</td>
        <td>${formatDate(p.data)}</td>
        <td>${formatTimeForInput(p.ora)}</td>
        <td>
          <div class="actions">
            <button class="btn-small secondary" data-action="edit" data-id="${p.id}">Modifica</button>
            <button class="btn-small danger" data-action="delete" data-id="${p.id}">Elimina</button>
          </div>
        </td>
      </tr>
    `
      )
      .join("");
  }

  async function populateFkSelects() {
    try {
      const [cinemas, films] = await Promise.all([
        window.ApiClient.get("/cinemas"),
        window.ApiClient.get("/films")
      ]);

      cinemaSelect.innerHTML = "<option value=''>Seleziona cinema</option>";
      (Array.isArray(cinemas) ? cinemas : []).forEach((c) => {
        const option = document.createElement("option");
        option.value = String(c.id);
        option.textContent = `${c.id} - ${c.nome}`;
        cinemaSelect.appendChild(option);
      });

      filmSelect.innerHTML = "<option value=''>Seleziona film</option>";
      (Array.isArray(films) ? films : []).forEach((f) => {
        const option = document.createElement("option");
        option.value = String(f.id);
        option.textContent = `${f.id} - ${f.titolo}`;
        filmSelect.appendChild(option);
      });
    } catch (error) {
      setStatus(`Errore caricamento riferimenti: ${error.message}`, "error");
    }
  }

  async function loadProiezioni() {
    setStatus("Caricamento proiezioni...", "info");
    try {
      const items = await window.ApiClient.get("/proiezioni");
      renderRows(Array.isArray(items) ? items : []);
      setStatus("Proiezioni caricate.", "success");
    } catch (error) {
      tableBody.innerHTML = "";
      setStatus(`Errore: ${error.message}`, "error");
    }
  }

  async function submitForm(event) {
    event.preventDefault();

    const cinemaId = Number(form.cinemaId.value);
    const filmId = Number(form.filmId.value);
    const data = form.data.value;
    const ora = form.ora.value;

    if (!cinemaId || !filmId || !data || !ora) {
      setStatus("Compila tutti i campi obbligatori.", "error");
      return;
    }

    const payload = {
      cinemaId,
      filmId,
      data,
      ora: toTimeIso(ora)
    };

    try {
      if (editingId) {
        await window.ApiClient.put(`/proiezioni/${editingId}`, payload);
        setStatus("Proiezione aggiornata.", "success");
      } else {
        await window.ApiClient.post("/proiezioni", payload);
        setStatus("Proiezione creata.", "success");
      }
      resetForm();
      await loadProiezioni();
    } catch (error) {
      if (error.status === 409) {
        setStatus("Conflitto: proiezione duplicata.", "error");
        return;
      }
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
        const p = await window.ApiClient.get(`/proiezioni/${id}`);
        editingId = p.id;
        form.cinemaId.value = String(p.cinemaId || "");
        form.filmId.value = String(p.filmId || "");
        form.data.value = formatDate(p.data);
        form.ora.value = formatTimeForInput(p.ora);
        submitBtn.textContent = "Salva modifiche";
        cancelBtn.classList.remove("hidden");
        setStatus(`Modifica proiezione #${id}`, "info");
      } catch (error) {
        setStatus(`Errore: ${error.message}`, "error");
      }
    }

    if (action === "delete") {
      const confirmed = window.confirm(`Confermi eliminazione della proiezione ${id}?`);
      if (!confirmed) {
        return;
      }
      try {
        await window.ApiClient.delete(`/proiezioni/${id}`);
        setStatus("Proiezione eliminata.", "success");
        await loadProiezioni();
      } catch (error) {
        setStatus(`Errore: ${error.message}`, "error");
      }
    }
  }

  async function initProiezioniPage() {
    if (!form || !tableBody) {
      return;
    }
    form.addEventListener("submit", submitForm);
    tableBody.addEventListener("click", handleTableClick);
    cancelBtn.addEventListener("click", resetForm);

    await populateFkSelects();
    await loadProiezioni();
  }

  window.initProiezioniPage = initProiezioniPage;
})();
