# Piano di Lavoro Tecnico — Iterazione 5.1: E-Commerce, Cart, Shop & UX Refinement

## 1. Analisi Stato Attuale

### 1.1 Flusso acquisto corrente
programmazione -> scheda-film -> acquista.html (seat lock 8-10min) -> pagamento.html -> Stripe Checkout -> esito-pagamento.html -> email+PDF

**Problemi architetturali attuali**:
- Acquisto immediato, nessun carrello persistente
- SeatLock legato a `acquista.html` senza integrazione con un carrello
- Una sola proiezione per ordine
- Nessuna possibilità di aggiungere merchandise, gift card, coupon
- Lock scade e il frontend reindirizza forzatamente a `scheda-film.html` -- perdita totale contesto
- Il timer countdown e solo visivo, nessuna notifica proattiva

### 1.2 Toggle distanza cinema (bug attuale)
Nel file `my-cinemas.html`, il range e `min="1" max="200"`. Il valore 200 mostra "200 km", ma dovrebbe rappresentare "Nessuna distanza / Illimitata". Il problema e puramente frontend: il range HTML non ha un valore infinito. Backend: quando raggio non e presente nella query, non applica filtro distanza.

### 1.3 Persistenza filtri cinema
Nessuna persistenza. Quando l'utente cambia pagina e torna indietro, tutto lo stato (citta, tipologia, distanza, ordinamento) viene perso e i cinema appaiono in ordine alfabetico di default.

### 1.4 Stato attuale modelli rilevanti

| Modello | Stato |
|---------|-------|
| Prenotazione | Singola proiezione, max 10 posti, stripe session |
| SeatLock | Legato a (UtenteId, ProiezioneId, PostoCodice), TTL 8-10 min |
| Utente.CreditoPiattaforma | Esistente, usato marginalmente |
| CleanupHostedService | Pulizia ExternalAuthState e token scaduti |
| TicketPdfService | Genera PDF singolo ordine |
| TicketEmailService | Invia email con PDF allegato |


---

## 2. Problemi Architetturali da Risolvere

1. **Manca un layer Cart tra selezione e pagamento** -- il lock e direttamente accoppiato al flusso singolo acquisto
2. **Nessuna astrazione prodotto** -- ticket, merchandise, gift card sono concetti diversi senza interfaccia comune
3. **Lock non rinnovabili** -- dopo 8-10 minuti scade, nessun meccanismo di estensione
4. **Nessuna idempotenza lato Stripe** -- rischio double-charge se webhook + polling esito vanno in race
5. **Stato UI volatile** -- nessuna persistenza filtri/preferenze
6. **Nessun inventory management** -- merchandise e gift card richiedono tracciamento stock
7. **Coupon non esistono** -- nessun motore di regole promozionali

---

## 3. Strategia Architetturale Proposta

### 3.1 Principi guida

- **Refactoring incrementale**: estendere, non riscrivere. Il flusso attuale ticket-only deve continuare a funzionare
- **Cart-first**: introdurre un entita Cart che astrae gruppo di item da acquistare, con CartType per distinguere carrelli ticket-only (backward compat) da carrelli misti
- **Lock come risorsa del carrello**: il SeatLock attuale rimane, ma viene referenziato dal CartItem di tipo ticket. Il carrello eredita la scadenza del lock piu breve
- **Promotion Engine separato**: regole promozionali valutate lato server in un service dedicato

### 3.2 Diagramma flusso target

shop.html ----> [aggiungi merchandise/gift card]
programmazione ----> scheda-film ----> acquista.html ----> [aggiungi ticket al carrello] ----> CART (unificato) ----> applica coupon / verifica scadenze / calcola totale ----> checkout.html ----> Stripe Checkout ----> esito-pagamento.html ----> ticket PDF+email / gift card email / conferma merch

### 3.3 Backward compatibility

Il flusso attuale acquista.html -> pagamento.html -> Stripe rimane invariato. Dietro le quinte, il backend crea un Cart implicito di tipo TicketOnly e lo finalizza immediatamente. Zero regressioni.


