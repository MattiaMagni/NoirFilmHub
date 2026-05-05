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

  function formatDate(value) {
    return String(value || "").slice(0, 10);
  }

  function formatTime(value) {
    if (!value) {
      return "";
    }
    const d = new Date(value);
    const hh = String(d.getHours()).padStart(2, "0");
    const mm = String(d.getMinutes()).padStart(2, "0");
    return `${hh}:${mm}`;
  }

  async function downloadTicketPdf(codiceAcquisto) {
    const code = String(codiceAcquisto || "").trim();
    if (!code) {
      setStatus("Codice acquisto non valido per il download PDF.", "error");
      return;
    }

    try {
      const token = window.AuthService ? await window.AuthService.ensureValidAccessToken() : null;
      const headers = token ? { Authorization: `Bearer ${token}` } : {};
      const response = await fetch(`${window.AppConfig.API_BASE_URL}/tickets/${encodeURIComponent(code)}/pdf`, {
        method: "GET",
        headers
      });

      if (!response.ok) {
        throw new Error("Download PDF non riuscito");
      }

      const blob = await response.blob();
      const url = window.URL.createObjectURL(blob);
      const link = document.createElement("a");
      link.href = url;
      link.download = `ticket-${code}.pdf`;
      document.body.appendChild(link);
      link.click();
      link.remove();
      window.URL.revokeObjectURL(url);
    } catch (error) {
      setStatus(`Errore download PDF: ${error.message}`, "error");
    }
  }

  function renderPrenotazioni(rows) {
    if (!Array.isArray(rows) || rows.length === 0) {
      prenotazioniBody.innerHTML = "<tr><td colspan='8' class='subtle'>Nessuna programmazione salvata.</td></tr>";
      return;
    }

    prenotazioniBody.innerHTML = rows
      .map((p) => `
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
        </tr>
      `)
      .join("");
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

      setStatus("Profilo aggiornato.", "success");
    } catch (error) {
      renderPrenotazioni([]);
      setStatus(`Errore: ${error.message}`, "error");
    }
  }

  async function submitProfile(event) {
    event.preventDefault();
    const payload = {
      nome: nomeInput.value.trim(),
      cognome: cognomeInput.value.trim(),
      telefono: telefonoInput.value.trim()
    };

    if (!payload.nome || !payload.cognome) {
      setStatus("Nome e cognome obbligatori.", "error");
      return;
    }

    try {
      await window.ApiClient.put("/auth/me", payload);
      setStatus("Profilo salvato.", "success");
      await loadProfile();
    } catch (error) {
      setStatus(`Errore: ${error.message}`, "error");
    }
  }

  async function handlePrenotazioniClick(event) {
    const actionButton = event.target.closest("button[data-action]");
    if (!actionButton) {
      return;
    }

    const action = actionButton.dataset.action;
    if (action === "download") {
      await downloadTicketPdf(actionButton.dataset.code);
      return;
    }

    const button = event.target.closest("button[data-action='cancel']");
    if (!button) {
      return;
    }

    const id = Number(button.dataset.id);
    if (!window.confirm(`Confermi annullamento prenotazione #${id}?`)) {
      return;
    }

    try {
      await window.ApiClient.put(`/prenotazioni/${id}/annulla`, {});
      setStatus("Prenotazione annullata.", "success");
      await loadProfile();
    } catch (error) {
      setStatus(`Errore: ${error.message}`, "error");
    }
  }

  async function initProfilePage() {
    if (!form || !prenotazioniBody) {
      return;
    }
    form.addEventListener("submit", submitProfile);
    prenotazioniBody.addEventListener("click", handlePrenotazioniClick);
    await loadProfile();
  }

  window.initProfilePage = initProfilePage;
})();
