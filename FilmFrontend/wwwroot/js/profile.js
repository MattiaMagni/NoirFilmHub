(function () {
  const form = document.getElementById("profile-form");
  const emailInput = document.getElementById("profile-email");
  const nomeInput = document.getElementById("profile-nome");
  const cognomeInput = document.getElementById("profile-cognome");
  const telefonoInput = document.getElementById("profile-telefono");
  const statusEl = document.getElementById("profile-status");
  const prenotazioniBody = document.getElementById("prenotazioni-body");
  const giftcardsBody = document.getElementById("giftcards-body");

  function setStatus(message, kind) {
    statusEl.className = "status " + kind;
    statusEl.textContent = message;
  }
  function formatDate(value) { return String(value || "").slice(0, 10); }
  function formatTime(value) {
    if (!value) return "";
    var d = new Date(value);
    return `${String(d.getHours()).padStart(2, "0")}:${String(d.getMinutes()).padStart(2, "0")}`;
  }

  // --- Tab switching ---
  document.querySelectorAll(".profile-tab").forEach(tab => {
    tab.addEventListener("click", () => {
      document.querySelectorAll(".profile-tab").forEach(t => t.classList.remove("active"));
      tab.classList.add("active");
      document.querySelectorAll(".profile-tab-content").forEach(c => c.classList.add("hidden"));
      var target = document.getElementById("tab-" + tab.dataset.tab);
      if (target) target.classList.remove("hidden");
    });
  });

  // --- Password toggle ---
  document.addEventListener("click", (e) => {
    var btn = e.target.closest(".pw-toggle");
    if (!btn) return;
    var input = document.getElementById(btn.dataset.target);
    if (input) input.type = input.type === "password" ? "text" : "password";
  });

  // --- Download PDF ---
  async function downloadTicketPdf(codiceAcquisto) {
    var code = String(codiceAcquisto || "").trim();
    if (!code) { setStatus("Codice acquisto non valido.", "error"); return; }
    try {
      var token = window.AuthService ? await window.AuthService.ensureValidAccessToken() : null;
      var headers = token ? { Authorization: `Bearer ${token}` } : {};
      var response = await fetch(`${window.AppConfig.API_BASE_URL}/tickets/${encodeURIComponent(code)}/pdf`, { method: "GET", headers });
      if (!response.ok) throw new Error("Download PDF non riuscito");
      var blob = await response.blob();
      var url = window.URL.createObjectURL(blob);
      var link = document.createElement("a");
      link.href = url; link.download = `ticket-${code}.pdf`;
      document.body.appendChild(link); link.click(); link.remove();
      window.URL.revokeObjectURL(url);
    } catch (error) { setStatus(`Errore PDF: ${error.message}`, "error"); }
  }

  function renderPrenotazioni(rows) {
    if (!Array.isArray(rows) || rows.length === 0) {
      prenotazioniBody.innerHTML = "<tr><td colspan='8' class='subtle'>Nessun biglietto acquistato.</td></tr>";
      return;
    }
    var now = new Date();
    prenotazioniBody.innerHTML = rows.map(p => {
      var dataDate = new Date(p.data);
      var oraDate = new Date(p.ora);
      var showDateTime = new Date(dataDate.getFullYear(), dataDate.getMonth(), dataDate.getDate(), oraDate.getHours(), oraDate.getMinutes());
      var isExpired = showDateTime < now;
      var displayStato = isExpired ? "Scaduto" : p.stato;
      var statoClass = isExpired ? "disabled" : p.stato === "Confermata" ? "success" : p.stato === "Annullata" ? "danger" : "info";
      var actions = (p.stato === "Confermata" && !isExpired)
        ? `<button class="btn-small" data-action="download" data-code="${p.codiceAcquisto || ""}">PDF</button> <button class="btn-small danger" data-action="cancel" data-id="${p.id}">Annulla</button>`
        : "-";
      return `
        <tr>
          <td>#${p.id}</td><td>${p.titoloFilm || `Film #${p.filmId}`}</td><td>${p.nomeCinema || `Cinema #${p.cinemaId}`}</td>
          <td>${formatDate(p.data)}</td><td>${formatTime(p.ora)}</td><td>${p.numeroPosti}</td>
          <td><span class="tag ${statoClass}">${displayStato}</span></td>
          <td>${actions}</td>
        </tr>`;
    }).join("");
  }

  function renderGiftCards(cards) {
    if (!Array.isArray(cards) || cards.length === 0) {
      giftcardsBody.innerHTML = "<p class='subtle'>Nessuna gift card attiva.</p>";
      return;
    }
    var active = cards.filter(c => c.stato === "Active");
    if (!active.length) { giftcardsBody.innerHTML = "<p class='subtle'>Nessuna gift card attiva.</p>"; return; }
    giftcardsBody.innerHTML = active.map(c => `<div class="panel" style="margin-bottom:0.5rem">
      <div style="display:flex;justify-content:space-between;align-items:center">
        <div><strong>...${c.codice.slice(-4)}</strong></div>
        <div><span style="font-size:1.2rem;font-weight:700;color:var(--color-primary)">${c.saldoResiduo.toFixed(2)} EUR</span></div>
      </div>
      <p class="subtle" style="margin-bottom:0.1rem">Acquistata il: ${c.creatoIl ? new Date(c.creatoIl).toLocaleDateString("it-IT") : "N/D"}</p>
      <p class="subtle">Scadenza: ${c.scadenza ? new Date(c.scadenza).toLocaleDateString("it-IT") : "Nessuna scadenza"}</p>
    </div>`).join("");
  }

  function renderSecurity(me) {
    var sec = document.getElementById("security-section");
    var content = document.getElementById("security-content");
    if (!sec || !content) return;
    sec.style.display = "block";
    var passwordStatus = me.localCredentialsEnabled
      ? '<span class="tag success">Password impostata</span>'
      : '<span class="tag info">Solo social</span>';
    var passwordActions = me.localCredentialsEnabled
      ? '<button class="btn-small primary" id="btn-change-password">Cambia password</button>'
      : '<button class="btn-small primary" id="btn-setup-password">Imposta password</button>';
    var externalLoginsHtml = (me.externalLogins || []).length > 0
      ? (me.externalLogins || []).map(p => `<span class="tag">${p}</span>`).join(" ")
      : '<span class="subtle">Nessuno</span>';
    content.innerHTML = `
      <p>${passwordStatus} ${passwordActions}</p>
      <p><strong>Provider:</strong> ${externalLoginsHtml}</p>
      <p id="geo-toggle-row"><span>Posizione geografica</span> <button class="btn-small" id="btn-geo-toggle"></button></p>
      <p><button class="btn-small danger" id="btn-revoke-sessions">Disconnetti tutti</button></p>`;
    setTimeout(() => {
      var btnCp = document.getElementById("btn-change-password");
      var btnSp = document.getElementById("btn-setup-password");
      var btnRs = document.getElementById("btn-revoke-sessions");
      if (btnCp) btnCp.addEventListener("click", openChangePasswordModal);
      if (btnSp) btnSp.addEventListener("click", async () => { try { await window.AuthService.requestPasswordSetup(); setStatus("Email inviata.", "success"); } catch(e) { setStatus(e.message, "error"); } });
      if (btnRs) btnRs.addEventListener("click", async () => { if (confirm("Tutti i dispositivi verranno disconnessi.")) await window.AuthService.revokeAllSessions(); });
      updateGeoButton();
    }, 100);
  }

  function updateGeoButton() {
    var btn = document.getElementById("btn-geo-toggle");
    if (!btn) return;
    var on = localStorage.getItem("geo_enabled") !== "0";
    btn.textContent = on ? "Disattiva" : "Attiva";
    btn.className = on ? "btn-small danger" : "btn-small primary";
    btn.onclick = function () {
      if (on) {
        localStorage.setItem("geo_enabled", "0");
      } else {
        localStorage.removeItem("geo_enabled");
      }
      updateGeoButton();
    };
  }

  function showCancelToast(bookingId) {
    var existing = document.getElementById("cancel-toast-overlay");
    if (existing) existing.remove();
    var overlay = document.createElement("div");
    overlay.id = "cancel-toast-overlay";
    overlay.className = "cart-toast-overlay";
    overlay.innerHTML = '<div class="cart-toast-card"><p class="cart-toast-icon">&#x1f4e7;</p><h3>Prenotazione #'+bookingId+' annullata</h3><p class="subtle">Riceverai una gift card di rimborso (50%) via email.</p><p class="subtle">Controlla la tua casella di posta.</p><div class="cart-toast-actions"><button class="button primary" id="cancel-toast-ok">OK</button></div></div>';
    document.body.appendChild(overlay);
    document.getElementById("cancel-toast-ok").onclick = function() { overlay.remove(); };
    overlay.onclick = function(e) { if (e.target === overlay) overlay.remove(); };
  }

  // --- Modal ---
  function openChangePasswordModal() {
    var modal = document.getElementById("change-password-modal");
    if (!modal) return;
    modal.style.display = "flex";
    document.getElementById("cp-cancel").onclick = () => modal.style.display = "none";
    document.getElementById("change-password-form").onsubmit = async (e) => {
      e.preventDefault();
      var cur = document.getElementById("cp-current").value;
      var nw = document.getElementById("cp-new").value;
      var nw2 = document.getElementById("cp-new2").value;
      var st = document.getElementById("cp-status");
      if (nw !== nw2) { st.className = "status error"; st.textContent = "Le password non coincidono."; return; }
      st.className = "status info"; st.textContent = "Cambio in corso...";
      try { await window.AuthService.changePassword(cur, nw); st.className = "status success"; st.textContent = "Password cambiata!"; setTimeout(() => { modal.style.display = "none"; location.reload(); }, 1500); }
      catch (err) { st.className = "status error"; st.textContent = err.message || "Errore."; }
    };
  }

  function formatCurrency(v) {
    return new Intl.NumberFormat("it-IT", { style: "currency", currency: "EUR" }).format(Number(v || 0));
  }

  function renderOrders(orders) {
    var body = document.getElementById("orders-body");
    if (!body) return;
    if (!Array.isArray(orders) || orders.length === 0) {
      body.innerHTML = "<p class='subtle'>Nessun ordine trovato.</p>";
      return;
    }
    body.innerHTML = orders.map(o => {
      var statoClass = o.stato === "Confermata" || o.stato === "Completato" ? "success" : o.stato === "Annullata" ? "danger" : "info";
      var dataOrdine = o.data ? new Date(o.data).toLocaleDateString("it-IT") + " " + new Date(o.data).toLocaleTimeString("it-IT", { hour: "2-digit", minute: "2-digit" }) : "";
      var righe = (o.righe || []).map(function (r) {
        var dett = "";
        try {
          var d = JSON.parse(r.dettaglio || "{}");
          if (d.film) dett = d.film + " - " + (d.cinema || "") + " | " + (d.data || "") + " " + (d.ora || "");
          else if (d.emailDestinatario) dett = "Per: " + d.emailDestinatario;
          else if (d.nome) dett = d.nome + (d.taglia ? " - Taglia: " + d.taglia : "");
          else if (d.posti && d.posti.length) dett = "Posti: " + d.posti.join(", ");
          else if (r.dettaglio && r.dettaglio.length < 50) dett = r.dettaglio;
        } catch { dett = r.dettaglio || ""; }
        return "<div style='display:flex;justify-content:space-between;align-items:center;padding:0.25rem 0'><span>" + r.descrizione + (dett ? " <span class='subtle' style='font-size:0.75rem'>" + dett + "</span>" : "") + (r.quantita > 1 ? " x" + r.quantita : "") + "</span><strong>" + formatCurrency(r.prezzo || 0) + "</strong></div>";
      }).join("");
      var footer = "";
      if (o.sconto > 0) footer += "<div style='display:flex;justify-content:space-between;font-size:0.8rem'><span>Sconto coupon</span><span style='color:var(--color-accent)'>-" + formatCurrency(o.sconto) + "</span></div>";
      if (o.importoGiftCard > 0) footer += "<div style='display:flex;justify-content:space-between;font-size:0.8rem'><span>Saldo gift card</span><span style='color:var(--color-accent)'>-" + formatCurrency(o.importoGiftCard) + "</span></div>";
      footer += "<div style='display:flex;justify-content:space-between;font-weight:700;margin-top:0.5rem;padding-top:0.5rem;border-top:1px solid var(--color-border)'><span>Totale</span><span>" + formatCurrency(o.totale) + "</span></div>";
      return `<div class="panel" style="margin-bottom:1rem">
        <div style="display:flex;justify-content:space-between;align-items:center;margin-bottom:0.5rem">
          <div><strong>${o.id}</strong> <span class="tag">${o.tipo}</span></div>
          <div><span class="tag ${statoClass}">${o.stato}</span></div>
        </div>
        <p class="subtle" style="font-size:0.75rem">${dataOrdine}</p>
        ${righe}
        ${footer}
      </div>`;
    }).join("");
  }

  async function loadOrders() {
    try {
      var orders = await window.ApiClient.get("/orders/mine");
      renderOrders(orders);
    } catch (err) {
      var body = document.getElementById("orders-body");
      if (body) body.innerHTML = "<p class='subtle'>Errore caricamento ordini: " + (err && err.message ? err.message : "errore sconosciuto") + "</p>";
    }
  }

  async function loadProfile() {
    setStatus("Caricamento...", "info");
    try {
      var [me, miePrenotazioni, giftCards] = await Promise.all([
        window.ApiClient.get("/auth/me"),
        window.ApiClient.get("/prenotazioni/mie"),
        window.ApiClient.get("/giftcards/mine")
      ]);
      emailInput.value = me.email || "";
      nomeInput.value = me.nome || "";
      cognomeInput.value = me.cognome || "";
      telefonoInput.value = me.telefono || "";
      renderPrenotazioni(miePrenotazioni);
      renderGiftCards(giftCards);
      renderSecurity(me);
      await loadOrders();
      setStatus("Account aggiornato.", "success");
    } catch (error) { prenotazioniBody.innerHTML = "<tr><td colspan='8' class='subtle'>Errore caricamento.</td></tr>"; setStatus(`Errore: ${error.message}`, "error"); }
  }

  async function submitProfile(event) {
    event.preventDefault();
    var payload = { nome: nomeInput.value.trim(), cognome: cognomeInput.value.trim(), telefono: telefonoInput.value.trim() };
    if (!payload.nome || !payload.cognome) { setStatus("Nome e cognome obbligatori.", "error"); return; }
    try { await window.ApiClient.put("/auth/me", payload); setStatus("Profilo salvato.", "success"); await loadProfile(); }
    catch (error) { setStatus(`Errore: ${error.message}`, "error"); }
  }

  async function handlePrenotazioniClick(event) {
    var actionButton = event.target.closest("button[data-action]");
    if (!actionButton) return;
    var action = actionButton.dataset.action;
    if (action === "download") { await downloadTicketPdf(actionButton.dataset.code); return; }
    var button = event.target.closest("button[data-action='cancel']");
    if (!button) return;
    var id = Number(button.dataset.id);
    if (!window.confirm(`Annullare la prenotazione #${id}? Riceverai un rimborso del 50% come gift card.`)) return;
    showCancelToast(id);
    try {
      if (window.AuthService) await window.AuthService.ensureValidAccessToken();
      await window.ApiClient.put(`/prenotazioni/${id}/annulla`, {});
      setStatus("Prenotazione annullata con rimborso 50%.", "success");
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
