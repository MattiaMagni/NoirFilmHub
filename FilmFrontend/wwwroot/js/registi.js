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
    const row = button.closest("tr");
    const registaNome = row && row.children[1] ? row.children[1].textContent.trim() : "";
    const registaCognome = row && row.children[2] ? row.children[2].textContent.trim() : "";
    const registaLabel = `${registaNome} ${registaCognome}`.trim() || `#${id}`;

    switch (action) {
      case "edit":
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
        return;
      case "delete": {
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
        return;
      }
      case "films":
        if (!relatedFilms) {
          return;
        }

        relatedFilms.innerHTML = `<p class="subtle">Caricamento film del regista #${id}...</p>`;

        try {
          const films = await window.ApiClient.get(`/registi/${id}/films`);
          if (!Array.isArray(films) || films.length === 0) {
            relatedFilms.innerHTML = `
              <h3>Film del regista ${registaLabel}</h3>
              <p class="subtle">Nessun film associato a questo regista.</p>
              <p><a class="button secondary" href="/films.html?registaId=${id}">Aggiungi film per questo regista</a></p>
            `;
            return;
          }

          relatedFilms.innerHTML = `
            <h3>Film del regista ${registaLabel}</h3>
            <div class="table-wrap">
              <table>
                <thead>
                  <tr>
                    <th>ID</th>
                    <th>Titolo</th>
                    <th>Data produzione</th>
                    <th>Durata</th>
                  </tr>
                </thead>
                <tbody>
                  ${films
                    .map(
                      (f) => `
                    <tr>
                      <td>${f.id}</td>
                      <td>${f.titolo || "-"}</td>
                      <td>${String(f.dataProduzione || "").slice(0, 10) || "-"}</td>
                      <td>${f.durata || "-"} min</td>
                    </tr>
                  `
                    )
                    .join("")}
                </tbody>
              </table>
            </div>
            <p><a class="button secondary" href="/films.html?registaId=${id}">Gestisci film di questo regista</a></p>
          `;
        } catch (error) {
          relatedFilms.innerHTML = `<p class="status error">Errore: ${error.message}</p>`;
        }
        return;
      default:
        return;
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
