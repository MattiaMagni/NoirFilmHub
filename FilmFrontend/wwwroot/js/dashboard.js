(function () {
  const statusEl = document.getElementById("dashboard-status");
  const kpiFilms = document.getElementById("kpi-films");
  const kpiRegisti = document.getElementById("kpi-registi");
  const kpiCinemas = document.getElementById("kpi-cinemas");
  const kpiProiezioni = document.getElementById("kpi-proiezioni");
  const latestFilmsEl = document.getElementById("latest-films");
  const spotlightEl = document.getElementById("dashboard-spotlight");
  const cityLoadEl = document.getElementById("city-load");
  const nextShowsEl = document.getElementById("next-shows");

  function setStatus(message, kind) {
    if (!statusEl) {
      return;
    }
    statusEl.className = "status " + kind;
    statusEl.textContent = message;
  }

  function toDateLabel(value) {
    if (!value) {
      return "-";
    }
    return String(value).slice(0, 10);
  }

  function toTimeLabel(value) {
    if (!value) {
      return "-";
    }
    const d = new Date(value);
    const h = String(d.getHours()).padStart(2, "0");
    const m = String(d.getMinutes()).padStart(2, "0");
    return `${h}:${m}`;
  }

  function renderLatestFilms(films) {
    if (!latestFilmsEl) {
      return;
    }
    const sorted = [...films].sort((a, b) => String(b.dataProduzione).localeCompare(String(a.dataProduzione)));
    if (sorted.length === 0) {
      latestFilmsEl.innerHTML = "<li class='subtle'>Nessun film disponibile.</li>";
      return;
    }
    latestFilmsEl.innerHTML = sorted.slice(0, 6)
      .map((f) => `<li><strong>${f.titolo}</strong><span>${toDateLabel(f.dataProduzione)}</span></li>`)
      .join("");
  }

  function renderSpotlight(films) {
    if (!spotlightEl) {
      return;
    }

    if (!films.length) {
      spotlightEl.innerHTML = "<p class='subtle'>Nessun film disponibile.</p>";
      return;
    }

    const picks = [...films]
      .sort((a, b) => Number(b.durata || 0) - Number(a.durata || 0))
      .slice(0, 3);

    spotlightEl.innerHTML = picks
      .map(
        (f) => `
        <article class="card">
          <div class="card-media">
            ${f.copertinaPath
              ? `<img src="${f.copertinaPath}" alt="Copertina ${f.titolo}" onerror="this.style.display='none';this.nextElementSibling.classList.add('show');">`
              : ""}
            <span class="media-fallback${f.copertinaPath ? "" : " show"}">Copertina non disponibile</span>
          </div>
          <div class="card-body">
            <h3>${f.titolo}</h3>
            <p class="subtle">Durata ${f.durata} min</p>
          </div>
        </article>
      `
      )
      .join("");
  }

  function renderCityLoad(cinemas, proiezioni) {
    if (!cityLoadEl) {
      return;
    }
    const cityByCinemaId = new Map(cinemas.map((c) => [c.id, c.citta]));
    const map = new Map();

    proiezioni.forEach((p) => {
      const city = cityByCinemaId.get(p.cinemaId) || "N/D";
      map.set(city, (map.get(city) || 0) + 1);
    });

    const rows = [...map.entries()].sort((a, b) => b[1] - a[1]).slice(0, 6);
    if (rows.length === 0) {
      cityLoadEl.innerHTML = "<li class='subtle'>Nessun dato disponibile.</li>";
      return;
    }
    cityLoadEl.innerHTML = rows
      .map(([city, count]) => `<li><strong>${city}</strong><span class="tag info">${count} slot</span></li>`)
      .join("");
  }

  function renderNextShows(proiezioni, films, cinemas) {
    if (!nextShowsEl) {
      return;
    }
    const filmById = new Map(films.map((f) => [f.id, f.titolo]));
    const cinemaById = new Map(cinemas.map((c) => [c.id, c.nome]));

    const sorted = [...proiezioni].sort((a, b) => {
      const ad = String(a.data);
      const bd = String(b.data);
      if (ad === bd) {
        return String(a.ora).localeCompare(String(b.ora));
      }
      return ad.localeCompare(bd);
    });

    if (sorted.length === 0) {
      nextShowsEl.innerHTML = "<tr><td colspan='5' class='subtle'>Nessuna proiezione programmata.</td></tr>";
      return;
    }

    nextShowsEl.innerHTML = sorted.slice(0, 8)
      .map((p) => `
        <tr>
          <td>${filmById.get(p.filmId) || `Film #${p.filmId}`}</td>
          <td>${cinemaById.get(p.cinemaId) || `Cinema #${p.cinemaId}`}</td>
          <td>${toDateLabel(p.data)}</td>
          <td>${toTimeLabel(p.ora)}</td>
          <td><span class="tag secondary">Programmato</span></td>
        </tr>
      `)
      .join("");
  }

  async function initDashboard() {
    setStatus("Caricamento dashboard...", "info");
    try {
      const [films, registi, cinemas, proiezioni] = await Promise.all([
        window.ApiClient.get("/films"),
        window.ApiClient.get("/registi"),
        window.ApiClient.get("/cinemas"),
        window.ApiClient.get("/proiezioni")
      ]);

      if (kpiFilms) {
        kpiFilms.textContent = Array.isArray(films) ? films.length : 0;
      }
      if (kpiRegisti) {
        kpiRegisti.textContent = Array.isArray(registi) ? registi.length : 0;
      }
      if (kpiCinemas) {
        kpiCinemas.textContent = Array.isArray(cinemas) ? cinemas.length : 0;
      }
      if (kpiProiezioni) {
        kpiProiezioni.textContent = Array.isArray(proiezioni) ? proiezioni.length : 0;
      }

      renderLatestFilms(Array.isArray(films) ? films : []);
      renderSpotlight(Array.isArray(films) ? films : []);
      renderCityLoad(Array.isArray(cinemas) ? cinemas : [], Array.isArray(proiezioni) ? proiezioni : []);
      renderNextShows(
        Array.isArray(proiezioni) ? proiezioni : [],
        Array.isArray(films) ? films : [],
        Array.isArray(cinemas) ? cinemas : []
      );

      setStatus("Dashboard aggiornata in tempo reale dagli endpoint.", "success");
    } catch (error) {
      setStatus(`Errore caricamento dashboard: ${error.message}`, "error");
    }
  }

  window.initDashboard = initDashboard;
})();
