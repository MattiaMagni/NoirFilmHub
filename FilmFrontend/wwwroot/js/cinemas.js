(function () {
  let editingId = null;
  const tableBody = document.getElementById("cinemas-table-body");
  const form = document.getElementById("cinema-form");
  const statusEl = document.getElementById("cinemas-status");
  const submitBtn = document.getElementById("cinema-submit");
  const cancelBtn = document.getElementById("cinema-cancel");

  function setStatus(message, kind) {
    statusEl.className = "status " + kind;
    statusEl.textContent = message;
  }

  function resetForm() {
    editingId = null;
    form.reset();
    submitBtn.textContent = "Crea cinema";
    cancelBtn.classList.add("hidden");
  }

  function renderRows(items) {
    if (!items.length) {
      tableBody.innerHTML = "<tr><td colspan='9' class='subtle'>Nessun cinema trovato.</td></tr>";
      return;
    }

    tableBody.innerHTML = items
      .map(
        (c) => `
      <tr>
        <td>${c.id}</td>
        <td>${c.nome || ""}</td>
        <td>${c.indirizzo || ""}</td>
        <td>${c.citta || ""}</td>
        <td>${Number(c.capienza) || 0}</td>
        <td>${c.codiceLocale || "-"}</td>
        <td>${c.latitudine != null && c.longitudine != null ? `${Number(c.latitudine).toFixed(5)}, ${Number(c.longitudine).toFixed(5)}` : "-"}</td>
        <td>${c.attivo === false ? "Non attivo" : "Attivo"}</td>
        <td>
          <div class="actions">
            <button class="btn-small secondary" data-action="edit" data-id="${c.id}">Modifica</button>
            <button class="btn-small danger" data-action="delete" data-id="${c.id}">Elimina</button>
          </div>
        </td>
      </tr>
    `
      )
      .join("");
  }

  async function loadCinemas() {
    setStatus("Caricamento cinema...", "info");
    try {
      const items = await window.ApiClient.get("/cinemas");
      renderRows(Array.isArray(items) ? items : []);
      setStatus("Cinema caricati.", "success");
    } catch (error) {
      tableBody.innerHTML = "";
      setStatus(`Errore: ${error.message}`, "error");
    }
  }

  async function submitForm(event) {
    event.preventDefault();

    const payload = {
      nome: form.nome.value.trim(),
      indirizzo: form.indirizzo.value.trim(),
      citta: form.citta.value.trim(),
      capienza: Number(form.capienza.value),
      codiceLocale: form.codiceLocale.value.trim(),
      latitudine: form.latitudine.value ? Number(form.latitudine.value) : null,
      longitudine: form.longitudine.value ? Number(form.longitudine.value) : null,
      attivo: String(form.attivo.value) !== "false"
    };

    if (!payload.nome || !payload.indirizzo || !payload.citta || !payload.codiceLocale) {
      setStatus("Compila tutti i campi obbligatori.", "error");
      return;
    }

    if (!Number.isInteger(payload.capienza) || payload.capienza < 20 || payload.capienza > 500) {
      setStatus("Capienza non valida (20-500).", "error");
      return;
    }

    try {
      if (editingId) {
        await window.ApiClient.put(`/cinemas/${editingId}`, payload);
        setStatus("Cinema aggiornato.", "success");
      } else {
        await window.ApiClient.post("/cinemas", payload);
        setStatus("Cinema creato.", "success");
      }
      resetForm();
      await loadCinemas();
    } catch (error) {
      setStatus(`Errore: ${error.message}`, "error");
    }
  }

  function openEditModal(cinema) {
    var content = `
      <h3>Modifica cinema #${cinema.id}</h3>
      <form id="modal-edit-form">
        <label>Nome</label>
        <input id="modal-nome" type="text" value="${cinema.nome || ""}" required>
        <label>Indirizzo</label>
        <input id="modal-indirizzo" type="text" value="${cinema.indirizzo || ""}" required>
        <label>Citta</label>
        <input id="modal-citta" type="text" value="${cinema.citta || ""}" required>
        <label>Capienza</label>
        <input id="modal-capienza" type="number" value="${cinema.capienza || 120}" min="20" max="500" required>
        <label>Codice locale</label>
        <input id="modal-codiceLocale" type="text" value="${cinema.codiceLocale || ""}" required>
        <label>Latitudine</label>
        <input id="modal-latitudine" type="number" step="any" value="${cinema.latitudine ?? ""}">
        <label>Longitudine</label>
        <input id="modal-longitudine" type="number" step="any" value="${cinema.longitudine ?? ""}">
        <label>Stato</label>
        <select id="modal-attivo">
          <option value="true" ${cinema.attivo !== false ? "selected" : ""}>Attivo</option>
          <option value="false" ${cinema.attivo === false ? "selected" : ""}>Non attivo</option>
        </select>
        <div class="actions" style="margin-top:1rem">
          <button type="submit" class="button primary">Salva modifiche</button>
          <button type="button" class="button secondary" id="modal-cancel-btn">Annulla</button>
        </div>
      </form>`;

    var card = window.ModalUtils.open(content);
    if (!card) return;

    card.querySelector("#modal-cancel-btn").addEventListener("click", function () {
      if (window.confirm("Annullare le modifiche? I dati non verranno salvati.")) {
        window.ModalUtils.close();
      }
    });

    card.querySelector("#modal-edit-form").addEventListener("submit", async function (e) {
      e.preventDefault();
      var payload = {
        nome: document.getElementById("modal-nome").value.trim(),
        indirizzo: document.getElementById("modal-indirizzo").value.trim(),
        citta: document.getElementById("modal-citta").value.trim(),
        capienza: Number(document.getElementById("modal-capienza").value),
        codiceLocale: document.getElementById("modal-codiceLocale").value.trim(),
        latitudine: document.getElementById("modal-latitudine").value ? Number(document.getElementById("modal-latitudine").value) : null,
        longitudine: document.getElementById("modal-longitudine").value ? Number(document.getElementById("modal-longitudine").value) : null,
        attivo: String(document.getElementById("modal-attivo").value) !== "false"
      };
      if (!payload.nome || !payload.indirizzo || !payload.citta || !payload.codiceLocale) {
        alert("Compila tutti i campi obbligatori.");
        return;
      }
      if (!Number.isInteger(payload.capienza) || payload.capienza < 20 || payload.capienza > 500) {
        alert("Capienza non valida (20-500).");
        return;
      }
      if (!window.confirm("Salvare le modifiche?")) return;
      try {
        await window.ApiClient.put(`/cinemas/${cinema.id}`, payload);
        window.ModalUtils.close();
        setStatus("Cinema aggiornato.", "success");
        await loadCinemas();
      } catch (error) {
        alert("Errore: " + error.message);
      }
    });
  }

  async function handleTableClick(event) {
    const button = event.target.closest("button[data-action]");
    if (!button) return;
    const id = button.dataset.id;
    const action = button.dataset.action;

    switch (action) {
      case "edit":
        try {
          const cinema = await window.ApiClient.get(`/cinemas/${id}`);
          openEditModal(cinema);
        } catch (error) {
          setStatus(`Errore: ${error.message}`, "error");
        }
        return;
      case "delete": {
        const confirmed = window.confirm(`Confermi eliminazione del cinema ${id}?`);
        if (!confirmed) return;
        try {
          await window.ApiClient.delete(`/cinemas/${id}`);
          setStatus("Cinema eliminato.", "success");
          await loadCinemas();
        } catch (error) {
          setStatus(`Errore: ${error.message}`, "error");
        }
        return;
      }
      default:
        return;
    }
  }

  function initCinemasPage() {
    if (!form || !tableBody) return;
    form.addEventListener("submit", submitForm);
    tableBody.addEventListener("click", handleTableClick);
    cancelBtn.addEventListener("click", resetForm);
    loadCinemas();
  }

  window.initCinemasPage = initCinemasPage;
})();
