(function () {
  const tableBody = document.getElementById("utenti-table-body");
  const statusEl = document.getElementById("utenti-status");

  const allowedRoles = ["admin", "power_user", "utente"];

  function setStatus(message, kind) {
    statusEl.className = "status " + kind;
    statusEl.textContent = message;
  }

  function renderRows(items) {
    if (!Array.isArray(items) || items.length === 0) {
      tableBody.innerHTML = "<tr><td colspan='7' class='subtle'>Nessun utente trovato.</td></tr>";
      return;
    }

    tableBody.innerHTML = items
      .map((u) => {
        const options = allowedRoles
          .map((r) => `<option value="${r}" ${String(u.ruolo || "").toLowerCase() === r ? "selected" : ""}>${r}</option>`)
          .join("");

        return `
          <tr>
            <td>${u.id}</td>
            <td>${u.email}</td>
            <td>${u.nome}</td>
            <td>${u.cognome}</td>
            <td>${u.telefono || "-"}</td>
            <td>
              <select data-role-select data-id="${u.id}">${options}</select>
            </td>
            <td>
              <button class="btn-small primary" data-action="save-role" data-id="${u.id}">Salva</button>
            </td>
          </tr>
        `;
      })
      .join("");
  }

  async function loadUtenti() {
    setStatus("Caricamento utenti...", "info");
    try {
      const users = await window.ApiClient.get("/auth/utenti");
      renderRows(users);
      setStatus("Utenti caricati.", "success");
    } catch (error) {
      tableBody.innerHTML = "";
      setStatus(`Errore: ${error.message}`, "error");
    }
  }

  async function onTableClick(event) {
    const btn = event.target.closest("button[data-action='save-role']");
    if (!btn) {
      return;
    }

    const id = Number(btn.dataset.id);
    const select = tableBody.querySelector(`select[data-role-select][data-id='${id}']`);
    if (!select) {
      return;
    }

    const ruolo = String(select.value || "").toLowerCase();
    if (!allowedRoles.includes(ruolo)) {
      setStatus("Ruolo non valido.", "error");
      return;
    }

    try {
      await window.ApiClient.put(`/auth/utenti/${id}/ruolo`, { ruolo });
      setStatus(`Ruolo utente #${id} aggiornato.`, "success");
      await loadUtenti();
    } catch (error) {
      setStatus(`Errore: ${error.message}`, "error");
    }
  }

  async function initUtentiPage() {
    if (!tableBody) {
      return;
    }
    tableBody.addEventListener("click", onTableClick);
    await loadUtenti();
  }

  window.initUtentiPage = initUtentiPage;
})();
