(function () {
  const tableBody = document.getElementById("utenti-table-body");
  const statusEl = document.getElementById("utenti-status");
  const currentUser = window.AuthService ? window.AuthService.getCurrentUser() : null;
  const currentUserId = currentUser && Number.isInteger(Number(currentUser.id)) ? Number(currentUser.id) : null;

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
              ${currentUserId === Number(u.id)
                ? "<span class='tag info'>Account corrente</span>"
                : `<button class="btn-small danger" data-action="delete-user" data-id="${u.id}">Elimina</button>`}
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
    const btn = event.target.closest("button[data-action]");
    if (!btn) {
      return;
    }

    const action = btn.dataset.action;
    const id = Number(btn.dataset.id);

    if (action === "delete-user") {
      if (!Number.isFinite(id)) {
        return;
      }

      if (currentUserId === id) {
        setStatus("Non puoi eliminare il tuo account.", "error");
        return;
      }

      const confirmed = window.confirm(`Confermi l'eliminazione dell'utente #${id}?`);
      if (!confirmed) {
        return;
      }

      try {
        await window.ApiClient.delete(`/auth/utenti/${id}`);
        setStatus(`Utente #${id} eliminato.`, "success");
        await loadUtenti();
      } catch (error) {
        setStatus(`Errore: ${error.message}`, "error");
      }
      return;
    }

    if (action !== "save-role") {
      return;
    }

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
