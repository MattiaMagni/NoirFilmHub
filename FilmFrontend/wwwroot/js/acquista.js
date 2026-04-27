(function () {
	const statusEl = document.getElementById("acquista-status");
	const summaryEl = document.getElementById("acquista-summary");
	const seatGridEl = document.getElementById("seat-grid");
	const selectedListEl = document.getElementById("selected-seats-list");
	const timerEl = document.getElementById("seat-lock-timer");
	const continueBtn = document.getElementById("acquista-continue");

	let proiezioneId = null;
	let resolvedParams = {};
	let selectedSeats = new Set();
	let currentSeatState = null;
	let lockExpiresAt = null;
	let timerInterval = null;

	function setStatus(message, kind) {
		statusEl.className = `status ${kind}`;
		statusEl.textContent = message;
	}

	function getUrlParams() {
		const params = new URLSearchParams(window.location.search);
		const idShow = Number(params.get("idShow"));
		const idFilm = Number(params.get("idFilm"));
		const idCinema = Number(params.get("idCinema"));
		const idSala = Number(params.get("idSala"));

		return {
			idShow: idShow > 0 ? idShow : null,
			idFilm: idFilm > 0 ? idFilm : null,
			idCinema: idCinema > 0 ? idCinema : null,
			idSala: idSala > 0 ? idSala : null
		};
	}

	function formatCurrency(value) {
		return new Intl.NumberFormat("it-IT", { style: "currency", currency: "EUR" }).format(Number(value || 0));
	}

	function parseUtcMillis(value) {
		if (!value) {
			return null;
		}
		const raw = String(value).trim();
		if (!raw) {
			return null;
		}
		const hasZone = /(?:Z|[+-]\d{2}:\d{2})$/i.test(raw);
		const normalized = hasZone ? raw : `${raw}Z`;
		const ms = new Date(normalized).getTime();
		return Number.isNaN(ms) ? null : ms;
	}

	function startTimer() {
		if (timerInterval) {
			clearInterval(timerInterval);
			timerInterval = null;
		}

		if (!lockExpiresAt) {
			timerEl.textContent = "";
			return;
		}

		timerInterval = setInterval(() => {
			const now = Date.now();
			const diffMs = lockExpiresAt - now;
			if (diffMs <= 0) {
				clearInterval(timerInterval);
				timerInterval = null;
				timerEl.textContent = "Lock scaduto";
				setStatus("La selezione posti e scaduta. Verrai riportato alla scheda film.", "error");
				setTimeout(() => {
					const filmId = resolvedParams.idFilm || Number(new URLSearchParams(window.location.search).get("idFilm"));
					window.location.href = `/scheda-film.html?idFilm=${filmId}`;
				}, 1200);
				return;
			}

			const totalSec = Math.floor(diffMs / 1000);
			const min = String(Math.floor(totalSec / 60)).padStart(2, "0");
			const sec = String(totalSec % 60).padStart(2, "0");
			timerEl.textContent = `Tempo rimasto: ${min}:${sec}`;
		}, 500);
	}

	function buildSeatCode(row, col) {
		const rowLetter = String.fromCharCode(65 + row);
		return `${rowLetter}${col + 1}`;
	}

	function seatButtonClass(code) {
		if (!currentSeatState) {
			return "secondary";
		}
		if (selectedSeats.has(code)) {
			return "primary";
		}
		if (currentSeatState.soldSet.has(code)) {
			return "danger";
		}
		if (currentSeatState.lockedByOthersSet.has(code)) {
			return "secondary";
		}
		if (currentSeatState.myLocksSet.has(code)) {
			return "primary";
		}
		return "secondary";
	}

	function seatButtonDisabled(code) {
		if (!currentSeatState) {
			return true;
		}
		return currentSeatState.soldSet.has(code) || currentSeatState.lockedByOthersSet.has(code);
	}

	function renderSeats() {
		if (!currentSeatState) {
			seatGridEl.innerHTML = "<p class='subtle'>Piantina non disponibile.</p>";
			return;
		}

		const rows = Number(currentSeatState.numeroFile) || 10;
		const cols = Number(currentSeatState.postiPerFila) || 12;
		const validSeats = window.SeatMapUtils
			? new Set(window.SeatMapUtils.parseSeatMap(currentSeatState.mappaPostiJson, rows, cols))
			: null;

		const htmlRows = [];
		for (let r = 0; r < rows; r += 1) {
			const colsHtml = [];
			for (let c = 0; c < cols; c += 1) {
				const code = buildSeatCode(r, c);
				if (validSeats && !validSeats.has(code)) {
					colsHtml.push(`<button class="btn-small secondary seat-btn seat-gap" data-seat="${code}" disabled>--</button>`);
					continue;
				}
				const cls = seatButtonClass(code);
				const disabled = seatButtonDisabled(code) ? "disabled" : "";
				const label = selectedSeats.has(code) ? "✓" : code;
				colsHtml.push(`<button class="btn-small ${cls} seat-btn" data-seat="${code}" ${disabled}>${label}</button>`);
			}
			const rowLetter = String.fromCharCode(65 + r);
			htmlRows.push(`<div class="seat-row"><span class="seat-row-label">${rowLetter}</span>${colsHtml.join("")}</div>`);
		}

		const colLabels = [];
		for (let c = 0; c < cols; c += 1) {
			colLabels.push(`<span class="seat-col-label">${c + 1}</span>`);
		}

		seatGridEl.innerHTML = `
			<div class="seat-legend">
				<span class="seat-legend-item"><span class="seat-sample available"></span> Libero</span>
				<span class="seat-legend-item"><span class="seat-sample selected"></span> Selezionato</span>
				<span class="seat-legend-item"><span class="seat-sample sold"></span> Venduto</span>
				<span class="seat-legend-item"><span class="seat-sample locked"></span> Bloccato</span>
			</div>
			<div class="seat-col-headers"><span class="seat-row-label"></span>${colLabels.join("")}</div>
			${htmlRows.join("")}
		`;
		selectedListEl.textContent = Array.from(selectedSeats).sort().join(", ") || "Nessuno";
	}

	async function loadShowSummary() {
		const p = resolvedParams;
		const [film, cinema, show] = await Promise.all([
			window.ApiClient.get(`/films/${p.idFilm}`),
			window.ApiClient.get(`/cinemas/${p.idCinema}`),
			window.ApiClient.get(`/proiezioni/${p.idShow}`)
		]);

		const data = String(show.data || "").slice(0, 10);
		const ora = String(show.ora || "").slice(11, 16);
		const salaNome = show.tipologiaSala || "2D";
		summaryEl.innerHTML = `
			<h3>${film.titolo}</h3>
			<p class="subtle">Cinema: ${cinema.nome} (${cinema.citta})</p>
			<p class="subtle">Data: ${data} - Ora: ${ora}</p>
			<p class="subtle">Sala: ${p.idSala} (${salaNome})</p>
			<p class="subtle">Prezzo base: ${formatCurrency(show.prezzoBase || 0)}</p>
		`;
	}

	async function loadSeatState() {
		const payload = await window.ApiClient.get(`/checkout/seats/${proiezioneId}`);
		currentSeatState = {
			...payload,
			soldSet: new Set(payload.sold || []),
			lockedByOthersSet: new Set(payload.lockedByOthers || []),
			myLocksSet: new Set(payload.myLocks || [])
		};

		selectedSeats = new Set(payload.myLocks || []);
		lockExpiresAt = parseUtcMillis(payload.lockExpiresAtUtc);
		startTimer();
		renderSeats();
	}

	async function updateLocks() {
		if (selectedSeats.size === 0) {
			await window.ApiClient.delete(`/checkout/locks/${proiezioneId}`);
			lockExpiresAt = null;
			startTimer();
			return;
		}

		const payload = {
			proiezioneId,
			posti: Array.from(selectedSeats),
			lockMinutes: 10
		};

		const response = await window.ApiClient.post("/checkout/locks", payload);
		lockExpiresAt = parseUtcMillis(response.expiresAtUtc);
		startTimer();
	}

	function bindSeatEvents() {
		seatGridEl.addEventListener("click", async (event) => {
			const button = event.target.closest(".seat-btn");
			if (!button || button.disabled) {
				return;
			}

			const seat = button.dataset.seat;
			if (!seat) {
				return;
			}

			if (selectedSeats.has(seat)) {
				selectedSeats.delete(seat);
			} else {
				if (selectedSeats.size >= 10) {
					setStatus("Puoi selezionare al massimo 10 posti per acquisto.", "error");
					return;
				}
				selectedSeats.add(seat);
			}

			try {
				await updateLocks();
				await loadSeatState();
				setStatus("Posti aggiornati.", "success");
			} catch (error) {
				setStatus(`Errore lock posti: ${error.message}`, "error");
				await loadSeatState();
			}
		});

		continueBtn.addEventListener("click", () => {
			if (!selectedSeats.size) {
				setStatus("Seleziona almeno un posto per continuare.", "error");
				return;
			}

			const params = new URLSearchParams();
			params.set("idShow", resolvedParams.idShow);
			params.set("idFilm", resolvedParams.idFilm);
			params.set("idCinema", resolvedParams.idCinema);
			params.set("idSala", resolvedParams.idSala);
			params.set("posti", Array.from(selectedSeats).join(","));
			window.location.href = `/pagamento.html?${params.toString()}`;
		});
	}

	async function resolveParams(raw) {
		if (raw.idShow) {
			resolvedParams = { ...raw };
		} else {
			const params = new URLSearchParams(window.location.search);
			const idShowParam = params.get("idShow") || params.get("id") || params.get("proiezioneId");
			const idShow = Number(idShowParam);
			if (idShow > 0) {
				resolvedParams.idShow = idShow;
			} else {
				return false;
			}
		}

		if (!resolvedParams.idShow) {
			return false;
		}

		if (!resolvedParams.idFilm || !resolvedParams.idCinema || !resolvedParams.idSala) {
			setStatus("Risoluzione dati proiezione...", "info");
			try {
				const show = await window.ApiClient.get(`/proiezioni/${resolvedParams.idShow}`);
				if (!resolvedParams.idFilm && show.filmId) {
					resolvedParams.idFilm = show.filmId;
				}
				if (!resolvedParams.idCinema && show.cinemaId) {
					resolvedParams.idCinema = show.cinemaId;
				}
				if (!resolvedParams.idSala && show.salaId && show.salaId > 0) {
					resolvedParams.idSala = show.salaId;
				}
			} catch {
				return false;
			}
		}

		return !!(resolvedParams.idShow && resolvedParams.idFilm && resolvedParams.idCinema && resolvedParams.idSala);
	}

	async function initAcquistaPage() {
		const raw = getUrlParams();
		const valid = await resolveParams(raw);
		if (!valid) {
			setStatus("Parametri URL non validi. Torna alla scheda film per selezionare una proiezione.", "error");
			seatGridEl.innerHTML = "<p class='subtle'>Impossibile caricare la piantina senza dati proiezione.</p>";
			return;
		}

		proiezioneId = resolvedParams.idShow;
		setStatus("Caricamento dettaglio acquisto...", "info");

		try {
			await loadShowSummary();
			await loadSeatState();
			bindSeatEvents();
			setStatus("Seleziona i posti desiderati.", "success");
		} catch (error) {
			setStatus(`Errore caricamento acquisto: ${error.message}`, "error");
		}
	}

	window.initAcquistaPage = initAcquistaPage;
})();