---

## 4. Nuove Entita Database

### Cart
| Campo | Tipo |
|-------|------|
| Id | int PK |
| UtenteId | int? FK->Utente (nullable per guest) |
| GuestToken | string(64) (GUID per carrelli anonimi) |
| CartType | string(32): TicketOnly, Mixed, ShopOnly |
| Stato | string(32): Active, Checkout, Converted, Expired, Abandoned |
| Subtotale | decimal(10,2) |
| ScontoCoupon | decimal(10,2) |
| Totale | decimal(10,2) |
| CouponId | int? FK->Coupon |
| RinnoviResidui | int (default 3) |
| ExpiresAtUtc | DateTime |
| CreatedAtUtc / UpdatedAtUtc | DateTime |

### CartItem
| Campo | Tipo |
|-------|------|
| Id | int PK |
| CartId | int FK->Cart |
| ItemType | string(32): Ticket, Merchandise, GiftCard |
| ItemId | int (ProiezioneId / ProductId / GiftCardTemplateId) |
| VariantId | int? (per merch con varianti) |
| Quantita | int |
| PrezzoUnitario | decimal(10,2) (snapshot al momento aggiunta) |
| DettaglioJson | string(2048)? (es. posti selezionati per ticket) |
| CreatedAtUtc | DateTime |

### Product (catalogo merchandise)
Id, Sku(unique), Nome, Descrizione, Categoria(Abbigliamento/Accessori/Food/Gadget), PrezzoBase, ImmaginePath, Attivo, CreatoIl

### ProductVariant
Id, ProductId(FK), Nome(es. Taglia M Nero), Sku(unique), PrezzoExtra, Stock, Attivo

### GiftCardTemplate
Id, Nome(es. Gift Card 20EUR), Importo, ImmaginePath, Attivo

### GiftCard
Id, Codice(unique, formato NFH-GC-XXXX-XXXX), ImportoIniziale, SaldoResiduo, UtenteAcquirenteId(FK), EmailDestinatario?, Messaggio?, Scadenza?(nullable), Stato(Active/Consumed/Expired/Revoked), CreatoIl

### GiftCardTransaction
Id, GiftCardId(FK), CartId?(FK), Tipo(Purchase/Redemption/Refund), Importo, SaldoDopo, CreatoIl

### Coupon (promotion engine)
Id, Codice(unique, formato NFH-PROMO-XXXX), TipoSconto(Percentuale/Fisso/BigliettiGratis), ValoreSconto, ScontoMassimo?, TipoTarget(Film/Cinema/TipologiaSala/FasciaOraria/Carrello/Prodotto), TargetId?, QuantitaMinima, ValidoDal, ValidoAl, MaxUtilizzi(0=illimitato), UtilizziAttuali, MaxPerUtente, MinImportoCarrello?, Stackable(default false), Attivo, CreatoIl

### CouponUsage
Id, CouponId(FK), UtenteId(FK), CartId(FK), ScontoApplicato, CreatoIl. Unique(CouponId, UtenteId, CartId)

### InventoryReservation
Id, ProductVariantId(FK), CartId(FK), Quantita, ExpiresAtUtc, CreatoIl

### NotificationSubscription
Id, UtenteId(FK), Endpoint, P256dh, Auth, UserAgent?, CreatoIl, UltimoInvio?

### Estensioni entita esistenti
- Prenotazione.CartId (int?, FK->Cart)
- SeatLock.CartId (int?, FK->Cart)

### Schema riepilogativo
Utente 1--* Cart, Cart 1--* CartItem, Cart 1--* InventoryReservation, Cart 1--? Coupon,
Cart 1--* Prenotazione(via CartId), Coupon 1--* CouponUsage, Product 1--* ProductVariant,
ProductVariant 1--* InventoryReservation, GiftCard 1--* GiftCardTransaction


---

## 5. Refactor Backend

