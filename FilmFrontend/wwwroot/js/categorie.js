(function () {
  let editingId = null;
  const form = document.getElementById("categoria-form");
  const tableBody = document.getElementById("categorie-table-body");
  const statusEl = document.getElementById("categorie-status");
  const submitBtn = document.getElementById("categoria-submit");
  const cancelBtn = document.getElementById("categoria-cancel");

  function setStatus(message, kind) {
    statusEl.className = "status " + kind;
    statusEl.textContent = message;
  }

  function resetForm() {
    editingId = null;
    form.reset();
    submitBtn.textContent = "Crea categoria";
    cancelBtn.classList.add("hidden");
  }

  function renderRows(items) {
    if (!Array.isArray(items) || items.length === 0) {
      tableBody.innerHTML = "<tr><td colspan='4' class='subtle'>Nessuna categoria.</td></tr>";
      return;
    }

    tableBody.innerHTML = items
      .map((c) => `
        <tr>
          <td>${c.id}</td>
          <td>${c.nome}</td>
          <td>${c.descrizione || "-"}</td>
          <td>
            <div class="actions">
              <button class="btn-small secondary" data-action="edit" data-id="${c.id}">Modifica</button>
              <button class="btn-small danger" data-action="delete" data-id="${c.id}">Elimina</button>
            </div>
          </td>
        </tr>
      `)
      .join("");
  }

  async function loadCategorie() {
    setStatus("Caricamento categorie...", "info");
    try {
      const items = await window.ApiClient.get("/categorie");
      renderRows(items);
      setStatus("Categorie caricate.", "success");
    } catch (error) {
      tableBody.innerHTML = "";
      setStatus(`Errore: ${error.message}`, "error");
    }
  }

  async function submitForm(event) {
    event.preventDefault();
    const payload = {
      nome: form.nome.value.trim(),
      descrizione: form.descrizione.value.trim() || null
    };

    if (!payload.nome) {
      setStatus("Nome obbligatorio.", "error");
      return;
    }

    try {
      if (editingId) {
        await window.ApiClient.put(`/categorie/${editingId}`, payload);
        setStatus("Categoria aggiornata.", "success");
      } else {
        await window.ApiClient.post("/categorie", payload);
        setStatus("Categoria creata.", "success");
      }
      resetForm();
      await loadCategorie();
    } catch (error) {
      setStatus(`Errore: ${error.message}`, "error");
    }
  }

  async function handleTableClick(event) {
    const button = event.target.closest("button[data-action]");
    if (!button) {
      return;
    }

    const id = Number(button.dataset.id);
    const action = button.dataset.action;

    if (action === "edit") {
      try {
        const categoria = await window.ApiClient.get(`/categorie/${id}`);
        editingId = id;
        form.nome.value = categoria.nome || "";
        form.descrizione.value = categoria.descrizione || "";
        submitBtn.textContent = "Salva modifiche";
        cancelBtn.classList.remove("hidden");
        setStatus(`Modifica categoria #${id}`, "info");
      } catch (error) {
        setStatus(`Errore: ${error.message}`, "error");
      }
    }

    if (action === "delete") {
      if (!window.confirm(`Eliminare categoria #${id}?`)) {
        return;
      }
      try {
        await window.ApiClient.delete(`/categorie/${id}`);
        setStatus("Categoria eliminata.", "success");
        await loadCategorie();
      } catch (error) {
        setStatus(`Errore: ${error.message}`, "error");
      }
    }
  }

  function initCategoriePage() {
    if (!form || !tableBody) {
      return;
    }
    form.addEventListener("submit", submitForm);
    tableBody.addEventListener("click", handleTableClick);
    cancelBtn.addEventListener("click", resetForm);
    loadCategorie();
  }

  window.initCategoriePage = initCategoriePage;
})();
