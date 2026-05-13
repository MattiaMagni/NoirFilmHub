(function () {
  const statusEl = document.getElementById("shop-status");
  const tabs = document.querySelectorAll(".shop-tab");
  const tabContents = document.querySelectorAll(".shop-tab-content");

  function setStatus(msg, kind) {
    if (statusEl) { statusEl.className = `status ${kind}`; statusEl.textContent = msg; }
  }

  function formatCurrency(v) {
    return new Intl.NumberFormat("it-IT", { style: "currency", currency: "EUR" }).format(Number(v || 0));
  }

  function isAdmin() {
    try {
      const user = JSON.parse(localStorage.getItem("user") || "{}");
      return user.ruolo === "admin";
    } catch { return false; }
  }

  async function getOrCreateCart() {
    const headers = {};
    const guestToken = sessionStorage.getItem("cart_guest_token");
    if (guestToken && !(window.AuthService && window.AuthService.isAuthenticated())) {
      headers["X-Guest-Token"] = guestToken;
    }
    const cart = await window.ApiClient.post("/cart", null, headers);
    if (cart && cart.guestToken && !cart.utenteId) {
      sessionStorage.setItem("cart_guest_token", cart.guestToken);
    }
    return cart;
  }

  async function addToCart(itemType, itemId, variantId, qty, price, detailJson) {
    const cart = await getOrCreateCart();
    await window.ApiClient.post(`/cart/${cart.id}/items`, {
      itemType, itemId, variantId, quantita: qty, prezzoUnitario: price, dettaglioJson: detailJson
    });
    updateCartBadge();
    showCartToast();
  }

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

  function updateCartBadge() {
    const badge = document.getElementById("cart-badge");
    if (badge) { badge.style.display = "inline-block"; }
  }

  async function loadGiftCards() {
    const grid = document.getElementById("giftcard-grid");
    if (!grid) return;
    try {
      const templates = await window.ApiClient.get("/shop/giftcard-templates");
      if (!templates.length) {
        grid.innerHTML = "<p class='subtle'>Nessuna gift card disponibile.</p>";
        return;
      }
      grid.innerHTML = templates.map(t => `
        <article class="card giftcard-card">
          <div class="card-body" style="text-align:center">
            <div class="giftcard-amount">${formatCurrency(t.importo)}</div>
            <h3>${t.nome}</h3>
            <div class="filter-row" style="justify-content:center;margin-top:0.5rem">
              <input type="email" class="giftcard-email" placeholder="Email destinatario (regalo)" style="width:180px;font-size:0.85rem">
            </div>
            <button class="button primary add-giftcard" data-id="${t.id}" data-amount="${t.importo}" style="margin-top:0.5rem">Aggiungi al carrello</button>
          </div>
        </article>
      `).join("");

      grid.querySelectorAll(".add-giftcard").forEach(btn => {
        btn.addEventListener("click", async () => {
          const id = Number(btn.dataset.id);
          const amount = Number(btn.dataset.amount);
          const emailEl = btn.parentElement.querySelector(".giftcard-email");
          const email = emailEl?.value?.trim() || "";
          btn.disabled = true;
          btn.textContent = "Aggiunta...";
          try {
            await addToCart("GiftCard", id, null, 1, amount, JSON.stringify({ emailDestinatario: email }));
            setStatus("Gift card aggiunta al carrello!", "success");
          } catch (e) {
            setStatus(`Errore: ${e.message}`, "error");
          }
          btn.disabled = false;
          btn.textContent = "Aggiungi al carrello";
        });
      });
    } catch (e) {
      grid.innerHTML = "<p class='subtle'>Errore caricamento gift card.</p>";
    }
  }

  async function loadMerch(category) {
    const grid = document.getElementById("merch-grid");
    if (!grid) return;
    try {
      const products = await window.ApiClient.get("/shop/products");
      const filtered = category ? products.filter(p => p.categoria === category) : products;
      if (!filtered.length) {
        grid.innerHTML = "<p class='subtle'>Nessun prodotto disponibile.</p>";
        return;
      }
      grid.innerHTML = filtered.map(p => {
        const variants = (p.varianti || []).map(v =>
          `<option value="${v.id}" data-price="${v.prezzoFinale}">${v.nome} - ${formatCurrency(v.prezzoFinale)} (Stock: ${v.stock})</option>`
        ).join("");
        const hasVariants = variants.length > 0;
        return `
          <article class="card">
            <div class="card-body">
              <h3>${p.nome}</h3>
              <p class="subtle">${p.descrizione?.slice(0, 120) || ""}</p>
              <p>${hasVariants ? `Da ${formatCurrency(p.prezzoBase)}` : formatCurrency(p.prezzoBase)}</p>
              ${hasVariants ? `<select class="merch-variant" data-product="${p.id}">${variants}</select>` : ""}
              <button class="button primary add-merch" data-product="${p.id}" data-price="${p.prezzoBase}" data-has-variants="${hasVariants}">Aggiungi al carrello</button>
            </div>
          </article>
        `;
      }).join("");

      grid.querySelectorAll(".add-merch").forEach(btn => {
        btn.addEventListener("click", async () => {
          const productId = Number(btn.dataset.product);
          let price = Number(btn.dataset.price);
          let variantId = null;
          if (btn.dataset.hasVariants === "true") {
            const sel = btn.parentElement.querySelector(".merch-variant");
            if (sel) {
              variantId = Number(sel.value);
              price = Number(sel.selectedOptions[0].dataset.price);
            }
          }
          btn.disabled = true;
          btn.textContent = "Aggiunta...";
          try {
            await addToCart("Merchandise", productId, variantId, 1, price, null);
            setStatus("Prodotto aggiunto al carrello!", "success");
          } catch (e) {
            setStatus(`Errore: ${e.message}`, "error");
          }
          btn.disabled = false;
          btn.textContent = "Aggiungi al carrello";
        });
      });
    } catch (e) {
      grid.innerHTML = "<p class='subtle'>Errore caricamento prodotti.</p>";
    }
  }

  async function loadCinemasForFilter() {
    try {
      const cinemas = await window.ApiClient.get("/cinemas");
      const sel = document.getElementById("offers-cinema-filter");
      if (sel) {
        sel.innerHTML = '<option value="">Tutti i cinema</option>' +
          cinemas.map(c => `<option value="${c.id}">${c.nome} - ${c.citta}</option>`).join("");
      }
      const adminSel = document.getElementById("admin-coupon-cinema");
      if (adminSel) {
        adminSel.innerHTML = '<option value="">Seleziona cinema</option>' +
          cinemas.map(c => `<option value="${c.id}">${c.nome} - ${c.citta}</option>`).join("");
      }
    } catch {}
  }

  async function loadOffers(cinemaId) {
    const list = document.getElementById("offers-list");
    if (!list) return;
    try {
      const coupons = await window.ApiClient.get("/coupons");
      let activeCoupons = coupons.filter(c => c.attivo);
      if (cinemaId) {
        const cid = Number(cinemaId);
        activeCoupons = activeCoupons.filter(c =>
          c.tipoTarget === "Carrello" ||
          (c.tipoTarget === "Cinema" && c.targetId === cid)
        );
      }
      if (!activeCoupons.length) {
        list.innerHTML = "<p class='subtle'>Nessuna offerta attiva al momento.</p>";
        return;
      }
      list.innerHTML = activeCoupons.map(c => `
        <article class="card">
          <div class="card-body">
            <h3>${c.codice}</h3>
            <p class="subtle">${c.tipoSconto === "Percentuale" ? `${c.valoreSconto}% di sconto` : `${formatCurrency(c.valoreSconto)} di sconto`}${c.tipoTarget === "Cinema" ? " (cinema specifico)" : ""}</p>
            <p class="subtle">Valido fino al ${new Date(c.validoAl).toLocaleDateString("it-IT")}</p>
            <p class="subtle">Usa questo codice in fase di checkout.</p>
          </div>
        </article>
      `).join("");
    } catch (e) {
      list.innerHTML = "<p class='subtle'>Errore caricamento offerte.</p>";
    }
  }

  // Gift card custom add
  document.getElementById("giftcard-custom-add")?.addEventListener("click", async () => {
    const amountEl = document.getElementById("giftcard-custom-amount");
    const emailEl = document.getElementById("giftcard-recipient-email");
    const msgEl = document.getElementById("giftcard-message");
    const amount = Number(amountEl?.value || 0);
    if (amount < 5 || amount > 500) {
      setStatus("Inserisci un importo tra 5 e 500 EUR", "error");
      return;
    }
    try {
      await addToCart("GiftCard", 0, null, 1, amount, JSON.stringify({
        custom: true, amount,
        emailDestinatario: emailEl?.value?.trim() || "",
        messaggio: msgEl?.value?.trim() || ""
      }));
      setStatus("Gift card personalizzata aggiunta al carrello!", "success");
    } catch (e) {
      setStatus(`Errore: ${e.message}`, "error");
    }
  });

  document.getElementById("merch-category-filter")?.addEventListener("change", (e) => loadMerch(e.target.value));

  document.getElementById("offers-cinema-filter")?.addEventListener("change", (e) => loadOffers(e.target.value));

  // Admin: product add
  document.getElementById("admin-prod-add")?.addEventListener("click", async () => {
    const nome = document.getElementById("admin-prod-nome")?.value?.trim();
    const sku = document.getElementById("admin-prod-sku")?.value?.trim();
    if (!nome || !sku) { setStatus("Nome e SKU obbligatori", "error"); return; }
    const btn = document.getElementById("admin-prod-add");
    btn.disabled = true; btn.textContent = "Creazione...";
    try {
      let immaginePath = document.getElementById("admin-prod-immagine")?.value?.trim() || "";
      const fileInput = document.getElementById("admin-prod-upload");
      if (fileInput?.files?.length > 0) {
        const formData = new FormData();
        formData.append("file", fileInput.files[0]);
        try {
          const token = window.AuthService ? await window.AuthService.ensureValidAccessToken() : null;
          const resp = await fetch(`${window.AppConfig.API_BASE_URL}/shop/upload-image`, {
            method: "POST", headers: token ? { Authorization: `Bearer ${token}` } : {}, body: formData
          });
          if (resp.ok) {
            const result = await resp.json();
            immaginePath = result.path || immaginePath;
          }
        } catch {}
      }
      await window.ApiClient.post("/shop/products", {
        sku, nome,
        descrizione: document.getElementById("admin-prod-desc")?.value || "",
        categoria: document.getElementById("admin-prod-categoria")?.value || "Gadget",
        prezzoBase: Number(document.getElementById("admin-prod-prezzo")?.value || 9.99),
        immaginePath, attivo: true
      });
      setStatus("Prodotto creato!", "success");
      document.getElementById("admin-prod-nome").value = "";
      document.getElementById("admin-prod-sku").value = "";
      document.getElementById("admin-prod-desc").value = "";
      document.getElementById("admin-prod-immagine").value = "";
      if (fileInput) fileInput.value = "";
    } catch (e) {
      setStatus(`Errore: ${e.message}`, "error");
    }
    btn.disabled = false; btn.textContent = "Crea prodotto";
  });

  // Admin: coupon add
  document.getElementById("admin-coupon-target")?.addEventListener("change", function() {
    const cinemaSel = document.getElementById("admin-coupon-cinema");
    const targetId = document.getElementById("admin-coupon-targetid");
    if (this.value === "Cinema") {
      cinemaSel.classList.remove("hidden");
      targetId.classList.add("hidden");
    } else if (this.value === "Film") {
      cinemaSel.classList.add("hidden");
      targetId.classList.remove("hidden");
    } else {
      cinemaSel.classList.add("hidden");
      targetId.classList.add("hidden");
    }
  });

  document.getElementById("admin-coupon-cinema")?.addEventListener("change", function() {
    document.getElementById("admin-coupon-targetid").value = this.value;
  });

  document.getElementById("admin-coupon-add")?.addEventListener("click", async () => {
    const codice = document.getElementById("admin-coupon-codice")?.value?.trim();
    if (!codice) { setStatus("Codice obbligatorio", "error"); return; }
    const tipoTarget = document.getElementById("admin-coupon-target")?.value;
    let targetId = null;
    if (tipoTarget === "Cinema") targetId = Number(document.getElementById("admin-coupon-cinema")?.value || 0);
    if (tipoTarget === "Film") targetId = Number(document.getElementById("admin-coupon-targetid")?.value || 0);
    if (targetId === 0) targetId = null;

    try {
      await window.ApiClient.post("/coupons", {
        codice, tipoSconto: document.getElementById("admin-coupon-tipo")?.value || "Fisso",
        valoreSconto: Number(document.getElementById("admin-coupon-valore")?.value || 10),
        tipoTarget: tipoTarget || "Carrello", targetId, quantitaMinima: 1,
        validoDal: document.getElementById("admin-coupon-dal")?.value || new Date().toISOString().slice(0, 10),
        validoAl: document.getElementById("admin-coupon-al")?.value || new Date(Date.now() + 30*86400000).toISOString().slice(0, 10),
        maxUtilizzi: 0, maxPerUtente: 1, stackable: false, attivo: true
      });
      setStatus("Offerta creata!", "success");
    } catch (e) {
      setStatus(`Errore: ${e.message}`, "error");
    }
  });

  tabs.forEach(tab => {
    tab.addEventListener("click", () => {
      tabs.forEach(t => t.classList.remove("active"));
      tab.classList.add("active");
      tabContents.forEach(tc => tc.classList.add("hidden"));
      const target = document.getElementById(`shop-tab-${tab.dataset.tab}`);
      if (target) target.classList.remove("hidden");
      if (tab.dataset.tab === "giftcards") loadGiftCards();
      if (tab.dataset.tab === "merch") loadMerch("");
      if (tab.dataset.tab === "offers") loadOffers("");
    });
  });

  async function initShopPage() {
    setStatus("Caricamento shop...", "info");
    try {
      if (isAdmin()) {
        document.getElementById("shop-admin-section")?.classList.remove("hidden");
      }
      await loadCinemasForFilter();
      await Promise.all([loadGiftCards(), loadMerch(""), loadOffers("")]);
      setStatus("Shop pronto.", "success");
    } catch (e) {
      setStatus(`Errore: ${e.message}`, "error");
    }
  }

  window.initShopPage = initShopPage;
})();
