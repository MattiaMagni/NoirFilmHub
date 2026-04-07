(function () {
  let editingId = null;

  const tableBody = document.getElementById("registi-table-body");
  const form = document.getElementById("regista-form");
  const statusEl = document.getElementById("registi-status");
  const submitBtn = document.getElementById("regista-submit");
  const cancelBtn = document.getElementById("regista-cancel");
  const relatedFilms = document.getElementById("regista-films");

  function setStatus(message, kind) {
    statusEl.className = "status " + kind;
    statusEl.textContent = message;
  }

  function resetForm() {
    editingId = null;
    form.reset();
    submitBtn.textContent = "Crea regista";
    cancelBtn.classList.add("hidden");
  }

  function renderRows(items) {
    if (!items.length) {
      tableBody.innerHTML = "<tr><td colspan='5' class='subtle'>Nessun regista trovato.</td></tr>";
      return;
    }

    tableBody.innerHTML = items
      .map(
        (r) => `
      <tr>
        <td>${r.id}</td>
        <td>${r.nome || ""}</td>
        <td>${r.cognome || ""}</td>
        <td>${r.nazionalita || ""}</td>
        <td>
          <div class="actions">
            <button class="btn-small secondary" data-action="films" data-id="${r.id}">Film</button>
            <button class="btn-small secondary" data-action="edit" data-id="${r.id}">Modifica</button>
            <button class="btn-small danger" data-action="delete" data-id="${r.id}">Elimina</button>
          </div>
        </td>
      </tr>
    `
      )
      .join("");
  }

  async function loadRegisti() {
    setStatus("Caricamento registi...", "info");
    try {
      const items = await window.ApiClient.get("/registi");
      renderRows(Array.isArray(items) ? items : []);
      setStatus("Registi caricati.", "success");
    } catch (error) {
      tableBody.innerHTML = "";
      setStatus(`Errore: ${error.message}`, "error");
    }
  }

  async function submitForm(event) {
    event.preventDefault();

    const payload = {
      nome: form.nome.value.trim(),
      cognome: form.cognome.value.trim(),
      nazionalita: form.nazionalita.value.trim()
    };

    if (!payload.nome || !payload.cognome || !payload.nazionalita) {
      setStatus("Compila tutti i campi obbligatori.", "error");
      return;
    }

    try {
      if (editingId) {
        await window.ApiClient.put(`/registi/${editingId}`, payload);
        setStatus("Regista aggiornato.", "success");
      } else {
        await window.ApiClient.post("/registi", payload);
        setStatus("Regista creato.", "success");
      }
      resetForm();
      await loadRegisti();
    } catch (error) {
      setStatus(`Errore: ${error.message}`, "error");
    }
  }

  async function handleTableClick(event) {
    const button = event.target.closest("button[data-action]");
    if (!button) {
      return;
    }

    const action = button.dataset.action;
    const id = button.dataset.id;

    if (action === "edit") {
      try {
        const regista = await window.ApiClient.get(`/registi/${id}`);
        editingId = regista.id;
        form.nome.value = regista.nome || "";
        form.cognome.value = regista.cognome || "";
        form.nazionalita.value = regista.nazionalita || "";
        submitBtn.textContent = "Salva modifiche";
        cancelBtn.classList.remove("hidden");
        setStatus(`Modifica regista #${id}`, "info");
      } catch (error) {
        setStatus(`Errore: ${error.message}`, "error");
      }
    }

    if (action === "delete") {
      const confirmed = window.confirm(`Confermi eliminazione del regista ${id}?`);
      if (!confirmed) {
        return;
      }
      try {
        await window.ApiClient.delete(`/registi/${id}`);
        setStatus("Regista eliminato.", "success");
        await loadRegisti();
      } catch (error) {
        setStatus(`Errore: ${error.message}`, "error");
      }
    }

    if (action === "films") {
      try {
        const films = await window.ApiClient.get(`/registi/${id}/films`);
        if (!Array.isArray(films) || films.length === 0) {
          relatedFilms.innerHTML = `<p class="subtle">Il regista #${id} non ha film associati.</p>`;
          return;
        }
        relatedFilms.innerHTML = `
          <h3>Film del regista #${id}</h3>
          <ul>
            ${films.map((f) => `<li>${f.titolo} (${f.durata} min)</li>`).join("")}
          </ul>
        `;
      } catch (error) {
        relatedFilms.innerHTML = `<p class="status error">Errore: ${error.message}</p>`;
      }
    }
  }

  function initRegistiPage() {
    if (!form || !tableBody) {
      return;
    }
    form.addEventListener("submit", submitForm);
    tableBody.addEventListener("click", handleTableClick);
    cancelBtn.addEventListener("click", resetForm);
    loadRegisti();
  }

  window.initRegistiPage = initRegistiPage;
})();
