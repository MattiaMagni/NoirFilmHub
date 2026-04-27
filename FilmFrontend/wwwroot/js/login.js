(function () {
  const form = document.getElementById("login-form");
  const statusEl = document.getElementById("login-status");

  function setStatus(message, kind) {
    statusEl.className = "status " + kind;
    statusEl.textContent = message;
  }

  async function submitForm(event) {
    event.preventDefault();
    const email = form.email.value.trim();
    const password = form.password.value;

    if (!email || !password) {
      setStatus("Inserisci email e password.", "error");
      return;
    }

    setStatus("Accesso in corso...", "info");
    try {
      await window.AuthService.login(email, password);
      setStatus("Login effettuato.", "success");
      const callback = window.AuthService.getCallbackFromLocation ? window.AuthService.getCallbackFromLocation() : null;
      const saved = callback || window.AuthService.consumeRedirect();
      window.location.replace(saved || "/index.html");
    } catch (error) {
      setStatus(error && error.status === 401 ? "Credenziali non valide." : `Errore: ${error.message}`, "error");
    }
  }

  async function initLoginPage() {
    if (!form || !window.AuthGuard || !window.AuthService) {
      return;
    }

    if (await window.AuthGuard.redirectIfAuthenticated("/index.html")) {
      return;
    }

    form.addEventListener("submit", submitForm);
  }

  window.initLoginPage = initLoginPage;
})();
