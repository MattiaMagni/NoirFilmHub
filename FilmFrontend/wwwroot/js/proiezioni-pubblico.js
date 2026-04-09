(function () {
  const tableBody = document.getElementById("public-proiezioni-body");
  const statusEl = document.getElementById("public-proiezioni-status");

  function setStatus(message, kind) {
    statusEl.className = "status " + kind;
    statusEl.textContent = message;
  }

  function formatDate(value) {
    return String(value || "").slice(0, 10);
  }

  function formatTime(value) {
    const d = new Date(value);
    const hh = String(d.getHours()).padStart(2, "0");
    const mm = String(d.getMinutes()).padStart(2, "0");
    return `${hh}:${mm}`;
  }

  function renderRows(proiezioni, films, cinemas) {
    const filmById = new Map((films || []).map((f) => [f.id, f.titolo]));
    const cinemaById = new Map((cinemas || []).map((c) => [c.id, c.nome]));

    const today = new Date();
    today.setHours(0, 0, 0, 0);
    const inCorso = (Array.isArray(proiezioni) ? proiezioni : []).filter((p) => {
      const d = new Date(p.data);
      return !Number.isNaN(d.getTime()) && d >= today;
    });

    if (inCorso.length === 0) {
      tableBody.innerHTML = "<tr><td colspan='5' class='subtle'>Nessuna proiezione disponibile.</td></tr>";
      return;
    }

    tableBody.innerHTML = inCorso
      .map((p) => `
        <tr>
          <td>${filmById.get(p.filmId) || `Film #${p.filmId}`}</td>
          <td>${cinemaById.get(p.cinemaId) || `Cinema #${p.cinemaId}`}</td>
          <td>${formatDate(p.data)}</td>
          <td>${formatTime(p.ora)}</td>
          <td>
            <button class="btn-small primary" data-action="book" data-id="${p.id}">Prenota</button>
          </td>
        </tr>
      `)
      .join("");
  }

  async function loadData() {
    setStatus("Caricamento proiezioni...", "info");
    try {
      const [proiezioni, films, cinemas] = await Promise.all([
        window.ApiClient.get("/proiezioni"),
        window.ApiClient.get("/films"),
        window.ApiClient.get("/cinemas")
      ]);

      renderRows(proiezioni, films, cinemas);
      setStatus("Proiezioni caricate.", "success");
    } catch (error) {
      tableBody.innerHTML = "";
      setStatus(`Errore: ${error.message}`, "error");
    }
  }

  async function onTableClick(event) {
    const button = event.target.closest("button[data-action='book']");
    if (!button) {
      return;
    }

    const proiezioneId = Number(button.dataset.id);
    if (!window.AuthService || !window.AuthService.isAuthenticated()) {
      if (window.AuthService) {
        const currentPath = `${window.location.pathname || "/proiezioni-pubblico.html"}${window.location.search || ""}${window.location.hash || ""}`;
        window.AuthService.saveRedirect(currentPath);
      }
      setStatus("Per prenotare devi effettuare il login.", "info");
      window.location.replace("/login.html");
      return;
    }

    const raw = window.prompt("Numero posti da prenotare", "1");
    const numeroPosti = Number(raw);
    if (!numeroPosti || numeroPosti <= 0) {
      setStatus("Numero posti non valido.", "error");
      return;
    }

    try {
      await window.ApiClient.post("/prenotazioni", {
        proiezioneId,
        numeroPosti
      });
      setStatus("Prenotazione registrata nell'area personale.", "success");
    } catch (error) {
      setStatus(`Errore prenotazione: ${error.message}`, "error");
    }
  }

  async function initPublicProiezioniPage() {
    if (!tableBody) {
      return;
    }
    tableBody.addEventListener("click", onTableClick);
    await loadData();
  }

  window.initPublicProiezioniPage = initPublicProiezioniPage;
})();
