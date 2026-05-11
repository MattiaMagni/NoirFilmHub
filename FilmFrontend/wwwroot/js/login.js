(function () {
  function setStatus(statusEl, message, kind) {
    if (!statusEl) return;
    statusEl.className = "status " + kind;
    statusEl.textContent = message;
  }

  async function handleSubmit(event) {
    event.preventDefault();
    var form = document.getElementById("login-form");
    var statusEl = document.getElementById("login-status");
    var email = form.email.value.trim();
    var password = form.password.value;
    if (!email || !password) { setStatus(statusEl, "Inserisci email e password.", "error"); return; }
    setStatus(statusEl, "Accesso in corso...", "info");
    try {
      await window.AuthService.login(email, password);
      setStatus(statusEl, "Login effettuato.", "success");
      var callback = window.AuthService.getCallbackFromLocation ? window.AuthService.getCallbackFromLocation() : null;
      var saved = callback || window.AuthService.consumeRedirect();
      window.location.replace(saved || "/index.html");
    } catch (error) {
      setStatus(statusEl, error && error.status === 401 ? "Credenziali non valide." : "Errore: " + (error.message || "Errore sconosciuto"), "error");
    }
  }

  async function initLoginPage() {
    var form = document.getElementById("login-form");
    if (!form) return;
    form.addEventListener("submit", handleSubmit);
  }

  window.initLoginPage = initLoginPage;
})();