(function () {
  const statusEl = document.getElementById("sale-status");
  const form = document.getElementById("sala-form");
  const tableBody = document.getElementById("sale-table-body");
  const cinemaSelect = document.getElementById("sala-cinema-id");
  const submitBtn = document.getElementById("sala-submit");
  const cancelBtn = document.getElementById("sala-cancel");

  let editingId = null;

  function setStatus(message, kind) {
    statusEl.className = `status ${kind}`;
    statusEl.textContent = message;
  }

  function resetForm() {
    editingId = null;
    form.reset();
    submitBtn.textContent = "Crea sala";
    cancelBtn.classList.add("hidden");
  }

  async function loadCinemas() {
    try {
      const cinemas = await window.ApiClient.get("/cinemas");
      cinemaSelect.innerHTML = "<option value=''>Seleziona cinema</option>";
      (Array.isArray(cinemas) ? cinemas : []).forEach((c) => {
        const option = document.createElement("option");
        option.value = String(c.id);
        option.textContent = `${c.id} - ${c.nome}`;
        cinemaSelect.appendChild(option);
      });
    } catch (error) {
      setStatus(`Errore caricamento cinema: ${error.message}`, "error");
    }
  }

  function renderRows(items) {
    if (!items.length) {
      tableBody.innerHTML = "<tr><td colspan='9' class='subtle'>Nessuna sala trovata.</td></tr>";
      return;
    }

    tableBody.innerHTML = items
      .map((s) => `
        <tr>
          <td>${s.id}</td>
          <td>${s.cinemaId}</td>
          <td>${s.numeroProgressivo}</td>
          <td>${s.tipologia}</td>
          <td>${s.nome || "-"}</td>
          <td>${s.numeroFile}x${s.postiPerFila}</td>
          <td>${s.attiva ? "Attiva" : "Non attiva"}</td>
          <td class="subtle">${s.mappaPostiJson ? "Definita" : "Default"}</td>
          <td>
            <div class="actions">
              <button class="btn-small secondary" data-action="edit" data-id="${s.id}">Modifica</button>
              <button class="btn-small danger" data-action="delete" data-id="${s.id}">Elimina</button>
            </div>
          </td>
        </tr>
      `)
      .join("");
  }

  async function loadSale() {
    setStatus("Caricamento sale...", "info");
    try {
      const items = await window.ApiClient.get("/sale");
      renderRows(Array.isArray(items) ? items : []);
      setStatus("Sale caricate.", "success");
    } catch (error) {
      tableBody.innerHTML = "";
      setStatus(`Errore caricamento sale: ${error.message}`, "error");
    }
  }

  async function submitForm(event) {
    event.preventDefault();
    const payload = {
      cinemaId: Number(form.cinemaId.value),
      numeroProgressivo: Number(form.numeroProgressivo.value),
      tipologia: form.tipologia.value,
      nome: form.nome.value.trim(),
      numeroFile: Number(form.numeroFile.value),
      postiPerFila: Number(form.postiPerFila.value),
      mappaPostiJson: form.mappaPostiJson.value.trim(),
      attiva: String(form.attiva.value) !== "false"
    };

    if (!payload.cinemaId || !payload.numeroProgressivo || !payload.tipologia || !payload.numeroFile || !payload.postiPerFila) {
      setStatus("Compila tutti i campi obbligatori.", "error");
      return;
    }

    try {
      if (editingId) {
        await window.ApiClient.put(`/sale/${editingId}`, payload);
        setStatus("Sala aggiornata.", "success");
      } else {
        await window.ApiClient.post("/sale", payload);
        setStatus("Sala creata.", "success");
      }

      resetForm();
      await loadSale();
    } catch (error) {
      setStatus(`Errore salvataggio sala: ${error.message}`, "error");
    }
  }

  async function handleTableClick(event) {
    const button = event.target.closest("button[data-action]");
    if (!button) {
      return;
    }
    const action = button.dataset.action;
    const id = Number(button.dataset.id);
    if (!id) {
      return;
    }

    if (action === "delete") {
      if (!window.confirm(`Confermi eliminazione sala #${id}?`)) {
        return;
      }
      try {
        await window.ApiClient.delete(`/sale/${id}`);
        setStatus("Sala eliminata.", "success");
        await loadSale();
      } catch (error) {
        setStatus(`Errore eliminazione sala: ${error.message}`, "error");
      }
      return;
    }

    if (action === "edit") {
      try {
        const sala = await window.ApiClient.get(`/sale/${id}`);
        editingId = sala.id;
        form.cinemaId.value = String(sala.cinemaId || "");
        form.numeroProgressivo.value = sala.numeroProgressivo || "";
        form.tipologia.value = sala.tipologia || "2D";
        form.nome.value = sala.nome || "";
        form.numeroFile.value = sala.numeroFile || 10;
        form.postiPerFila.value = sala.postiPerFila || 12;
        form.mappaPostiJson.value = sala.mappaPostiJson || "";
        form.attiva.value = String(sala.attiva !== false);
        submitBtn.textContent = "Salva modifiche";
        cancelBtn.classList.remove("hidden");
        setStatus(`Modifica sala #${id}`, "info");
      } catch (error) {
        setStatus(`Errore caricamento sala: ${error.message}`, "error");
      }
    }
  }

  async function initSalePage() {
    await loadCinemas();
    await loadSale();

    form.addEventListener("submit", submitForm);
    cancelBtn.addEventListener("click", resetForm);
    tableBody.addEventListener("click", handleTableClick);
  }

  window.initSalePage = initSalePage;
})();
