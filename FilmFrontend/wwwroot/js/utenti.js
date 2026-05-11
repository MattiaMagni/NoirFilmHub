(function () {
  const tableBody = document.getElementById("utenti-table-body");
  const statusEl = document.getElementById("utenti-status");
  const paginationEl = document.getElementById("pagination");
  const searchInput = document.getElementById("search-input");
  const filterRuolo = document.getElementById("filter-ruolo");
  const filterStatus = document.getElementById("filter-status");
  const allowedRoles = ["admin", "power_user", "utente"];

  let currentPage = 1;
  let searchTimer = null;

  function setStatus(msg, kind) { statusEl.className = "status " + kind; statusEl.textContent = msg; }

  function formatDate(v) {
    if (!v) return "-";
    return new Date(v).toLocaleDateString("it-IT", { day: "2-digit", month: "2-digit", year: "numeric", hour: "2-digit", minute: "2-digit" });
  }

  function renderPagination(totalPages) {
    if (totalPages <= 1) { paginationEl.innerHTML = ""; return; }
    var html = "";
    for (var i = 1; i <= Math.min(totalPages, 10); i++) {
      html += `<button class="btn-small ${i === currentPage ? "primary" : "secondary"}" data-page="${i}">${i}</button>`;
    }
    paginationEl.innerHTML = html;
    paginationEl.querySelectorAll("button").forEach(b => b.addEventListener("click", () => { currentPage = parseInt(b.dataset.page); loadUtenti(); }));
  }

  function renderRows(items) {
    if (!Array.isArray(items) || items.length === 0) {
      tableBody.innerHTML = "<tr><td colspan='8' class='subtle'>Nessun utente trovato.</td></tr>";
      return;
    }
    tableBody.innerHTML = items.map(u => {
      var roles = allowedRoles.map(r => `<option value="${r}" ${u.ruolo === r ? "selected" : ""}>${r}</option>`).join("");
      var providers = (u.externalLogins || []).join(", ") || "-";
      var stato = u.isDisabled ? '<span class="tag danger">Disabilitato</span>' : '<span class="tag success">Attivo</span>';
      return `<tr>
        <td>${u.id}</td>
        <td>${u.nome} ${u.cognome}</td>
        <td>${u.email}</td>
        <td><select data-role-select data-id="${u.id}" style="width:120px;">${roles}</select></td>
        <td>${providers}</td>
        <td>${stato}</td>
        <td>${formatDate(u.lastLoginAtUtc)}</td>
        <td>
          <div class="actions">
            <button class="btn-small" data-action="save-role" data-id="${u.id}">Salva ruolo</button>
            <button class="btn-small" data-action="detail" data-id="${u.id}">Dettaglio</button>
            ${u.isDisabled
              ? `<button class="btn-small success" data-action="enable" data-id="${u.id}">Riabilita</button>`
              : `<button class="btn-small warning" data-action="disable" data-id="${u.id}">Disabilita</button>`}
            ${u.localCredentialsEnabled ? `<button class="btn-small" data-action="force-reset" data-id="${u.id}">Reset PW</button>` : ""}
            <button class="btn-small danger" data-action="delete" data-id="${u.id}">Elimina</button>
          </div>
        </td>
      </tr>`;
    }).join("");
  }

  async function loadUtenti() {
    setStatus("Caricamento utenti...", "info");
    try {
      var params = { page: currentPage, pageSize: 15 };
      if (searchInput.value.trim()) params.search = searchInput.value.trim();
      if (filterRuolo.value) params.ruolo = filterRuolo.value;
      if (filterStatus.value === "disabled") params.isDisabled = true;
      else if (filterStatus.value === "social") params.hasLocalCredentials = false;
      var result = await window.AuthService.searchUsers(params);
      renderRows(result.items);
      renderPagination(result.totalPages);
      setStatus(`${result.totalCount} utenti trovati.`, "success");
    } catch (error) {
      tableBody.innerHTML = "<tr><td colspan='8' class='subtle'>Errore caricamento utenti.</td></tr>";
      setStatus(`Errore: ${error.message}`, "error");
    }
  }

  async function handleTableClick(event) {
    var btn = event.target.closest("button[data-action]");
    if (!btn) return;
    var action = btn.dataset.action;
    var id = parseInt(btn.dataset.id);
    try {
      if (action === "save-role") {
        var select = document.querySelector(`select[data-role-select][data-id="${id}"]`);
        if (!select) return;
        await window.AuthService.changeUserRole(id, select.value);
        setStatus("Ruolo aggiornato.", "success");
        loadUtenti();
      } else if (action === "detail") {
        var detail = await window.AuthService.getUserDetail(id);
        showDetail(detail);
      } else if (action === "disable") {
        if (!confirm("Disabilitare questo utente?")) return;
        await window.AuthService.disableUser(id);
        loadUtenti();
      } else if (action === "enable") {
        await window.AuthService.enableUser(id);
        loadUtenti();
      } else if (action === "force-reset") {
        if (!confirm("Forzare il reset della password?")) return;
        await window.AuthService.forcePasswordReset(id);
        setStatus("Email di reset inviata.", "success");
      } else if (action === "delete") {
        if (!confirm("Sei sicuro di voler eliminare questo utente? L'operazione e irreversibile.")) return;
        await window.AuthService.deleteUser(id);
        loadUtenti();
      }
    } catch (error) {
      setStatus(`Errore: ${error.message}`, "error");
    }
  }

  function showDetail(detail) {
    var modal = document.getElementById("detail-modal");
    var content = document.getElementById("detail-content");
    if (!modal || !content) return;
    var externalLogins = (detail.externalLogins || []).map(el => `<li>${el.provider} - ${el.email} (${formatDate(el.createdAtUtc)})</li>`).join("") || "<li>Nessuno</li>";
    var auditLogs = (detail.recentAuditLog || []).slice(0, 10).map(l => `<tr><td>${formatDate(l.createdAtUtc)}</td><td>${l.eventType}</td><td>${l.ipAddress || "-"}</td></tr>`).join("") || "<tr><td colspan='3'>Nessun evento</td></tr>";
    content.innerHTML = `
      <p><strong>ID:</strong> ${detail.id}</p>
      <p><strong>Email:</strong> ${detail.email}</p>
      <p><strong>Nome:</strong> ${detail.nome} ${detail.cognome}</p>
      <p><strong>Ruolo:</strong> ${detail.ruolo}</p>
      <p><strong>Stato:</strong> ${detail.isDisabled ? "Disabilitato" : "Attivo"}</p>
      <p><strong>Email verificata:</strong> ${detail.emailVerified ? "Si" : "No"}</p>
      <p><strong>Credenziali locali:</strong> ${detail.localCredentialsEnabled ? "Si" : "No (social-only)"}</p>
      <p><strong>Auth Version:</strong> ${detail.authVersion}</p>
      <p><strong>Ultimo login:</strong> ${formatDate(detail.lastLoginAtUtc)} (${detail.lastLoginProvider || "-"})</p>
      <p><strong>Ultimo cambio password:</strong> ${formatDate(detail.passwordChangedAtUtc)}</p>
      <p><strong>Creato:</strong> ${formatDate(detail.createdAtUtc)}</p>
      <p><strong>Credito piattaforma:</strong> €${detail.creditoPiattaforma}</p>
      <h4 style="margin-top:12px;">Provider Social</h4><ul>${externalLogins}</ul>
      <h4 style="margin-top:12px;">Audit Log Recente</h4>
      <table><thead><tr><th>Data</th><th>Evento</th><th>IP</th></tr></thead><tbody>${auditLogs}</tbody></table>`;
    modal.style.display = "flex";
    document.getElementById("detail-close").addEventListener("click", () => modal.style.display = "none");
  }

  function initInviteModal() {
    var modal = document.getElementById("invite-modal");
    document.getElementById("btn-invite").addEventListener("click", () => modal.style.display = "flex");
    document.getElementById("inv-cancel").addEventListener("click", () => modal.style.display = "none");
    document.getElementById("invite-form").addEventListener("submit", async (e) => {
      e.preventDefault();
      var st = document.getElementById("inv-status");
      try {
        await window.AuthService.inviteUser(
          document.getElementById("inv-email").value.trim(),
          document.getElementById("inv-ruolo").value,
          document.getElementById("inv-nome").value.trim(),
          document.getElementById("inv-cognome").value.trim()
        );
        st.className = "status success"; st.textContent = "Invito inviato!";
        setTimeout(() => { modal.style.display = "none"; loadUtenti(); }, 1500);
      } catch (err) {
        st.className = "status error"; st.textContent = err.message || "Errore.";
      }
    });
  }

  async function initUtentiPage() {
    if (!tableBody) return;
    tableBody.addEventListener("click", handleTableClick);
    searchInput.addEventListener("input", () => { clearTimeout(searchTimer); searchTimer = setTimeout(() => { currentPage = 1; loadUtenti(); }, 300); });
    filterRuolo.addEventListener("change", () => { currentPage = 1; loadUtenti(); });
    filterStatus.addEventListener("change", () => { currentPage = 1; loadUtenti(); });
    initInviteModal();
    await loadUtenti();
  }

  window.initUtentiPage = initUtentiPage;
})();