### 5.1 Nuovi servizi
- **CartService**: CRUD carrello, item, totali, scadenza, merge guest->user, conversione in ordine
- **ProductCatalogService**: CRUD prodotti, varianti, stock, ricerca catalogo
- **GiftCardService**: Acquisto, generazione codice, validazione, redemption, saldo, transazioni
- **PromotionService**: Validazione coupon server-side, calcolo sconto, anti-abuso, regole applicative
- **InventoryService**: Riserva/rilascio stock merchandise, prevenzione oversell
- **PushNotificationService**: Invio notifiche Web Push, fallback email, scheduling
- **CartCleanupHostedService**: Estende CleanupHostedService: scadenza carrelli, rilascio lock+inventory+notifiche

### 5.2 Modifiche a servizi esistenti
- **PagamentiEndpoints**: Supporto carrello misto, Stripe session include tutti i CartItem. Idempotency key basata su CartId.
- **CheckoutEndpoints**: SeatLock ora referenzia CartId opzionale.
- **CleanupHostedService**: Nuovi job: scadenza carrelli -> rilascio lock+inventory+notifiche.
- **TicketEmailService**: Esteso per gift card email e conferma merchandise.

### 5.3 Nuovi gruppi endpoint in Program.cs
- MapCart(/cart): CRUD carrello, item, coupon, checkout (Utente/Guest)
- MapShop(/shop): Catalogo prodotti, gift card template (Pubblica/Admin CRUD)
- MapCoupons(/coupons): Gestione e validazione coupon (Admin/Utente)
- MapGiftCards(/giftcards): Gift card personali, saldo, storico (Utente/Admin)
- MapNotifications(/notifications): Subscribe/unsubscribe push, preferenze (Utente)

---

## 6. Refactor Frontend

### 6.1 Nuove pagine
| Pagina | Descrizione | Script JS |
|--------|-------------|-----------|
| shop.html | Catalogo e-commerce (merch + gift card + offerte) | shop.js |
| checkout.html | Checkout unificato (carrello misto) | checkout.js |
| cart.html | Gestione carrello (vedi/modifica/rimuovi item) | cart.js |
| gift-card-balance.html | Saldo e storico gift card | gift-card.js |

### 6.2 Pagine modificate
| Pagina | Modifica |
|--------|----------|
| acquista.html | Pulsante Aggiungi al carrello (alternativo a Procedi al pagamento) |
| pagamento.html | Mantenuto per backward compat (flusso rapido ticket-only) |
| esito-pagamento.html | Supporto ordine misto (ticket + gift card + merch) |
| my-cinemas.html | Fix toggle distanza, persistenza filtri e ordinamento |
| navbar.html | Aggiunti link Shop e Carrello (con badge conteggio item) |

### 6.3 Fix #1: Toggle distanza cinema
- Range: min=1 max=201 value=201 (201 = Illimitata)
- Label: value==201 -> Nessuna distanza, altrimenti {value} km
- Backend: nessuna modifica, gia gestisce assenza parametro raggio

### 6.4 Fix #2: Persistenza filtri cinema
localStorage strategy: CinemaFilterState con citta, tipologiaSala, raggioKm(null=illimitata), ordinamento, lastGeo. Hydration all avvio da localStorage. Debounced save a 500ms.

### 6.5 Shop page design
3 tab: Gift Card (template 10/20/30/50 EUR + custom), Merchandise (griglia con filtro categoria), Offerte Speciali (coupon attivi)


---

## 7. Nuovi Endpoint API

### 7.1 Cart (/cart/*) - 10 endpoint
POST /cart - Crea o restituisce carrello attivo (Utente/Guest)
GET /cart/{cartId} - Dettaglio carrello con item, totali, scadenze
POST /cart/{cartId}/items - Aggiungi item (ticket/merch/giftcard)
PUT /cart/{cartId}/items/{itemId} - Modifica quantita
DELETE /cart/{cartId}/items/{itemId} - Rimuovi item (rilascia lock/inventory)
POST /cart/{cartId}/extend-locks - Estende TTL lock +8 min (max 3 rinnovi)
POST /cart/{cartId}/apply-coupon - Applica codice coupon
DELETE /cart/{cartId}/coupon - Rimuovi coupon
POST /cart/{cartId}/checkout - Crea Stripe session per carrello misto
POST /cart/merge - Merge carrello guest -> utente dopo login

