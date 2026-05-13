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

  async function loadCinemasIntoSelect(selectEl, selectedId) {
    try {
      const cinemas = await window.ApiClient.get("/cinemas");
      selectEl.innerHTML = "<option value=''>Seleziona cinema</option>";
      (Array.isArray(cinemas) ? cinemas : []).forEach((c) => {
        const option = document.createElement("option");
        option.value = String(c.id);
        option.textContent = `${c.id} - ${c.nome}`;
        selectEl.appendChild(option);
      });
      if (selectedId) selectEl.value = String(selectedId);
    } catch (error) {
      setStatus(`Errore caricamento cinema: ${error.message}`, "error");
    }
  }

  async function loadCinemas() {
    await loadCinemasIntoSelect(cinemaSelect, null);
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

  async function openEditModal(sala) {
    var content = `
      <h3>Modifica sala #${sala.id}</h3>
      <form id="modal-edit-form">
        <label>Cinema</label>
        <select id="modal-cinemaId" required></select>
        <label>Numero progressivo</label>
        <input id="modal-numeroProgressivo" type="number" value="${sala.numeroProgressivo || ""}" required>
        <label>Tipologia</label>
        <select id="modal-tipologia">
          <option value="2D" ${sala.tipologia === "2D" ? "selected" : ""}>2D</option>
          <option value="3D" ${sala.tipologia === "3D" ? "selected" : ""}>3D</option>
          <option value="ISENSE" ${sala.tipologia === "ISENSE" ? "selected" : ""}>ISENSE</option>
          <option value="XL" ${sala.tipologia === "XL" ? "selected" : ""}>XL</option>
        </select>
        <label>Nome</label>
        <input id="modal-nome" type="text" value="${sala.nome || ""}">
        <label>Numero file</label>
        <input id="modal-numeroFile" type="number" value="${sala.numeroFile || 10}" required>
        <label>Posti per fila</label>
        <input id="modal-postiPerFila" type="number" value="${sala.postiPerFila || 12}" required>
        <label>Mappa posti (JSON)</label>
        <input id="modal-mappaPostiJson" type="text" value="${sala.mappaPostiJson || ""}">
        <label>Stato</label>
        <select id="modal-attiva">
          <option value="true" ${sala.attiva !== false ? "selected" : ""}>Attiva</option>
          <option value="false" ${sala.attiva === false ? "selected" : ""}>Non attiva</option>
        </select>
        <div class="actions" style="margin-top:1rem">
          <button type="submit" class="button primary">Salva modifiche</button>
          <button type="button" class="button secondary" id="modal-cancel-btn">Annulla</button>
        </div>
      </form>`;

    var card = window.ModalUtils.open(content);
    if (!card) return;

    await loadCinemasIntoSelect(document.getElementById("modal-cinemaId"), sala.cinemaId);

    card.querySelector("#modal-cancel-btn").addEventListener("click", function () {
      if (window.confirm("Annullare le modifiche? I dati non verranno salvati.")) {
        window.ModalUtils.close();
      }
    });

    card.querySelector("#modal-edit-form").addEventListener("submit", async function (e) {
      e.preventDefault();
      var payload = {
        cinemaId: Number(document.getElementById("modal-cinemaId").value),
        numeroProgressivo: Number(document.getElementById("modal-numeroProgressivo").value),
        tipologia: document.getElementById("modal-tipologia").value,
        nome: document.getElementById("modal-nome").value.trim(),
        numeroFile: Number(document.getElementById("modal-numeroFile").value),
        postiPerFila: Number(document.getElementById("modal-postiPerFila").value),
        mappaPostiJson: document.getElementById("modal-mappaPostiJson").value.trim(),
        attiva: String(document.getElementById("modal-attiva").value) !== "false"
      };
      if (!payload.cinemaId || !payload.numeroProgressivo || !payload.tipologia || !payload.numeroFile || !payload.postiPerFila) {
        alert("Compila tutti i campi obbligatori.");
        return;
      }
      if (!window.confirm("Salvare le modifiche?")) return;
      try {
        await window.ApiClient.put(`/sale/${sala.id}`, payload);
        window.ModalUtils.close();
        setStatus("Sala aggiornata.", "success");
        await loadSale();
      } catch (error) {
        alert("Errore: " + error.message);
      }
    });
  }

  async function handleTableClick(event) {
    const button = event.target.closest("button[data-action]");
    if (!button) return;
    const action = button.dataset.action;
    const id = Number(button.dataset.id);
    if (!id) return;

    if (action === "delete") {
      if (!window.confirm(`Confermi eliminazione sala #${id}?`)) return;
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
        openEditModal(sala);
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
