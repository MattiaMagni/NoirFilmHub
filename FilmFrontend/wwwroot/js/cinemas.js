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
      tableBody.innerHTML = "<tr><td colspan='5' class='subtle'>Nessun cinema trovato.</td></tr>";
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
      citta: form.citta.value.trim()
    };

    if (!payload.nome || !payload.indirizzo || !payload.citta) {
      setStatus("Compila tutti i campi obbligatori.", "error");
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

  async function handleTableClick(event) {
    const button = event.target.closest("button[data-action]");
    if (!button) {
      return;
    }
    const id = button.dataset.id;
    const action = button.dataset.action;

    if (action === "edit") {
      try {
        const cinema = await window.ApiClient.get(`/cinemas/${id}`);
        editingId = cinema.id;
        form.nome.value = cinema.nome || "";
        form.indirizzo.value = cinema.indirizzo || "";
        form.citta.value = cinema.citta || "";
        submitBtn.textContent = "Salva modifiche";
        cancelBtn.classList.remove("hidden");
        setStatus(`Modifica cinema #${id}`, "info");
      } catch (error) {
        setStatus(`Errore: ${error.message}`, "error");
      }
    }

    if (action === "delete") {
      const confirmed = window.confirm(`Confermi eliminazione del cinema ${id}?`);
      if (!confirmed) {
        return;
      }
      try {
        await window.ApiClient.delete(`/cinemas/${id}`);
        setStatus("Cinema eliminato.", "success");
        await loadCinemas();
      } catch (error) {
        setStatus(`Errore: ${error.message}`, "error");
      }
    }
  }

  function initCinemasPage() {
    if (!form || !tableBody) {
      return;
    }
    form.addEventListener("submit", submitForm);
    tableBody.addEventListener("click", handleTableClick);
    cancelBtn.addEventListener("click", resetForm);
    loadCinemas();
  }

  window.initCinemasPage = initCinemasPage;
})();
