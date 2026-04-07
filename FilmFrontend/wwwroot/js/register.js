(function () {
  const form = document.getElementById("register-form");
  const statusEl = document.getElementById("register-status");

  function setStatus(message, kind) {
    statusEl.className = "status " + kind;
    statusEl.textContent = message;
  }

  function isStrongPassword(password) {
    if (password.length < 8) {
      return false;
    }
    const hasUpper = /[A-Z]/.test(password);
    const hasLower = /[a-z]/.test(password);
    const hasNumber = /\d/.test(password);
    const hasSpecial = /[^A-Za-z0-9]/.test(password);
    return hasUpper && hasLower && hasNumber && hasSpecial;
  }

  async function submitForm(event) {
    event.preventDefault();

    const payload = {
      nome: form.nome.value.trim(),
      cognome: form.cognome.value.trim(),
      telefono: form.telefono.value.trim(),
      email: form.email.value.trim(),
      password: form.password.value
    };

    if (!payload.nome || !payload.cognome || !payload.email || !payload.password) {
      setStatus("Compila tutti i campi obbligatori.", "error");
      return;
    }

    if (!isStrongPassword(payload.password)) {
      setStatus("Password debole: usa almeno 8 caratteri con maiuscola, minuscola, numero e simbolo.", "error");
      return;
    }

    if (payload.password !== form.password2.value) {
      setStatus("Le password non coincidono.", "error");
      return;
    }

    setStatus("Registrazione in corso...", "info");
    try {
      await window.AuthService.register(payload);
      setStatus("Registrazione completata. Ora puoi accedere.", "success");
      setTimeout(() => {
        window.location.replace("/login.html");
      }, 600);
    } catch (error) {
      setStatus(`Errore: ${error.message}`, "error");
    }
  }

  async function initRegisterPage() {
    if (!form || !window.AuthGuard) {
      return;
    }
    if (await window.AuthGuard.redirectIfAuthenticated("/profile.html")) {
      return;
    }

    form.addEventListener("submit", submitForm);
  }

  window.initRegisterPage = initRegisterPage;
})();
