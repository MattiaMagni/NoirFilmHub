async function loadComponent(targetId, componentPath) {
  const target = document.getElementById(targetId);
  if (!target) {
    return;
  }

  try {
    const response = await fetch(componentPath);
    if (!response.ok) {
      throw new Error("Errore caricamento componente");
    }

    target.innerHTML = await response.text();
  } catch {
    target.innerHTML = "<div class=\"status error\">Impossibile caricare il layout comune.</div>";
  }
}

async function bootstrapLayout() {
  await loadComponent("navbar-container", "/components/navbar.html");
  await loadComponent("footer-container", "/components/footer.html");

  if (window.AuthService && typeof window.AuthService.ensureValidAccessToken === "function") {
    try {
      await window.AuthService.ensureValidAccessToken();
    } catch {
    }
  }

  if (typeof window.setupNavbar === "function") {
    await window.setupNavbar();
  }
}

window.bootstrapLayout = bootstrapLayout;
