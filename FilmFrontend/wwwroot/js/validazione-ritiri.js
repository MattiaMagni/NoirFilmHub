(function () {
  const statusEl = document.getElementById("ritiro-status");
  const codeInput = document.getElementById("ritiro-code");
  const lookupBtn = document.getElementById("ritiro-lookup");
  const scanToggleBtn = document.getElementById("ritiro-scan-toggle");
  const scannerDiv = document.getElementById("qr-scanner");
  const detailsEl = document.getElementById("ritiro-details");
  const actionsEl = document.getElementById("ritiro-actions");
  const confirmBtn = document.getElementById("ritiro-confirm");
  const summaryEl = document.getElementById("ritiro-summary");

  let currentOrder = null;
  let scanner = null;
  let scanning = false;

  function setStatus(message, kind) {
    statusEl.className = `status ${kind}`;
    statusEl.textContent = message;
  }

  function getCodeFromUrl() {
    const params = new URLSearchParams(window.location.search);
    return (params.get("codice") || "").trim();
  }

  function extractCodeFromQrText(text) {
    if (!text) return "";
    try {
      const url = new URL(text);
      const codeParam = url.searchParams.get("codice");
      if (codeParam) return codeParam;
      const segments = url.pathname.split("/").filter(Boolean);
      if (segments.length > 0) return segments[segments.length - 1];
    } catch {}
    return text.trim();
  }

  function formatDate(value) {
    if (!value) return "-";
    return new Date(value).toLocaleDateString("it-IT", { day: "2-digit", month: "2-digit", year: "numeric", hour: "2-digit", minute: "2-digit" });
  }

  function renderOrderDetails(order) {
    if (!order) {
      detailsEl.style.display = "none";
      actionsEl.style.display = "none";
      return;
    }

    const articoliHtml = (order.articoli || []).length
      ? order.articoli.map(a => `<li><strong>${a.nome}</strong> &times; ${a.quantita}</li>`).join("")
      : "<li>Nessun articolo</li>";

    const blocked = order.stato === "Ritirato" || order.stato === "Annullato";

    detailsEl.innerHTML = `
      <h3>Ordine ritiro</h3>
      <p class="subtle"><strong>Codice:</strong> ${order.codiceRitiro}</p>
      <p class="subtle"><strong>Data acquisto:</strong> ${formatDate(order.creatoIl)}</p>
      <p class="subtle"><strong>Stato:</strong> <span class="tag ${order.stato === "Ritirato" ? "success" : "info"}">${order.stato}</span></p>
      <p class="subtle"><strong>Articoli da ritirare:</strong></p>
      <ul>${articoliHtml}</ul>
    `;
    detailsEl.style.display = "block";
    actionsEl.style.display = blocked ? "none" : "flex";
  }

  function renderSummary(order) {
    const articoliHtml = (order.articoli || []).length
      ? order.articoli.map(a => `<li><strong>${a.nome}</strong> &times; ${a.quantita}</li>`).join("")
      : "<li>Nessun articolo</li>";

    summaryEl.innerHTML = `
      <h3 style="color:var(--color-primary)">Consegna effettuata</h3>
      <p class="subtle">Consegna i seguenti articoli al cliente:</p>
      <ul style="font-size:1.05rem;margin:0.75rem 0">${articoliHtml}</ul>
      <p class="subtle">Codice ritiro: <strong>${order.codiceRitiro}</strong></p>
      <div class="actions" style="margin-top:1rem">
        <button class="primary" id="ritiro-done" type="button">Fatto</button>
      </div>
    `;
    summaryEl.style.display = "block";
    detailsEl.style.display = "none";
    actionsEl.style.display = "none";
    document.getElementById("ritiro-done").addEventListener("click", resetPage);
  }

  function resetPage() {
    currentOrder = null;
    codeInput.value = "";
    detailsEl.style.display = "none";
    summaryEl.style.display = "none";
    actionsEl.style.display = "none";
    statusEl.className = "status info";
    statusEl.textContent = "Inserisci un codice di ritiro o scansiona il QR.";
  }

  async function lookupOrder() {
    const code = (codeInput.value || "").trim();
    if (!code) {
      setStatus("Inserisci un codice di ritiro.", "error");
      return;
    }

    setStatus("Ricerca ordine...", "info");
    try {
      const order = await window.ApiClient.get(`/ritiri/validate/${encodeURIComponent(code)}`);
      currentOrder = order;
      renderOrderDetails(order);
      setStatus("Ordine caricato.", "success");
    } catch (error) {
      currentOrder = null;
      renderOrderDetails(null);
      setStatus(`Errore ricerca: ${error.message}`, "error");
    }
  }

  async function confirmPickup() {
    if (!currentOrder || !currentOrder.codiceRitiro) {
      setStatus("Carica prima un ordine valido.", "error");
      return;
    }

    setStatus("Conferma ritiro in corso...", "info");
    confirmBtn.disabled = true;
    try {
      const result = await window.ApiClient.post(`/ritiri/${encodeURIComponent(currentOrder.codiceRitiro)}/ritira`, {});
      currentOrder.stato = result.stato;
      renderSummary(currentOrder);
      setStatus("Ritiro confermato con successo.", "success");
    } catch (error) {
      if (error.status === 409) {
        setStatus("Ordine gia ritirato.", "error");
        currentOrder.stato = "Ritirato";
        renderOrderDetails(currentOrder);
      } else {
        setStatus(`Errore: ${error.message}`, "error");
      }
    } finally {
      confirmBtn.disabled = false;
    }
  }

  function startScanner() {
    if (scanning) return;
    scannerDiv.innerHTML = "";
    scanning = true;
    scanToggleBtn.textContent = "Ferma scansione";

    scanner = new Html5Qrcode("qr-scanner");
    scanner.start(
      { facingMode: "environment" },
      { fps: 10, qrbox: { width: 250, height: 250 } },
      (decodedText) => {
        const code = extractCodeFromQrText(decodedText);
        scanner.stop().then(() => {
          scanning = false;
          scanToggleBtn.textContent = "Scansiona QR";
          scannerDiv.innerHTML = "";
          scanner = null;
          if (code) {
            codeInput.value = code;
            lookupOrder();
          }
        }).catch(() => {
          scanning = false;
          scanToggleBtn.textContent = "Scansiona QR";
          scannerDiv.innerHTML = "";
          scanner = null;
        });
      },
      () => {}
    ).catch(err => {
      setStatus("Errore fotocamera: " + (err.message || err), "error");
      scanning = false;
      scanToggleBtn.textContent = "Scansiona QR";
      scannerDiv.innerHTML = "";
      scanner = null;
    });
  }

  function stopScanner() {
    if (scanner) {
      scanner.stop().then(() => {
        scanning = false;
        scanToggleBtn.textContent = "Scansiona QR";
        scannerDiv.innerHTML = "";
        scanner = null;
      }).catch(() => {
        scanning = false;
        scanToggleBtn.textContent = "Scansiona QR";
        scannerDiv.innerHTML = "";
        scanner = null;
      });
    }
  }

  function toggleScanner() {
    if (scanning) {
      stopScanner();
    } else {
      startScanner();
    }
  }

  async function initValidazioneRitiriPage() {
    const fromUrl = getCodeFromUrl();
    if (fromUrl) {
      codeInput.value = fromUrl;
    }

    lookupBtn.addEventListener("click", lookupOrder);
    confirmBtn.addEventListener("click", confirmPickup);
    scanToggleBtn.addEventListener("click", toggleScanner);

    if (fromUrl) {
      await lookupOrder();
    }
  }

  window.initValidazioneRitiriPage = initValidazioneRitiriPage;
})();
