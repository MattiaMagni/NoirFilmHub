(function () {
  const statusEl = document.getElementById("pagamento-status");
  const summaryEl = document.getElementById("pagamento-summary");
  const confirmBtn = document.getElementById("pagamento-confirm");

  let context = null;

  function setStatus(message, kind) {
    statusEl.className = `status ${kind}`;
    statusEl.textContent = message;
  }

  function formatCurrency(value) {
    return new Intl.NumberFormat("it-IT", { style: "currency", currency: "EUR" }).format(Number(value || 0));
  }

  function getParams() {
    const params = new URLSearchParams(window.location.search);
    const idShow = Number(params.get("idShow"));
    const idFilm = Number(params.get("idFilm"));
    const idCinema = Number(params.get("idCinema"));
    const cancelled = params.get("cancelled") === "1";
    const postiRaw = (params.get("posti") || "").trim();
    const seats = postiRaw ? postiRaw.split(",").map((x) => x.trim().toUpperCase()).filter(Boolean) : [];

    return {
      idShow: idShow > 0 ? idShow : null,
      idFilm: idFilm > 0 ? idFilm : null,
      idCinema: idCinema > 0 ? idCinema : null,
      seats,
      cancelled
    };
  }

  async function loadContext() {
    const params = getParams();
    if (!params.idShow || !params.idFilm || !params.idCinema || !params.seats.length) {
      throw new Error("Parametri pagamento non validi");
    }

    const [show, film, cinema] = await Promise.all([
      window.ApiClient.get(`/proiezioni/${params.idShow}`),
      window.ApiClient.get(`/films/${params.idFilm}`),
      window.ApiClient.get(`/cinemas/${params.idCinema}`)
    ]);

    const totale = Number(show.prezzoBase || 0) * params.seats.length;
    context = {
      params,
      show,
      film,
      cinema,
      total: Number(totale.toFixed(2))
    };
  }

  function renderSummary() {
    const data = String(context.show.data || "").slice(0, 10);
    const ora = String(context.show.ora || "").slice(11, 16);
    summaryEl.innerHTML = `
      <h3>${context.film.titolo}</h3>
      <p class="subtle">Cinema: ${context.cinema.nome} (${context.cinema.citta})</p>
      <p class="subtle">Data/Ora: ${data} - ${ora}</p>
      <p class="subtle">Posti: ${context.params.seats.join(", ")}</p>
      <p class="subtle">Totale ordine: <strong>${formatCurrency(context.total)}</strong></p>
      <p class="subtle">Pagamento: solo carta (Stripe Hosted Checkout)</p>
    `;
  }

  function bindEvents() {
    confirmBtn.addEventListener("click", async function () {
      if (!context) {
        return;
      }
      if (this.disabled) return;
      this.disabled = true;
      this.textContent = "Pagamento in corso...";

      setStatus("Reindirizzamento verso Stripe in corso...", "info");

      try {
        const payload = {
          proiezioneId: context.params.idShow,
          postiSelezionati: context.params.seats.join(",")
        };

        const result = await window.ApiClient.post("/pagamenti/checkout-session", payload);
        if (!result || !result.url) {
          throw new Error("Sessione Stripe non disponibile");
        }

        window.location.href = result.url;
      } catch (error) {
        setStatus(`Errore pagamento: ${error.message}`, "error");
        this.disabled = false;
        this.textContent = "Paga con Stripe";
      }
    });
  }

  async function initPagamentoPage() {
    setStatus("Caricamento dati pagamento...", "info");
    try {
      await loadContext();
      renderSummary();
      bindEvents();

      if (context.params.cancelled) {
        setStatus("Pagamento annullato su Stripe. Puoi riprovare.", "error");
      } else {
        setStatus("Premi il pulsante per pagare su Stripe.", "success");
      }
    } catch (error) {
      setStatus(`Errore caricamento pagamento: ${error.message}`, "error");
    }
  }

  window.initPagamentoPage = initPagamentoPage;
})();