### 7.2 Shop (/shop/*) - 6 endpoint
GET /shop/products (Pubblica) - Catalogo prodotti con varianti
GET /shop/products/{id} (Pubblica) - Dettaglio prodotto
GET /shop/giftcard-templates (Pubblica) - Template gift card
POST/PUT/DELETE /shop/products/{id} (Admin) - CRUD prodotti

### 7.3 Coupon (/coupons/*) - 6 endpoint
POST /coupons/validate (Utente) - Valida codice coupon (input: codice, cartId)
GET /coupons (Admin) - Lista coupon
POST /coupons (Admin) - Crea coupon
PUT /coupons/{id} (Admin) - Modifica coupon
DELETE /coupons/{id} (Admin) - Disattiva coupon
GET /coupons/{id}/usage (Admin) - Storico utilizzi

### 7.4 Gift Cards (/giftcards/*) - 6 endpoint
GET /giftcards/mine (Utente) - Mie gift card
GET /giftcards/{codice}/balance (Pubblica) - Verifica saldo
POST /giftcards/{codice}/redeem (Utente) - Riscatta -> CreditoPiattaforma
GET /giftcards/mine/transactions (Utente) - Storico transazioni
POST /giftcards/admin/generate (Admin) - Genera gift card
GET /giftcards/admin (Admin) - Lista gift card

### 7.5 Notifiche (/notifications/*) - 4 endpoint
POST /notifications/subscribe (Utente)
DELETE /notifications/unsubscribe (Utente)
GET /notifications/preferences (Utente)
PUT /notifications/preferences (Utente)

---

## 8. Sistema Carrello - Dettaglio

### 8.1 Creazione carrello
POST /cart -> cerca Active per UtenteId/guestToken. Se trovato restituisce. Se no crea nuovo Cart con ExpiresAtUtc=+30min. Guest token = crypto.randomUUID(), salvato in sessionStorage.

### 8.2 Aggiunta ticket e gestione lock
1. Verifica lock attivi per quei posti/proiezione
2. Se non ha lock: POST /checkout/locks
3. SeatLock.CartId = cart.Id
4. Cart.ExpiresAtUtc = min(tutti i lock)
5. Snapshot prezzo in PrezzoUnitario

### 8.3 Rinnovo lock
POST /cart/{id}/extend-locks -> controlla Active e RinnoviResidui>0 -> estende tutti i lock +8min -> RinnoviResidui-- -> restituisce newExpiresAtUtc

### 8.4 Scadenza carrello (CartCleanupHostedService, ogni 60s)
1. Trova carrelli Active con ExpiresAtUtc < UtcNow
2. Stato = Expired
3. Cancella SeatLock associati
4. Cancella InventoryReservation
5. Invia push (fallback email) Il tuo carrello e scaduto
6. Dopo 2h: email carrello abbandonato

### 8.5 Merge guest -> authenticated
POST /cart/merge { guestToken } -> cerca guest cart -> se utente ha cart: unisce item (deduplica ticket, somma merch), elimina guest -> se non ha cart: associa all utente -> ricalcola totali

### 8.6 Gestione errori
- Aggiunta item fallita dopo lock creato: cleanup job rilascia lock orfano
- Checkout fallito dopo Stripe session: session scade 24h, carrello resta Checkout
- Webhook non ricevuto: polling GET /pagamenti/esito recupera

---

## 9. Sistema Promozioni (Coupon Engine)

