(function () {
  const tableBody = document.getElementById("public-proiezioni-body");
  const statusEl = document.getElementById("public-proiezioni-status");

  function setStatus(message, kind) {
    statusEl.className = "status " + kind;
    statusEl.textContent = message;
  }

  function formatDate(value) {
    if (window.ShowUtils) {
      return window.ShowUtils.formatShowDate(value);
    }
    return String(value || "").slice(0, 10);
  }

  function formatTime(value) {
    if (window.ShowUtils) {
      return window.ShowUtils.formatShowTime(value);
    }

    const raw = String(value || "");
    const hhmm = raw.length >= 16 ? raw.slice(11, 16) : raw.slice(0, 5);
    if (/^\d{2}:\d{2}$/.test(hhmm)) {
      return hhmm;
    }
    const d = new Date(value);
    if (Number.isNaN(d.getTime())) {
      return "--:--";
    }
    const hh = String(d.getHours()).padStart(2, "0");
    const mm = String(d.getMinutes()).padStart(2, "0");
    return `${hh}:${mm}`;
  }

  function toShowDateTime(proiezione) {
    if (window.ShowUtils) {
      return window.ShowUtils.parseShowDate(proiezione && proiezione.data, proiezione && proiezione.ora);
    }

    const day = formatDate(proiezione && proiezione.data);
    const time = formatTime(proiezione && proiezione.ora);
    if (!day || !/^\d{2}:\d{2}$/.test(time)) {
      return null;
    }
    const date = new Date(`${day}T${time}:00`);
    return Number.isNaN(date.getTime()) ? null : date;
  }

  function renderRows(proiezioni, films, cinemas) {
    const filmById = new Map((films || []).map((f) => [f.id, f.titolo]));
    const cinemaById = new Map((cinemas || []).map((c) => [c.id, c.nome]));

    const allRows = (Array.isArray(proiezioni) ? proiezioni : [])
      .slice()
      .sort((a, b) => {
        const aDate = `${formatDate(a.data)}T${formatTime(a.ora)}:00`;
        const bDate = `${formatDate(b.data)}T${formatTime(b.ora)}:00`;
        return aDate.localeCompare(bDate);
      });

    const now = new Date();
    const futureRows = allRows.filter((p) => {
      const showAt = toShowDateTime(p);
      return showAt ? showAt >= now : false;
    });

    const rows = futureRows.length > 0
      ? futureRows
      : allRows.slice().sort((a, b) => {
          const aDate = `${formatDate(a.data)}T${formatTime(a.ora)}:00`;
          const bDate = `${formatDate(b.data)}T${formatTime(b.ora)}:00`;
          return bDate.localeCompare(aDate);
        });

    if (rows.length === 0) {
      tableBody.innerHTML = "<tr><td colspan='5' class='subtle'>Nessuna proiezione disponibile.</td></tr>";
      return { mode: "empty", count: 0 };
    }

    tableBody.innerHTML = rows
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

    return {
      mode: futureRows.length > 0 ? "future" : "fallback",
      count: rows.length
    };
  }

  async function loadData() {
    setStatus("Caricamento proiezioni...", "info");
    try {
      const [proiezioni, films, cinemas] = await Promise.all([
        window.ApiClient.get("/proiezioni"),
        window.ApiClient.get("/films"),
        window.ApiClient.get("/cinemas")
      ]);

      const rendered = renderRows(proiezioni, films, cinemas);
      if (rendered.mode === "fallback") {
        setStatus("Nessuna proiezione futura: mostro le piu recenti.", "info");
      } else if (rendered.mode === "empty") {
        setStatus("Nessuna proiezione disponibile.", "info");
      } else {
        setStatus("Proiezioni future caricate.", "success");
      }
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
    const row = button.closest("tr");
    const cinemaCell = row ? row.children[1] : null;
    const filmCell = row ? row.children[0] : null;
    const selectedCinemaName = cinemaCell ? cinemaCell.textContent.trim() : "";
    const selectedFilmName = filmCell ? filmCell.textContent.trim() : "";

    try {
      const proiezione = await window.ApiClient.get(`/proiezioni/${proiezioneId}`);
      const destination = `/acquista.html?idCinema=${proiezione.cinemaId}&idFilm=${proiezione.filmId}&idSala=${proiezione.salaId || 0}&idShow=${proiezioneId}`;

      if (!window.AuthService || !window.AuthService.isAuthenticated()) {
        if (window.AuthService) {
          const loginUrl = window.AuthService.buildLoginUrl ? window.AuthService.buildLoginUrl(destination) : "/login.html";
          window.location.replace(loginUrl);
          return;
        }
        setStatus("Per prenotare devi effettuare il login.", "info");
        window.location.replace("/login.html");
        return;
      }

      window.location.href = destination;
    } catch {
      setStatus(`Impossibile aprire acquisto per ${selectedFilmName} / ${selectedCinemaName}.`, "error");
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
