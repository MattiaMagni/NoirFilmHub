(function () {
  const statusEl = document.getElementById("validazione-status");
  const codeInput = document.getElementById("ticket-code");
  const lookupBtn = document.getElementById("ticket-lookup");
  const validateBtn = document.getElementById("ticket-validate");
  const detailsEl = document.getElementById("ticket-details");

  let currentTicket = null;

  function setStatus(message, kind) {
    statusEl.className = `status ${kind}`;
    statusEl.textContent = message;
  }

  function getCodeFromUrl() {
    const params = new URLSearchParams(window.location.search);
    return (params.get("codice") || "").trim();
  }

  function renderTicketDetails(ticket) {
    if (!ticket) {
      detailsEl.innerHTML = "<p class='subtle'>Nessun biglietto caricato.</p>";
      return;
    }

    detailsEl.innerHTML = `
      <article class="panel">
        <h3>${ticket.film || "Film"}</h3>
        <p class="subtle"><strong>Codice:</strong> ${ticket.codiceAcquisto}</p>
        <p class="subtle"><strong>Cinema:</strong> ${ticket.cinema}</p>
        <p class="subtle"><strong>Data/Ora:</strong> ${String(ticket.data || "").slice(0, 10)} ${String(ticket.ora || "").slice(11, 16)}</p>
        <p class="subtle"><strong>Posti:</strong> ${ticket.postiSelezionati || "-"}</p>
        <p class="subtle"><strong>Stato:</strong> ${ticket.stato || "-"}</p>
        <p class="subtle"><strong>Validato:</strong> ${ticket.validato ? "Si" : "No"}</p>
      </article>
    `;
  }

  async function lookupTicket() {
    const code = (codeInput.value || "").trim();
    if (!code) {
      setStatus("Inserisci un codice acquisto.", "error");
      return;
    }

    setStatus("Ricerca biglietto...", "info");
    try {
      const ticket = await window.ApiClient.get(`/tickets/validate/${encodeURIComponent(code)}`);
      currentTicket = ticket;
      renderTicketDetails(ticket);
      setStatus("Biglietto caricato.", "success");
    } catch (error) {
      currentTicket = null;
      renderTicketDetails(null);
      setStatus(`Errore ricerca: ${error.message}`, "error");
    }
  }

  async function validateTicket() {
    if (!currentTicket || !currentTicket.codiceAcquisto) {
      setStatus("Carica prima un biglietto valido.", "error");
      return;
    }

    setStatus("Validazione biglietto in corso...", "info");
    try {
      const result = await window.ApiClient.post(`/tickets/${encodeURIComponent(currentTicket.codiceAcquisto)}/validate`, {});
      currentTicket = {
        ...currentTicket,
        validato: true,
        validatoAtUtc: result.validatoAtUtc,
        stato: "Validata"
      };
      renderTicketDetails(currentTicket);
      setStatus("Biglietto validato con successo.", "success");
    } catch (error) {
      if (error.status === 409) {
        setStatus("Biglietto gia validato.", "error");
        return;
      }
      setStatus(`Errore validazione: ${error.message}`, "error");
    }
  }

  async function initValidazioneBigliettiPage() {
    const fromUrl = getCodeFromUrl();
    if (fromUrl) {
      codeInput.value = fromUrl;
    }

    lookupBtn.addEventListener("click", lookupTicket);
    validateBtn.addEventListener("click", validateTicket);

    if (fromUrl) {
      await lookupTicket();
    }
  }

  window.initValidazioneBigliettiPage = initValidazioneBigliettiPage;
})();