### 9.1 Validazione server-side (10 step)
1. Cerca Coupon per Codice (case-insensitive)
2. Attivo == true
3. ValidoDal <= UtcNow <= ValidoAl
4. UtilizziAttuali < MaxUtilizzi (0=illimitato)
5. CouponUsage per (CouponId,UtenteId) < MaxPerUtente
6. Se Cart.CouponId esiste e non stackable -> rifiuta
7. Cart.Subtotale >= MinImportoCarrello
8. Almeno un CartItem con Quantita >= QuantitaMinima
9. Target: se Film/Cinema verifica presenza ticket per TargetId
10. Calcolo sconto: Percentuale = subtotale * (valore/100) con cap ScontoMassimo; Fisso = min(valore, subtotale)

### 9.2 Anti-abuso
- Validazione SOLO server-side (frontend mostra Sconto stimato: ~X EUR)
- Idempotenza: riapplicare stesso coupon = no-op
- SELECT FOR UPDATE su Coupon per UtilizziAttuali atomico
- Anti-enumerazione: messaggio generico sempre uguale
- Logging ogni tentativo (valido e non)
- Rate limiting: 5 tentativi/min su /coupons/validate


---

## 10. Sistema Gift Card

### Acquisto
1. Seleziona template (10/20/30/50 EUR) o importo custom
2. Opzionale: email destinatario (regalo), messaggio
3. Aggiunge al carrello come CartItem(ItemType=GiftCard)
4. Dopo pagamento: GiftCard con codice NFH-GC-XXXXXXXX-XXXX, Stato=Active, SaldoResiduo=ImportoIniziale
5. Email con codice (a destinatario se specificato)

### Redenzione
POST /giftcards/{codice}/redeem -> verifica Active e non scaduta -> ImportoRiscatto = min(importo ?? SaldoResiduo, SaldoResiduo) -> transazione: GiftCard.SaldoResiduo -= ImportoRiscatto, Utente.CreditoPiattaforma += ImportoRiscatto, GiftCardTransaction

---

## 11. Sistema Notifiche Push

### Architettura
Frontend(Service Worker) <-- Push Server(browser) <-- PushNotificationService(backend) <-- Scheduler(timer scadenza) <-- Email fallback

### Service Worker (sw.js)
push event: mostra notifica con titolo, corpo, icona, badge, data URL
notificationclick: focus/apre finestra con URL specificato

### Strategia notifiche
| Evento | Canale | Messaggio |
|--------|--------|-----------|
| Lock in scadenza (1 min) | Push + Toast | I tuoi posti si libereranno tra 1 minuto! |
| Lock scaduto | Push + Email | Posti rilasciati. Il carrello e scaduto. |
| Carrello abbandonato (2h) | Email | Hai dimenticato qualcosa nel carrello? |
| Gift card acquistata | Email | Codice: NFH-GC-XXXX |
| Ordine completato | Push + Email | Acquisto confermato! |

### Opt-in e GDPR
Double opt-in (popup app + prompt browser), preferenze granulari, unsubscribe immediato, VAPID keys in .env

---

## 12. Strategia Lock e Timeout

### Evoluzione SeatLock
Aggiungere CartId (int?, FK->Cart) per rilascio lock in blocco quando carrello scade.

### Rinnovo
Max 3 rinnovi per carrello (RinnoviResidui). Ogni rinnovo +8min su tutti i lock. Carrello eredita nuova scadenza.

### Recovery
Riapertura browser: acquista.html/cart.html recuperano lock via API. Tab chiuso: lock attivi server-side. Network loss: Stripe+webhook gestiscono.

### Cleanup job (unico CartCleanupHostedService)
| Job | Freq | Azione |
|-----|------|--------|
| Expired carts | 60s | Rilascia lock+inventory, setta Expired |
| Orphan locks | 120s | Lock scaduti senza CartId |
| Expired reservations | 120s | InventoryReservation scadute |
| Expired auth states | 300s | (esistente) |
| Abandoned cart email | 7200s | Email carrello >2h |

---

## 13. Sicurezza

