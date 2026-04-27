(function () {
  const statusEl = document.getElementById("esito-status");
  const bodyEl = document.getElementById("esito-body");

  function setStatus(message, kind) {
    statusEl.className = `status ${kind}`;
    statusEl.textContent = message;
  }

  function getSessionId() {
    const params = new URLSearchParams(window.location.search);
    return (params.get("session_id") || "").trim();
  }

  async function downloadTicketPdf(codiceAcquisto) {
    const code = String(codiceAcquisto || "").trim();
    if (!code) {
      setStatus("Codice acquisto non disponibile per il download PDF.", "error");
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

  async function initEsitoPagamentoPage() {
    const sessionId = getSessionId();
    if (!sessionId) {
      setStatus("Sessione Stripe mancante.", "error");
      return;
    }

    setStatus("Verifica esito pagamento Stripe...", "info");
    try {
      const result = await window.ApiClient.get(`/pagamenti/esito?session_id=${encodeURIComponent(sessionId)}`);

      if (result.stato !== "Confermata") {
        setStatus("Pagamento registrato ma prenotazione non ancora finalizzata. Riprova tra pochi secondi.", "info");
        bodyEl.innerHTML = `
          <article class="panel">
            <h3>Stato pagamento: ${result.stato || "In attesa"}</h3>
            <p class="subtle">Se hai appena pagato, attendi qualche secondo: il webhook Stripe puo impiegare un attimo.</p>
            <div class="actions">
              <button class="button secondary" type="button" id="retry-esito">Aggiorna stato</button>
              <a class="button primary" href="/profile.html">Vai al profilo</a>
            </div>
          </article>
        `;
        const retryBtn = document.getElementById("retry-esito");
        if (retryBtn) {
          retryBtn.addEventListener("click", () => window.location.reload());
        }
        return;
      }

      bodyEl.innerHTML = `
        <article class="panel">
          <h3>Pagamento completato</h3>
          <p class="subtle"><strong>Codice acquisto:</strong> ${result.codiceAcquisto || "-"}</p>
          <p class="subtle"><strong>Film:</strong> ${result.film || "-"}</p>
          <p class="subtle"><strong>Cinema:</strong> ${result.cinema || "-"}</p>
          <p class="subtle"><strong>Posti:</strong> ${result.postiSelezionati || "-"}</p>
          <p class="subtle"><strong>Totale:</strong> ${Number(result.totalePrezzo || 0).toFixed(2)} EUR</p>
          <div class="actions">
            <button class="button" type="button" id="download-ticket-pdf">Scarica PDF</button>
            <a class="button secondary" href="/programmazione.html">Torna alla programmazione</a>
            <a class="button primary" href="/profile.html">Vai al profilo</a>
          </div>
        </article>
      `;
      const downloadBtn = document.getElementById("download-ticket-pdf");
      if (downloadBtn) {
        downloadBtn.addEventListener("click", () => downloadTicketPdf(result.codiceAcquisto));
      }
      setStatus("Esito pagamento caricato.", "success");
    } catch (error) {
      setStatus(`Errore caricamento esito: ${error.message}`, "error");
      bodyEl.innerHTML = "";
    }
  }

  window.initEsitoPagamentoPage = initEsitoPagamentoPage;
})();
