(function () {
  let editingId = null;

  const tableBody = document.getElementById("registi-table-body");
  const form = document.getElementById("regista-form");
  const statusEl = document.getElementById("registi-status");
  const submitBtn = document.getElementById("regista-submit");
  const cancelBtn = document.getElementById("regista-cancel");

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

  function formatCurrency(v) {
    return new Intl.NumberFormat("it-IT", { style: "currency", currency: "EUR" }).format(Number(v || 0));
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

  function openEditModal(regista) {
    var content = `
      <h3>Modifica regista #${regista.id}</h3>
      <form id="modal-edit-form">
        <label>Nome</label>
        <input id="modal-nome" type="text" value="${regista.nome || ""}" required>
        <label>Cognome</label>
        <input id="modal-cognome" type="text" value="${regista.cognome || ""}" required>
        <label>Nazionalita</label>
        <input id="modal-nazionalita" type="text" value="${regista.nazionalita || ""}" required>
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
      var nome = document.getElementById("modal-nome").value.trim();
      var cognome = document.getElementById("modal-cognome").value.trim();
      var nazionalita = document.getElementById("modal-nazionalita").value.trim();
      if (!nome || !cognome || !nazionalita) {
        alert("Compila tutti i campi obbligatori.");
        return;
      }
      if (!window.confirm("Salvare le modifiche?")) return;
      try {
        await window.ApiClient.put(`/registi/${regista.id}`, { nome, cognome, nazionalita });
        window.ModalUtils.close();
        setStatus("Regista aggiornato.", "success");
        await loadRegisti();
      } catch (error) {
        alert("Errore: " + error.message);
      }
    });
  }

  function openFilmsModal(registaId, registaLabel) {
    var content = `
      <h3>Film del regista ${registaLabel}</h3>
      <div id="modal-films-body"><p class="subtle">Caricamento...</p></div>
      <div class="actions" style="margin-top:1rem">
        <a class="button secondary" href="/films.html?registaId=${registaId}">Gestisci film di questo regista</a>
        <button type="button" class="button secondary" id="modal-films-close">Chiudi</button>
      </div>`;

    var card = window.ModalUtils.open(content);
    if (!card) return;

    card.querySelector("#modal-films-close").addEventListener("click", function () {
      window.ModalUtils.close();
    });

    card.querySelector(".modal-card").style.maxWidth = "720px";

    window.ApiClient.get(`/registi/${registaId}/films`).then(function (films) {
      var body = document.getElementById("modal-films-body");
      if (!body) return;
      if (!Array.isArray(films) || films.length === 0) {
        body.innerHTML = "<p class='subtle'>Nessun film associato a questo regista.</p>";
        return;
      }
      body.innerHTML = films.map(function (f) {
        return `
          <div class="film-sheet-grid" style="margin-bottom:1rem;padding:0.5rem;border-bottom:1px solid var(--color-border)">
            <div style="width:80px;height:120px;overflow:hidden;border-radius:6px;flex-shrink:0">
              ${f.copertinaPath ? `<img src="${f.copertinaPath}" alt="${f.titolo}" style="width:100%;height:100%;object-fit:cover">` : "<div style='width:100%;height:100%;background:var(--color-surface-variant);display:flex;align-items:center;justify-content:center;color:var(--color-text-muted);font-size:0.7rem'>N/D</div>"}
            </div>
            <div>
              <strong>${f.titolo || "Senza titolo"}</strong>
              <p class="subtle">${f.dataProduzione ? String(f.dataProduzione).slice(0, 10) : "N/D"} &middot; ${f.durata || "-"} min</p>
              <p class="subtle">ID: #${f.id}</p>
            </div>
          </div>`;
      }).join("");
    }).catch(function (error) {
      var body = document.getElementById("modal-films-body");
      if (body) body.innerHTML = "<p class='status error'>Errore: " + error.message + "</p>";
    });
  }

  async function handleTableClick(event) {
    var button = event.target.closest("button[data-action]");
    if (!button) return;

    var action = button.dataset.action;
    var id = button.dataset.id;
    var row = button.closest("tr");
    var registaNome = row && row.children[1] ? row.children[1].textContent.trim() : "";
    var registaCognome = row && row.children[2] ? row.children[2].textContent.trim() : "";
    var registaLabel = `${registaNome} ${registaCognome}`.trim() || `#${id}`;

    switch (action) {
      case "edit":
        try {
          var regista = await window.ApiClient.get(`/registi/${id}`);
          openEditModal(regista);
        } catch (error) {
          setStatus(`Errore: ${error.message}`, "error");
        }
        return;
      case "delete": {
        var confirmed = window.confirm(`Confermi eliminazione del regista ${id}?`);
        if (!confirmed) return;
        try {
          await window.ApiClient.delete(`/registi/${id}`);
          setStatus("Regista eliminato.", "success");
          await loadRegisti();
        } catch (error) {
          setStatus(`Errore: ${error.message}`, "error");
        }
        return;
      }
      case "films":
        openFilmsModal(id, registaLabel);
        return;
      default:
        return;
    }
  }

  function initRegistiPage() {
    if (!form || !tableBody) return;
    form.addEventListener("submit", submitForm);
    tableBody.addEventListener("click", handleTableClick);
    cancelBtn.addEventListener("click", resetForm);
    loadRegisti();
  }

  window.initRegistiPage = initRegistiPage;
})();