| Minaccia | Mitigazione |
|----------|-------------|
| Frode coupon - uso multiplo | CouponUsage unique index, SELECT FOR UPDATE atomico |
| Frode coupon - guessing | Codici 12+ char random, rate limiting 5/min |
| Lock bypass | Validazione server-side in checkout |
| Race condition doppio checkout | Stripe idempotency key basata su CartId |
| Inventory oversell | SELECT FOR UPDATE su ProductVariant, reservation TTL |
| Gift card guessing | Codici 16 char (36^16 spazio), rate limiting 10/min |
| Double checkout stesso carrello | Cart.Stato transizione atomica Active->Checkout, 409 |
| Coupon information leak | Messaggio generico sempre uguale |

### Idempotenza Stripe
SessionCreateOptions { IdempotencyKey = cart_{cartId}_checkout_v1 }

### Transazioni database
1. Finalizzazione ordine: Prenotazioni + GiftCard + aggiornamento stock + rimozione lock (unica tx)
2. Applicazione coupon: incremento utilizzi + CouponUsage (unica tx)
3. Redenzione gift card: decremento saldo + incremento CreditoPiattaforma (unica tx)

---

## 14. Performance e Scalabilita

### Indici database
IX_Cart_UtenteId_Stato, IX_Cart_ExpiresAtUtc (filtered Stato=Active)
IX_SeatLock_ExpiresAtUtc, IX_SeatLock_CartId
IX_InventoryReservation_ExpiresAtUtc
UNIQUE IX_Coupon_Codice, UNIQUE IX_CouponUsage(CouponId,UtenteId,CartId), UNIQUE IX_GiftCard_Codice

### Caching
Catalogo prodotti: IMemoryCache 5min. Gift card template: 10min. Dati utente per OnTokenValidated: 30s.

### Scalabilita orizzontale
SeatLock e InventoryReservation nel DB = multi-istanza nativo. Rate limiting: in-memory(dev) / Redis(prod). Webhook Stripe: coda condivisa.


---

## 15. Piano Implementativo Step-by-Step

### Sprint 1 -- Fix UX + Fondazioni DB (Giorni 1-3)
1. [ ] Fix toggle distanza cinema (range 201, label Nessuna distanza)
2. [ ] Persistenza filtri cinema (localStorage strategy)
3. [ ] Creare nuove entita (Cart, CartItem, Product, ProductVariant, GiftCardTemplate, GiftCard, GiftCardTransaction, Coupon, CouponUsage, InventoryReservation, NotificationSubscription)
4. [ ] Aggiornare FilmDbContext.cs con DbSet, Fluent API, indici, vincoli
5. [ ] Aggiungere CartId a SeatLock e Prenotazione
6. [ ] Creare migrazione EF Core: Iteration5_1_Ecommerce
7. [ ] Aggiornare .env.example con nuove variabili

