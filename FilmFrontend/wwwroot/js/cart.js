(function () {
  const statusEl = document.getElementById("cart-status");
  const itemsEl = document.getElementById("cart-items");
  const summaryEl = document.getElementById("cart-summary");
  let cartId = null;

  function setStatus(msg, kind) {
    if (statusEl) { statusEl.className = `status ${kind}`; statusEl.textContent = msg; }
  }
  function formatCurrency(v) {
    return new Intl.NumberFormat("it-IT", { style: "currency", currency: "EUR" }).format(Number(v || 0));
  }
  function parseDetail(d) { try { return d ? JSON.parse(d) : {}; } catch { return {}; } }

  let cartRefreshTimer = null;

  async function loadCart() {
    setStatus("Caricamento carrello...", "info");
    try {
      const headers = {};
      const guestToken = sessionStorage.getItem("cart_guest_token");
      if (guestToken && !(window.AuthService && window.AuthService.isAuthenticated())) {
        headers["X-Guest-Token"] = guestToken;
      }
      const cart = await window.ApiClient.post("/cart", null, headers);
      if (!cart || !cart.items || cart.items.length === 0 || cart.stato === "Expired") {
        itemsEl.innerHTML = "<div class='panel' style='text-align:center;padding:2rem'><p>&#x1f6d2;</p><h3>Il tuo carrello e vuoto</h3><p class='subtle'>Aggiungi biglietti, gift card o merchandise dallo shop.</p><a class='button primary' href='/shop.html'>Vai allo shop</a></div>";
        summaryEl.classList.add("hidden");
        setStatus("Carrello vuoto.", "info");
        clearInterval(cartRefreshTimer);
        cartRefreshTimer = null;
        return;
      }
      if (cart.guestToken && !cart.utenteId) sessionStorage.setItem("cart_guest_token", cart.guestToken);
      cartId = cart.id;

      var prevItemCount = itemsEl.querySelectorAll(".cart-item-card").length;
      itemsEl.innerHTML = cart.items.map(item => renderCartItem(item)).join("");
      document.getElementById("cart-subtotale").textContent = formatCurrency(cart.subtotale);
      document.getElementById("cart-sconto").textContent = cart.scontoCoupon > 0 ? `-${formatCurrency(cart.scontoCoupon)}` : "-";
      document.getElementById("cart-giftcard-amount").textContent = cart.importoGiftCard > 0 ? `-${formatCurrency(cart.importoGiftCard)}` : "-";
      document.getElementById("cart-totale").textContent = formatCurrency(cart.totale);
      summaryEl.classList.remove("hidden");
      bindEvents();
      updateBadge(cart.items.length);

      if (cart.items.length !== prevItemCount && prevItemCount > 0) {
        setStatus("Alcuni biglietti sono scaduti e sono stati rimossi.", "info");
      } else {
        setStatus("Pronto per il checkout.", "success");
      }

      var hasTickets = cart.items.some(function(i) { return i.itemType === "Ticket"; });
      if (!cartRefreshTimer && hasTickets) {
        cartRefreshTimer = setInterval(loadCart, 30000);
      } else if (cartRefreshTimer && !hasTickets) {
        clearInterval(cartRefreshTimer);
        cartRefreshTimer = null;
      }
    } catch (e) {
      itemsEl.innerHTML = "<p class='subtle'>Errore caricamento carrello.</p>";
      setStatus(`Errore: ${e.message}`, "error");
    }
  }

  function updateBadge(count) {
    var b = document.getElementById("cart-badge");
    if (b) {
      b.textContent = count || "0";
      b.classList.toggle("hidden", !count || count <= 0);
    }
  }

  function renderCartItem(item) {
    var detail = parseDetail(item.dettaglioJson);
    var total = formatCurrency(item.prezzoUnitario * item.quantita);
    if (item.itemType === "Ticket") {
      var seats = detail.posti || [];
      var tipo = detail.tipo === "vip" ? "VIP" : "Standard";
      var film = detail.film || "";
      var cinema = detail.cinema || "";
      var data = detail.data || "";
      var ora = detail.ora || "";
      var projInfo = film ? `${film} — ${cinema}<br>${data} ${ora}` : "";
      return `<div class="cart-item-card panel">
        <div class="cart-item-main">
          <div class="cart-item-icon">&#x1f3ac;</div>
          <div class="cart-item-info">
            <h4>Biglietto cinema <span class="tag ${detail.tipo === 'vip' ? 'accent' : 'info'}">${tipo}</span></h4>
            ${projInfo ? '<p class="subtle" style="font-size:0.78rem">'+projInfo+'</p>' : ''}
            <p class="subtle">${seats.length} posto/i: ${seats.join(", ") || "N/D"}</p>
            <p class="subtle">${formatCurrency(item.prezzoUnitario)} cad.</p>
          </div>
        </div>
        <div class="cart-item-right">
          <strong>${total}</strong>
          <div class="cart-seat-chips">${seats.map(s => '<button class="btn-small secondary cart-remove-seat" data-item-id="'+item.id+'" data-seat="'+s+'">'+s+' ✕</button>').join(" ")}</div>
        </div>
      </div>`;
    }
    if (item.itemType === "GiftCard") {
      var email = detail.emailDestinatario || "";
      var messaggio = detail.messaggio || "";
      return `<div class="cart-item-card panel">
        <div class="cart-item-main">
          <div class="cart-item-icon">&#x1f381;</div>
          <div class="cart-item-info">
            <h4>Gift Card ${formatCurrency(item.prezzoUnitario)}</h4>
            ${email ? '<p class="subtle">Per: '+email+'</p>' : ''}
            ${messaggio ? '<p class="subtle" style="font-size:0.78rem;font-style:italic">"'+messaggio+'"</p>' : ''}
          </div>
        </div>
        <div class="cart-item-right">
          <strong>${total}</strong>
          <button class="btn-small secondary cart-remove" data-item-id="${item.id}">Rimuovi</button>
        </div>
      </div>`;
    }
    var prodName = detail.nome || `Prodotto #${item.itemId}`;
    var variantLabel = detail.taglia ? `Taglia: ${detail.taglia}` : "";
    return `<div class="cart-item-card panel">
      <div class="cart-item-main">
        <div class="cart-item-icon">&#x1f455;</div>
        <div class="cart-item-info">
          <h4>${prodName}</h4>
          ${variantLabel ? '<p class="subtle" style="font-size:0.78rem">'+variantLabel+'</p>' : ''}
          <p class="subtle">${formatCurrency(item.prezzoUnitario)} cad.</p>
          <div class="cart-qty-row">
            <button class="btn-small secondary cart-qty-dec" data-item-id="${item.id}">−</button>
            <span class="cart-qty-val" data-item-id="${item.id}">${item.quantita}</span>
            <button class="btn-small secondary cart-qty-inc" data-item-id="${item.id}">+</button>
          </div>
        </div>
      </div>
      <div class="cart-item-right">
        <strong>${total}</strong>
        <button class="btn-small secondary cart-remove" data-item-id="${item.id}">Rimuovi</button>
      </div>
    </div>`;
  }

  function bindEvents() {
    itemsEl.querySelectorAll(".cart-remove").forEach(btn => {
      btn.addEventListener("click", async () => { btn.disabled = true; try { await window.ApiClient.delete(`/cart/${cartId}/items/${Number(btn.dataset.itemId)}`); await loadCart(); } catch(e) { setStatus(`Errore: ${e.message}`, "error"); btn.disabled = false; } });
    });
    itemsEl.querySelectorAll(".cart-remove-seat").forEach(btn => {
      btn.addEventListener("click", async () => {
        var itemId = Number(btn.dataset.itemId), seat = btn.dataset.seat; btn.disabled = true;
        try {
          var cart = await window.ApiClient.get(`/cart/${cartId}`);
          var item = cart.items.find(i => i.id === itemId);
          if (!item) return;
          var detail = parseDetail(item.dettaglioJson);
          var remaining = (detail.posti || []).filter(s => s !== seat);
          if (remaining.length === 0) { await window.ApiClient.delete(`/cart/${cartId}/items/${itemId}`); }
          else { await window.ApiClient.put(`/cart/${cartId}/items/${itemId}`, { quantita: remaining.length, dettaglioJson: JSON.stringify({ posti: remaining, tipo: detail.tipo }) }); }
          await loadCart();
        } catch(e) { setStatus(`Errore: ${e.message}`, "error"); btn.disabled = false; }
      });
    });
    itemsEl.querySelectorAll(".cart-qty-inc").forEach(btn => {
      btn.addEventListener("click", async () => {
        var itemId = Number(btn.dataset.itemId), valEl = document.querySelector(`.cart-qty-val[data-item-id="${itemId}"]`);
        var qty = Number(valEl?.textContent || 1) + 1;
        try { await window.ApiClient.put(`/cart/${cartId}/items/${itemId}`, { quantita: qty }); await loadCart(); } catch(e) { setStatus(`Errore: ${e.message}`, "error"); }
      });
    });
    itemsEl.querySelectorAll(".cart-qty-dec").forEach(btn => {
      btn.addEventListener("click", async () => {
        var itemId = Number(btn.dataset.itemId), valEl = document.querySelector(`.cart-qty-val[data-item-id="${itemId}"]`);
        var qty = Math.max(1, Number(valEl?.textContent || 2) - 1);
        try { await window.ApiClient.put(`/cart/${cartId}/items/${itemId}`, { quantita: qty }); await loadCart(); } catch(e) { setStatus(`Errore: ${e.message}`, "error"); }
      });
    });
    document.getElementById("cart-giftcard-apply")?.addEventListener("click", async () => {
      var c = document.getElementById("cart-giftcard-input")?.value?.trim().toUpperCase();
      if (!c) { setStatus("Inserisci un codice gift card.", "error"); return; }
      try { await window.ApiClient.post(`/cart/${cartId}/apply-giftcard`, { codice: c }); setStatus("Gift card applicata!", "success"); await loadCart(); } catch(e) { setStatus(`Errore: ${e.message}`, "error"); }
    });
    document.getElementById("cart-coupon-apply")?.addEventListener("click", async () => {
      var c = document.getElementById("cart-coupon-input")?.value?.trim().toUpperCase();
      if (!c) { setStatus("Inserisci un codice offerta.", "error"); return; }
      try { var r = await window.ApiClient.post(`/cart/${cartId}/apply-coupon`, { codice: c }); setStatus(`Offerta applicata! -${formatCurrency(r.sconto)}`, "success"); await loadCart(); } catch(e) { setStatus(`Errore: ${e.message}`, "error"); }
    });
    document.getElementById("cart-checkout")?.addEventListener("click", async function () {
      if (!window.AuthService || !window.AuthService.isAuthenticated()) { window.location.href = `/login.html?callback=${encodeURIComponent("/cart.html")}`; return; }
      if (!cartId) { setStatus("Carrello non caricato.", "error"); return; }
      if (this.disabled) return;
      this.disabled = true;
      clearInterval(cartRefreshTimer);
      cartRefreshTimer = null;
      this.textContent = "Pagamento in corso...";
      setStatus("Reindirizzamento a Stripe...", "info");
      try {
        var r = await window.ApiClient.post("/pagamenti/cart-checkout", { cartId });
        if (r.redirectToStripe === false) { setStatus("Pagamento completato con gift card!", "success"); setTimeout(() => { window.location.href = "/profile.html"; }, 1500); return; }
        if (r && r.url) { window.location.href = r.url; } else { setStatus("Errore: sessione pagamento non disponibile", "error"); this.disabled = false; this.textContent = "Paga con Stripe"; }
      } catch(e) { setStatus(`Errore: ${e.message}`, "error"); this.disabled = false; this.textContent = "Paga con Stripe"; }
    });
  }

  async function initCartPage() { await loadCart(); }
  window.initCartPage = initCartPage;
})();
