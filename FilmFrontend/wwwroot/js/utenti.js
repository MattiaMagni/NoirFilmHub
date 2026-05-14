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
  let cinemasCache = [];

  function setStatus(msg, kind) { statusEl.className = "status " + kind; statusEl.textContent = msg; }

  function formatDate(v) {
    if (!v) return "-";
    return new Date(v).toLocaleDateString("it-IT", { day: "2-digit", month: "2-digit", year: "numeric", hour: "2-digit", minute: "2-digit" });
  }

  async function loadCinemas() {
    if (cinemasCache.length > 0) return cinemasCache;
    try {
      var data = await window.ApiClient.get("/cinemas");
      cinemasCache = Array.isArray(data) ? data : (data && Array.isArray(data.$values) ? data.$values : []);
      return cinemasCache;
    } catch (e) {
      console.error("Errore caricamento cinema:", e);
      return [];
    }
  }

  function buildCinemaOptions(selectedId) {
    var opts = '<option value="">Nessuno</option>';
    for (var i = 0; i < cinemasCache.length; i++) {
      var c = cinemasCache[i];
      var sel = String(c.id) === String(selectedId) ? " selected" : "";
      opts += '<option value="' + c.id + '"' + sel + '>' + c.nome + ' - ' + c.citta + '</option>';
    }
    return opts;
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
      tableBody.innerHTML = "<tr><td colspan='9' class='subtle'>Nessun utente trovato.</td></tr>";
      return;
    }
    tableBody.innerHTML = items.map(u => {
      var roles = allowedRoles.map(r => `<option value="${r}" ${u.ruolo === r ? "selected" : ""}>${r}</option>`).join("");
      var providers = (u.externalLogins || []).join(", ") || "-";
      var stato = u.isDisabled ? '<span class="tag danger">Disabilitato</span>' : '<span class="tag success">Attivo</span>';
      var cinemaSelect = `<select data-cinema-select data-id="${u.id}" style="width:160px;">${buildCinemaOptions(u.cinemaPreferitoId)}</select>`;
      return `<tr>
        <td>${u.id}</td>
        <td>${u.nome} ${u.cognome}</td>
        <td>${u.email}</td>
        <td><select data-role-select data-id="${u.id}" style="width:120px;">${roles}</select></td>
        <td>${cinemaSelect}</td>
        <td>${providers}</td>
        <td>${stato}</td>
        <td>${formatDate(u.lastLoginAtUtc)}</td>
        <td>
          <div class="actions">
            <button class="btn-small" data-action="save-role" data-id="${u.id}">Salva ruolo</button>
            <button class="btn-small" data-action="save-cinema" data-id="${u.id}">Salva cinema</button>
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
      tableBody.innerHTML = "<tr><td colspan='9' class='subtle'>Errore caricamento utenti.</td></tr>";
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
      } else if (action === "save-cinema") {
        var cinemaSelect = document.querySelector(`select[data-cinema-select][data-id="${id}"]`);
        if (!cinemaSelect) return;
        var cinemaId = cinemaSelect.value ? parseInt(cinemaSelect.value) : 0;
        await window.AuthService.assignCinema(id, cinemaId);
        setStatus("Cinema assegnato con successo.", "success");
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
    var cinemaLabel = "-";
    if (detail.cinemaPreferitoId) {
      var found = cinemasCache.find(function(c) { return String(c.id) === String(detail.cinemaPreferitoId); });
      cinemaLabel = found ? (found.nome + " - " + found.citta) : ("Cinema #" + detail.cinemaPreferitoId);
    }
    var externalLogins = (detail.externalLogins || []).map(el => `<li>${el.provider} - ${el.email} (${formatDate(el.createdAtUtc)})</li>`).join("") || "<li>Nessuno</li>";
    var auditLogs = (detail.recentAuditLog || []).slice(0, 10).map(l => `<tr><td>${formatDate(l.createdAtUtc)}</td><td>${l.eventType}</td><td>${l.ipAddress || "-"}</td></tr>`).join("") || "<tr><td colspan='3'>Nessun evento</td></tr>";
    content.innerHTML = `
      <p><strong>ID:</strong> ${detail.id}</p>
      <p><strong>Email:</strong> ${detail.email}</p>
      <p><strong>Nome:</strong> ${detail.nome} ${detail.cognome}</p>
      <p><strong>Ruolo:</strong> ${detail.ruolo}</p>
      <p><strong>Cinema Assegnato:</strong> ${cinemaLabel}</p>
      <p><strong>Stato:</strong> ${detail.isDisabled ? "Disabilitato" : "Attivo"}</p>
      <p><strong>Email verificata:</strong> ${detail.emailVerified ? "Si" : "No"}</p>
      <p><strong>Credenziali locali:</strong> ${detail.localCredentialsEnabled ? "Si" : "No (social-only)"}</p>
      <p><strong>Auth Version:</strong> ${detail.authVersion}</p>
      <p><strong>Ultimo login:</strong> ${formatDate(detail.lastLoginAtUtc)} (${detail.lastLoginProvider || "-"})</p>
      <p><strong>Ultimo cambio password:</strong> ${formatDate(detail.passwordChangedAtUtc)}</p>
      <p><strong>Creato:</strong> ${formatDate(detail.createdAtUtc)}</p>
      <p><strong>Credito piattaforma:</strong> &euro;${detail.creditoPiattaforma}</p>
      <h4 style="margin-top:12px;">Provider Social</h4><ul>${externalLogins}</ul>
      <h4 style="margin-top:12px;">Audit Log Recente</h4>
      <table><thead><tr><th>Data</th><th>Evento</th><th>IP</th></tr></thead><tbody>${auditLogs}</tbody></table>`;
    modal.style.display = "flex";
    document.getElementById("detail-close").addEventListener("click", () => modal.style.display = "none");
  }

  function initInviteModal() {
    var modal = document.getElementById("invite-modal");
    document.getElementById("btn-invite").addEventListener("click", async () => {
      modal.style.display = "flex";
      var cinemas = await loadCinemas();
      var cinemaSelect = document.getElementById("inv-cinema");
      cinemaSelect.innerHTML = '<option value="">Nessun cinema</option>' + cinemas.map(c =>
        `<option value="${c.id}">${c.nome} - ${c.citta}</option>`
      ).join("");
      document.getElementById("inv-cinema-group").style.display = (document.getElementById("inv-ruolo").value === "power_user" || document.getElementById("inv-ruolo").value === "admin") ? "" : "none";
    });
    document.getElementById("inv-cancel").addEventListener("click", () => modal.style.display = "none");
    var ruoloSelect = document.getElementById("inv-ruolo");
    ruoloSelect.addEventListener("change", () => {
      var show = ruoloSelect.value === "power_user" || ruoloSelect.value === "admin";
      document.getElementById("inv-cinema-group").style.display = show ? "" : "none";
    });
    document.getElementById("invite-form").addEventListener("submit", async (e) => {
      e.preventDefault();
      var st = document.getElementById("inv-status");
      try {
        var ruolo = document.getElementById("inv-ruolo").value;
        var result = await window.AuthService.inviteUser(
          document.getElementById("inv-email").value.trim(),
          ruolo,
          document.getElementById("inv-nome").value.trim(),
          document.getElementById("inv-cognome").value.trim()
        );
        if (result && result.id) {
          var cinemaId = document.getElementById("inv-cinema").value;
          if (cinemaId && (ruolo === "power_user" || ruolo === "admin")) {
            try {
              await window.AuthService.assignCinema(result.id, parseInt(cinemaId));
            } catch (assignErr) {
              console.warn("Assegnazione cinema fallita:", assignErr);
            }
          }
        }
        st.className = "status success";
        st.textContent = "Invito inviato!";
        setTimeout(() => { modal.style.display = "none"; loadUtenti(); }, 1500);
      } catch (err) {
        st.className = "status error";
        st.textContent = err.message || "Errore.";
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
    await loadCinemas();
    await loadUtenti();
  }

  window.initUtentiPage = initUtentiPage;
})();