### Sprint 2 -- Cart Service (Giorni 4-6)
8. [ ] Implementare CartService: CRUD carrello, item, totali, scadenza
9. [ ] Implementare endpoint /cart/* (10 endpoint)
10. [ ] Modificare CheckoutEndpoints: SeatLock con CartId
11. [ ] Implementare merge guest->user: POST /cart/merge
12. [ ] Implementare CartCleanupHostedService
13. [ ] Unit test CartService

### Sprint 3 -- Product Catalog + Gift Card (Giorni 7-9)
14. [ ] Implementare ProductCatalogService e InventoryService
15. [ ] Implementare endpoint /shop/* (6 endpoint)
16. [ ] Implementare GiftCardService
17. [ ] Implementare endpoint /giftcards/* (6 endpoint)
18. [ ] Creare shop.html con tab merch + gift card
19. [ ] Creare js/shop.js
20. [ ] Unit test GiftCardService

### Sprint 4 -- Coupon Engine (Giorni 10-12)
21. [ ] Implementare PromotionService: validazione, calcolo sconto, regole
22. [ ] Implementare endpoint /coupons/* (6 endpoint)
23. [ ] Integrare coupon in POST /cart/{id}/apply-coupon
24. [ ] Validazione server-side anti-abuso
25. [ ] Aggiungere tab Offerte in shop.html
26. [ ] Unit test PromotionService

### Sprint 5 -- Refactor Checkout (Giorni 13-15)
27. [ ] Modificare PagamentiEndpoints: supporto carrello misto, idempotency key
28. [ ] Creare checkout.html e checkout.js
29. [ ] Creare cart.html e cart.js
30. [ ] Modificare acquista.html: pulsante Aggiungi al carrello
31. [ ] Modificare esito-pagamento.html: ordine misto
32. [ ] Integration test: flusso carrello misto end-to-end

### Sprint 6 -- Notifiche Push (Giorni 16-17)
33. [ ] Generare VAPID keys, aggiungere a .env
34. [ ] Creare PushNotificationService
35. [ ] Implementare endpoint /notifications/* (4 endpoint)
36. [ ] Creare sw.js (Service Worker)
37. [ ] Creare js/notifications.js: sottoscrizione, opt-in
38. [ ] Integrare notifiche in CartCleanupHostedService
39. [ ] Fallback toast per browser senza Web Push
40. [ ] Fallback email per carrelli scaduti

### Sprint 7 -- Frontend Completo (Giorni 18-20)
41. [ ] Aggiornare navbar.html: link Shop, Carrello (badge conteggio)
42. [ ] cart.js completamento: modifica item, rinnovo lock, coupon, checkout
43. [ ] checkout.js: riepilogo, metodo pagamento, Stripe redirect
44. [ ] shop.js completamento: filtri, ricerca, aggiunta carrello
45. [ ] Aggiornare api-client.js: supporto guest token per carrello anonimo
46. [ ] Aggiornare styles.css: nuovi componenti (shop grid, cart, checkout, badge)

### Sprint 8 -- Testing, QA, Docs (Giorni 21-23)
47. [ ] Unit test: tutti i nuovi servizi
48. [ ] Integration test: endpoint carrello, shop, coupon, gift card
49. [ ] Integration test: checkout carrello misto
50. [ ] Integration test: race condition (lock, coupon, inventory)
51. [ ] Integration test: scadenza carrello e rilascio automatico
52. [ ] E2E test: flusso completo shop->cart->coupon->checkout->Stripe->esito
53. [ ] E2E test: backward compat (vecchio flusso ticket-only intatto)
54. [ ] Aggiornare documentazione

---

## 16. Priorita Sviluppo

| Priorita | Componente | Motivazione |
|----------|-----------|-------------|
| P0 | Fix toggle distanza + persistenza filtri | Bug in produzione |
| P0 | Modello dati + migrazione | Fondamenta |
| P0 | Cart Service + endpoint carrello | Abilita flusso e-commerce |
| P1 | Product Catalog + Gift Card | Shop funzionante |
| P1 | Refactor checkout (carrello misto) | Core business value |
| P2 | Coupon Engine | Complessita alta, differenziabile |
| P2 | Notifiche Push | Nice-to-have |
| P3 | Admin CRUD prodotti/coupon | Solo se necessario |

---

## 17. Rischi Tecnici

| Rischio | Prob | Impatto | Mitigazione |
|---------|------|---------|-------------|
| Regressione flusso ticket attuale | Media | Alto | Backward compat (Cart implicito), E2E test |
| Race condition doppio acquisto | Media | Alto | Idempotency key Stripe, unique constraint, tx atomica |
| Inventory oversell | Alta | Medio | SELECT FOR UPDATE, reservation TTL breve |
| Coupon indovinabili | Bassa | Medio | Codici 12+ char random, rate limiting |
| Push not supportate | Alta | Basso | Fallback toast + email |
| Complessita carrello eccessiva | Media | Medio | Feature flag FEATURE_CART_ENABLED |
| Nuove tabelle DB | Bassa | Alto | Additive, colonne nullable su tabelle esistenti |

---

## 18. Stima Complessita

| Componente | Complessita | Giorni |
|------------|-------------|--------|
| Fix UX (toggle + persistenza) | Bassa | 0.5 |
| Modello dati + migrazione | Media | 1.5 |
| Cart Service + endpoint | Alta | 3 |
| Product Catalog + Shop | Media | 2 |
| Gift Card Service | Media | 2 |
| Coupon Engine | Alta | 3 |
| Refactor Checkout | Alta | 3 |
| Notifiche Push | Media | 2 |
| Frontend (shop, cart, checkout) | Alta | 4 |
| Testing | Media | 3 |
| Totale | | 24 giorni (~5 settimane) |

---

## 19. Migrazione Produzione

### Feature flags
FEATURE_CART_ENABLED, FEATURE_SHOP_ENABLED, FEATURE_COUPONS_ENABLED, FEATURE_GIFTCARDS_ENABLED, FEATURE_PUSH_NOTIFICATIONS

### Rollout a fasi
1. Fase 1 (g8): Deploy migrazione DB + Cart Service. FEATURE_CART_ENABLED=false. Nessun impatto.
2. Fase 2 (g15): Attivare carrello. Vecchio flusso crea Cart implicito.
3. Fase 3 (g18): Attivare Shop + Gift Card.
4. Fase 4 (g23): Attivare Coupon e Push.

### Rollback
Disabilitare feature flags -> codice nuovo non eseguito. Tabelle DB rimangono (non bloccano). Flusso ticket-only funziona senza carrello. Nessuna modifica breaking a tabelle esistenti.

---

## 20. Checklist QA/Testing

### Fix UX
- [ ] Toggle mostra Nessuna distanza al valore massimo
- [ ] Slider raggiunge il bordo destro
- [ ] Filtri persistono dopo cambio pagina e ritorno
- [ ] Ordinamento distanza con e senza GPS
- [ ] GPS negato -> fallback alfabetico

### Cart
- [ ] Creazione carrello (utente e guest)
- [ ] Aggiunta ticket (crea lock automaticamente)
- [ ] Aggiunta merchandise (riserva inventory)
- [ ] Aggiunta gift card
- [ ] Rimozione item (rilascia lock/inventory)
- [ ] Rinnovo lock (max 3)
- [ ] Scadenza carrello (rilascio automatico + notifica)
- [ ] Merge guest -> utente dopo login

### Shop
- [ ] Catalogo prodotti visibile con varianti
- [ ] Gift card template visibili
- [ ] Aggiunta al carrello funzionante
- [ ] Importo custom gift card validato

### Coupon
- [ ] Coupon valido applicato
- [ ] Coupon scaduto/esaurito/doppio uso -> rifiutato
- [ ] Target specifico (film/cinema) validato
- [ ] Sconto calcolato correttamente
- [ ] Anti-enumerazione (messaggio generico)

### Checkout carrello misto
- [ ] Stripe session include tutti gli item
- [ ] Pagamento completato -> biglietti + gift card + merch generati
- [ ] Idempotenza (doppio click non crea doppio ordine)
- [ ] Email include tutti i tipi di item
- [ ] Backward compat: vecchio flusso intatto

### Gift Card
- [ ] Acquisto -> codice generato ed email inviata
- [ ] Redenzione -> credito aggiunto a piattaforma
- [ ] Utilizzo parziale -> saldo aggiornato
- [ ] Codice inesistente/scaduto -> errore generico

### Notifiche
- [ ] Sottoscrizione push funzionante
- [ ] Notifica 1 minuto prima scadenza
- [ ] Fallback toast se browser non supporta push
- [ ] Unsubscribe funzionante

### Performance
- [ ] Carrello con 10+ item risponde in <200ms
- [ ] Cleanup job non blocca richieste API
- [ ] Indici database utilizzati (EXPLAIN query)

---

*Piano redatto il 2026-05-12. Copre tutti i requisiti dell Iterazione 5.1.*
