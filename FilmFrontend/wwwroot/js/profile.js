(function () {
  const form = document.getElementById("profile-form");
  const emailInput = document.getElementById("profile-email");
  const nomeInput = document.getElementById("profile-nome");
  const cognomeInput = document.getElementById("profile-cognome");
  const telefonoInput = document.getElementById("profile-telefono");
  const statusEl = document.getElementById("profile-status");
  const prenotazioniBody = document.getElementById("prenotazioni-body");

  function setStatus(message, kind) {
    statusEl.className = "status " + kind;
    statusEl.textContent = message;
  }

  function formatDate(value) { return String(value || "").slice(0, 10); }
  function formatTime(value) {
    if (!value) return "";
    const d = new Date(value);
    return `${String(d.getHours()).padStart(2, "0")}:${String(d.getMinutes()).padStart(2, "0")}`;
  }

  async function downloadTicketPdf(codiceAcquisto) {
    const code = String(codiceAcquisto || "").trim();
    if (!code) { setStatus("Codice acquisto non valido per il download PDF.", "error"); return; }
    try {
      const token = window.AuthService ? await window.AuthService.ensureValidAccessToken() : null;
      const headers = token ? { Authorization: `Bearer ${token}` } : {};
      const response = await fetch(`${window.AppConfig.API_BASE_URL}/tickets/${encodeURIComponent(code)}/pdf`, { method: "GET", headers });
      if (!response.ok) throw new Error("Download PDF non riuscito");
      const blob = await response.blob();
      const url = window.URL.createObjectURL(blob);
      const link = document.createElement("a");
      link.href = url; link.download = `ticket-${code}.pdf`;
      document.body.appendChild(link); link.click(); link.remove();
      window.URL.revokeObjectURL(url);
    } catch (error) { setStatus(`Errore download PDF: ${error.message}`, "error"); }
  }

  function renderPrenotazioni(rows) {
    if (!Array.isArray(rows) || rows.length === 0) {
      prenotazioniBody.innerHTML = "<tr><td colspan='8' class='subtle'>Nessuna programmazione salvata.</td></tr>";
      return;
    }
    prenotazioniBody.innerHTML = rows.map(p => `
      <tr>
        <td>${p.id}</td>
        <td>${p.titoloFilm || `Film #${p.filmId}`}</td>
        <td>${p.nomeCinema || `Cinema #${p.cinemaId}`}</td>
        <td>${formatDate(p.data)}</td>
        <td>${formatTime(p.ora)}</td>
        <td>${p.numeroPosti}</td>
        <td>${p.stato}</td>
        <td>
          <div class="actions">
            ${p.stato === "Confermata" ? `<button class="btn-small" data-action="download" data-code="${p.codiceAcquisto || ""}">PDF</button>` : ""}
            ${p.stato === "Confermata" ? `<button class="btn-small danger" data-action="cancel" data-id="${p.id}">Annulla</button>` : "-"}
          </div>
        </td>
      </tr>`).join("");
  }

  function renderSecurity(me) {
    var sec = document.getElementById("security-section");
    var content = document.getElementById("security-content");
    if (!sec || !content) return;
    sec.style.display = "block";

    var passwordStatus = me.localCredentialsEnabled
      ? '<span class="tag success">Password impostata</span>'
      : '<span class="tag info">Nessuna password (account social)</span>';

    var passwordActions = me.localCredentialsEnabled
      ? '<button class="btn-small primary" id="btn-change-password">Cambia password</button>'
      : '<button class="btn-small primary" id="btn-setup-password">Imposta password</button>';

    var externalLoginsHtml = (me.externalLogins || []).length > 0
      ? (me.externalLogins || []).map(p => `<span class="tag">${p}</span>`).join(" ")
      : '<span class="subtle">Nessun provider collegato</span>';

    content.innerHTML = `
      <div style="margin-bottom:16px;">
        <strong>Stato password:</strong> ${passwordStatus}
        <div style="margin-top:8px;">${passwordActions}</div>
      </div>
      <div style="margin-bottom:16px;">
        <strong>Provider social collegati:</strong> ${externalLoginsHtml}
      </div>
      <div style="margin-bottom:16px;">
        <strong>Sessioni:</strong>
        <button class="btn-small danger" id="btn-revoke-sessions" style="margin-left:8px;">Disconnetti tutti i dispositivi</button>
      </div>`;

    setTimeout(() => {
      var btnCp = document.getElementById("btn-change-password");
      var btnSp = document.getElementById("btn-setup-password");
      var btnRs = document.getElementById("btn-revoke-sessions");
      if (btnCp) btnCp.addEventListener("click", openChangePasswordModal);
      if (btnSp) btnSp.addEventListener("click", setupPasswordDirect);
      if (btnRs) btnRs.addEventListener("click", async () => {
        if (confirm("Sei sicuro? Tutti i dispositivi verranno disconnessi."))
          await window.AuthService.revokeAllSessions();
      });
    }, 100);
  }

  function openChangePasswordModal() {
    var modal = document.getElementById("change-password-modal");
    if (!modal) return;
    modal.style.display = "flex";
    document.getElementById("cp-cancel").addEventListener("click", () => modal.style.display = "none");
    document.getElementById("change-password-form").addEventListener("submit", async (e) => {
      e.preventDefault();
      var cur = document.getElementById("cp-current").value;
      var nw = document.getElementById("cp-new").value;
      var nw2 = document.getElementById("cp-new2").value;
      var st = document.getElementById("cp-status");
      if (nw !== nw2) { st.className = "status error"; st.textContent = "Le password non coincidono."; return; }
      st.className = "status info"; st.textContent = "Cambio password in corso...";
      try {
        await window.AuthService.changePassword(cur, nw);
        st.className = "status success"; st.textContent = "Password cambiata!";
        setTimeout(() => { modal.style.display = "none"; location.reload(); }, 1500);
      } catch (err) {
        st.className = "status error"; st.textContent = err.message || "Errore.";
      }
    });
  }

  async function setupPasswordDirect() {
    try {
      await window.AuthService.requestPasswordSetup();
      setStatus("Email di setup inviata. Controlla la tua casella di posta.", "success");
    } catch (err) {
      setStatus(err.message || "Errore durante l'invio.", "error");
    }
  }

  async function loadProfile() {
    setStatus("Caricamento profilo...", "info");
    try {
      const [me, miePrenotazioni] = await Promise.all([
        window.ApiClient.get("/auth/me"),
        window.ApiClient.get("/prenotazioni/mie")
      ]);
      emailInput.value = me.email || "";
      nomeInput.value = me.nome || "";
      cognomeInput.value = me.cognome || "";
      telefonoInput.value = me.telefono || "";
      renderPrenotazioni(miePrenotazioni);
      renderSecurity(me);
      setStatus("Profilo aggiornato.", "success");
    } catch (error) {
      renderPrenotazioni([]);
      setStatus(`Errore: ${error.message}`, "error");
    }
  }

  async function submitProfile(event) {
    event.preventDefault();
    const payload = { nome: nomeInput.value.trim(), cognome: cognomeInput.value.trim(), telefono: telefonoInput.value.trim() };
    if (!payload.nome || !payload.cognome) { setStatus("Nome e cognome obbligatori.", "error"); return; }
    try {
      await window.ApiClient.put("/auth/me", payload);
      setStatus("Profilo salvato.", "success");
      await loadProfile();
    } catch (error) { setStatus(`Errore: ${error.message}`, "error"); }
  }

  async function handlePrenotazioniClick(event) {
    const actionButton = event.target.closest("button[data-action]");
    if (!actionButton) return;
    const action = actionButton.dataset.action;
    if (action === "download") { await downloadTicketPdf(actionButton.dataset.code); return; }
    const button = event.target.closest("button[data-action='cancel']");
    if (!button) return;
    const id = Number(button.dataset.id);
    if (!window.confirm(`Confermi annullamento prenotazione #${id}?`)) return;
    try {
      await window.ApiClient.put(`/prenotazioni/${id}/annulla`, {});
      setStatus("Prenotazione annullata.", "success");
      await loadProfile();
    } catch (error) { setStatus(`Errore: ${error.message}`, "error"); }
  }

  async function initProfilePage() {
    if (!form || !prenotazioniBody) return;
    form.addEventListener("submit", submitProfile);
    prenotazioniBody.addEventListener("click", handlePrenotazioniClick);
    await loadProfile();
  }

  window.initProfilePage = initProfilePage;
})();
