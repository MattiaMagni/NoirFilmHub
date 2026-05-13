(function () {
	const statusEl = document.getElementById("acquista-status");
	const summaryEl = document.getElementById("acquista-summary");
	const seatGridEl = document.getElementById("seat-grid");
	const selectedListEl = document.getElementById("selected-seats-list");
	const timerEl = document.getElementById("seat-lock-timer");
	const continueBtn = document.getElementById("acquista-continue");
	const selectedCountEl = document.getElementById("selected-seats-count");
	const selectedUnitPriceEl = document.getElementById("selected-seat-price");
	const selectedVipExtraEl = document.getElementById("selected-seat-vip-extra");
	const selectedTotalEl = document.getElementById("selected-seats-total");
	const vipNoteEl = document.getElementById("seat-vip-note");
	let summaryVipExtraEl = null;

	function toPositiveNumber(value, fallback = 0) {
		const parsed = Number(value);
		return Number.isFinite(parsed) && parsed >= 0 ? parsed : fallback;
	}

	let proiezioneId = null;
	let resolvedParams = {};
	let selectedSeats = new Set();
	let currentSeatState = null;
	let lockExpiresAt = null;
	let timerInterval = null;
	let prezzoBaseCorrente = 0;
	let vipSupplementCorrente = 0;
	let currentFilmTitolo = "";
	let currentCinemaNome = "";
	let currentShowData = "";
	let currentShowOra = "";

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

	function seatIsVip(code) {
		if (!currentSeatState) {
			return false;
		}
		if (currentSeatState.vipSet && currentSeatState.vipSet.has(code)) {
			return true;
		}

		const match = String(code || "").toUpperCase().match(/^([A-Z])(\d+)$/);
		if (!match) {
			return false;
		}

		const rowIndex = match[1].charCodeAt(0) - 65;
		const colIndex = Number(match[2]) - 1;
		const rows = Number(currentSeatState.numeroFile) || 10;
		const cols = Number(currentSeatState.postiPerFila) || 12;
		const aisleStart = cols >= 10 ? Math.floor(cols / 2) - 1 : -1;
		const aisleEnd = cols >= 10 ? aisleStart + 1 : -1;
		return isVipSeat(rowIndex, colIndex, rows, cols, aisleStart, aisleEnd);
	}

	function vipCountLabel(count) {
		return count === 1 ? "posto VIP" : "posti VIP";
	}

	function isVipSeat(rowIndex, colIndex, rows, cols, aisleStart, aisleEnd) {
		if (colIndex === aisleStart || colIndex === aisleEnd) {
			return false;
		}
		const vipRowStart = Math.max(1, Math.floor(rows * 0.35));
		const vipRowEnd = Math.min(rows - 2, Math.floor(rows * 0.75));
		if (rowIndex < vipRowStart || rowIndex > vipRowEnd) {
			return false;
		}

		const vipBand = Math.max(2, Math.floor(cols * 0.18));
		const leftVipStart = Math.max(0, aisleStart - vipBand);
		const rightVipEnd = Math.min(cols - 1, aisleEnd + vipBand);
		return (colIndex >= leftVipStart && colIndex < aisleStart) || (colIndex > aisleEnd && colIndex <= rightVipEnd);
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

		const centerAisleStart = cols >= 10 ? Math.floor(cols / 2) - 1 : -1;
		const centerAisleEnd = cols >= 10 ? centerAisleStart + 1 : -1;

		const htmlRows = [];
		for (let r = 0; r < rows; r += 1) {
			const colsHtml = [];
			colsHtml.push(`<span class="seat-aisle side" aria-hidden="true"></span>`);
			for (let c = 0; c < cols; c += 1) {
				if (c === centerAisleStart || c === centerAisleEnd) {
					colsHtml.push(`<span class="seat-aisle" aria-hidden="true"></span>`);
					continue;
				}

				const code = buildSeatCode(r, c);
				if (validSeats && !validSeats.has(code)) {
					colsHtml.push(`<button class="btn-small secondary seat-btn seat-gap" data-seat="${code}" disabled>--</button>`);
					continue;
				}
				const cls = seatButtonClass(code);
				const vipClass = seatIsVip(code) || isVipSeat(r, c, rows, cols, centerAisleStart, centerAisleEnd) ? "seat-vip" : "";
				const disabled = seatButtonDisabled(code) ? "disabled" : "";
				const label = selectedSeats.has(code) ? "✓" : (currentSeatState.soldSet.has(code) ? "x" : "");
				const vipTitle = vipClass
					? `Posto ${code} VIP (+${formatCurrency(vipSupplementCorrente)})`
					: `Posto ${code}`;
				colsHtml.push(`<button class="btn-small ${cls} seat-btn ${vipClass}" data-seat="${code}" title="${vipTitle}" aria-label="${vipTitle}" ${disabled}>${label}</button>`);
			}
			colsHtml.push(`<span class="seat-aisle side" aria-hidden="true"></span>`);
			const rowLetter = String.fromCharCode(65 + r);
			htmlRows.push(`<div class="seat-row"><span class="seat-row-label">${rowLetter}</span>${colsHtml.join("")}<span class="seat-row-label right">${rowLetter}</span></div>`);
		}

		const colLabels = [];
		colLabels.push('<span class="seat-aisle side" aria-hidden="true"></span>');
		for (let c = 0; c < cols; c += 1) {
			if (c === centerAisleStart || c === centerAisleEnd) {
				colLabels.push('<span class="seat-col-label gap"></span>');
				continue;
			}
			colLabels.push(`<span class="seat-col-label">${c + 1}</span>`);
		}
		colLabels.push('<span class="seat-aisle side" aria-hidden="true"></span>');

		seatGridEl.innerHTML = `
			<div class="seat-legend">
				<span class="seat-legend-item"><span class="seat-sample available"></span> Libero</span>
				<span class="seat-legend-item"><span class="seat-sample vip"></span> VIP</span>
				<span class="seat-legend-item"><span class="seat-sample selected"></span> Selezionato</span>
				<span class="seat-legend-item"><span class="seat-sample sold"></span> Venduto</span>
				<span class="seat-legend-item"><span class="seat-sample locked"></span> Bloccato</span>
			</div>
			<div class="seat-col-headers"><span class="seat-row-label"></span>${colLabels.join("")}<span class="seat-row-label"></span></div>
			${htmlRows.join("")}
			<div class="seat-col-headers bottom"><span class="seat-row-label"></span>${colLabels.join("")}<span class="seat-row-label"></span></div>
		`;
		selectedListEl.textContent = Array.from(selectedSeats).sort().join(", ") || "Nessuno";
		updateSelectionSummary();
	}

	function updateSelectionSummary() {
		const count = selectedSeats.size;
		const selectedArray = Array.from(selectedSeats);
		const vipCount = selectedArray.filter((seat) => seatIsVip(seat)).length;
		const standardCount = count - vipCount;
		const basePrice = toPositiveNumber(prezzoBaseCorrente, 0);
		const vipSupplement = toPositiveNumber(vipSupplementCorrente, 0);
		const vipExtraTotal = vipCount * vipSupplement;
		const total = (standardCount * basePrice) + (vipCount * (basePrice + vipSupplement));

		if (selectedCountEl) {
			selectedCountEl.textContent = String(count);
		}
		if (selectedUnitPriceEl) {
			if (vipSupplement > 0) {
				selectedUnitPriceEl.textContent = `${formatCurrency(basePrice)} / VIP ${formatCurrency(basePrice + vipSupplement)}`;
			} else {
				selectedUnitPriceEl.textContent = formatCurrency(basePrice);
			}
		}
		if (selectedVipExtraEl) {
			if (vipSupplement > 0) {
				selectedVipExtraEl.textContent = vipCount > 0
					? `+${formatCurrency(vipExtraTotal)} (${vipCount} ${vipCountLabel(vipCount)})`
					: `+${formatCurrency(vipSupplement)} per posto VIP`;
			} else {
				selectedVipExtraEl.textContent = "Nessuno";
			}
		}
		if (selectedTotalEl) {
			selectedTotalEl.textContent = formatCurrency(total);
		}

		summaryVipExtraEl = document.getElementById("acquista-summary-vip-extra");
		if (summaryVipExtraEl) {
			summaryVipExtraEl.textContent = vipSupplement > 0
				? `Supplemento VIP: +${formatCurrency(vipSupplement)} per posto`
				: "Supplemento VIP: non previsto";
		}

		if (vipNoteEl) {
			if (vipSupplement > 0) {
				const seatWord = vipCountLabel(vipCount);
				const verb = vipCount === 1 ? "applicato" : "applicati";
				vipNoteEl.textContent = vipCount > 0
					? `Hai selezionato ${vipCount} ${seatWord}: ${verb} supplemento di ${formatCurrency(vipExtraTotal)}.`
					: `Nota: i posti VIP costano ${formatCurrency(vipSupplement)} in piu rispetto al prezzo base.`;
			} else {
				vipNoteEl.textContent = "";
			}
		}

		if (continueBtn) {
			continueBtn.disabled = count === 0;
		}
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
		prezzoBaseCorrente = Number(show.prezzoBase || 0);
		currentFilmTitolo = film.titolo || "";
		currentCinemaNome = cinema.nome || "";
		currentShowData = data;
		currentShowOra = ora;
		summaryEl.innerHTML = `
			<h3>${film.titolo}</h3>
			<p class="subtle">Cinema: ${cinema.nome} (${cinema.citta})</p>
			<p class="subtle">Data: ${data} - Ora: ${ora}</p>
			<p class="subtle">Sala: ${p.idSala} (${salaNome})</p>
			<p class="subtle">Prezzo base: ${formatCurrency(prezzoBaseCorrente)}</p>
			<p id="acquista-summary-vip-extra" class="subtle">Supplemento VIP: in aggiornamento...</p>
		`;
		updateSelectionSummary();
	}

	async function loadSeatState() {
		const payload = await window.ApiClient.get(`/checkout/seats/${proiezioneId}`);
		currentSeatState = {
			...payload,
			soldSet: new Set(payload.sold || []),
			lockedByOthersSet: new Set(payload.lockedByOthers || []),
			myLocksSet: new Set(payload.myLocks || []),
			vipSet: new Set(payload.vipSeats || [])
		};

		selectedSeats = new Set(payload.myLocks || []);
		prezzoBaseCorrente = toPositiveNumber(payload.prezzoBase, prezzoBaseCorrente);
		vipSupplementCorrente = toPositiveNumber(payload.vipSupplement, vipSupplementCorrente);
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

		const addCartBtn = document.getElementById("acquista-add-cart");
		if (addCartBtn) {
			addCartBtn.addEventListener("click", async () => {
				if (!selectedSeats.size) {
					setStatus("Seleziona almeno un posto.", "error");
					return;
				}

				addCartBtn.disabled = true;
				addCartBtn.textContent = "Aggiunta...";
				setStatus("Aggiunta al carrello...", "info");

				try {
					const guestToken = sessionStorage.getItem("cart_guest_token");
					const headers = {};
					if (guestToken && !(window.AuthService && window.AuthService.isAuthenticated())) {
						headers["X-Guest-Token"] = guestToken;
					}
					const cart = await window.ApiClient.post("/cart", null, headers);
					if (cart && cart.guestToken && !cart.utenteId) {
						sessionStorage.setItem("cart_guest_token", cart.guestToken);
					}

					// Calculate VIP pricing per seat
					const seatsArray = Array.from(selectedSeats);
					const vipSeats = seatsArray.filter(s => seatIsVip(s));
					const standardSeats = seatsArray.filter(s => !seatIsVip(s));
					const vipPrice = prezzoBaseCorrente + vipSupplementCorrente;

					// Add standard seats as one item (VariantId=0)
					if (standardSeats.length > 0) {
						await window.ApiClient.post(`/cart/${cart.id}/items`, {
							itemType: "Ticket", itemId: proiezioneId, variantId: 0,
							quantita: standardSeats.length, prezzoUnitario: prezzoBaseCorrente,
							dettaglioJson: JSON.stringify({ posti: standardSeats, tipo: "standard", film: currentFilmTitolo, cinema: currentCinemaNome, data: currentShowData, ora: currentShowOra })
						});
					}
					// Add VIP seats as separate item (VariantId=1)
					if (vipSeats.length > 0) {
						await window.ApiClient.post(`/cart/${cart.id}/items`, {
							itemType: "Ticket", itemId: proiezioneId, variantId: 1,
							quantita: vipSeats.length, prezzoUnitario: vipPrice,
							dettaglioJson: JSON.stringify({ posti: vipSeats, tipo: "vip", film: currentFilmTitolo, cinema: currentCinemaNome, data: currentShowData, ora: currentShowOra })
						});
					}

					showCartToast();
					setStatus("Ticket aggiunto al carrello!", "success");
					addCartBtn.textContent = "Aggiunto!";
				} catch (error) {
					setStatus(`Errore: ${error.message}`, "error");
					addCartBtn.disabled = false;
					addCartBtn.textContent = "Aggiungi al carrello";
				}
			});
		}
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

	function showCartToast() {
		var existing = document.getElementById("cart-toast-overlay");
		if (existing) existing.remove();
		var overlay = document.createElement("div");
		overlay.id = "cart-toast-overlay";
		overlay.className = "cart-toast-overlay";
		overlay.innerHTML = '<div class="cart-toast-card"><p class="cart-toast-icon">&#x1f6cd;</p><h3>Aggiunto al carrello!</h3><p class="subtle">Cosa vuoi fare?</p><div class="cart-toast-actions"><button class="button primary" id="cart-toast-goto">Vai al carrello</button><button class="button secondary" id="cart-toast-continue">Continua acquisti</button></div></div>';
		document.body.appendChild(overlay);
		document.getElementById("cart-toast-goto").onclick = function() { window.location.href = "/cart.html"; };
		document.getElementById("cart-toast-continue").onclick = function() { overlay.remove(); };
		overlay.onclick = function(e) { if (e.target === overlay) overlay.remove(); };
	}
})();
