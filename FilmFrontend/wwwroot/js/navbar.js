async function setupNavbar() {
  const pathname = window.location.pathname.toLowerCase();
  const links = document.querySelectorAll("#nav-links a[data-nav]");

  links.forEach((link) => {
    const href = (link.getAttribute("href") || "").toLowerCase();
    if (href === pathname || (pathname === "/" && href === "/index.html")) {
      link.classList.add("active");
    }
  });

  const toggle = document.getElementById("mobile-nav-toggle");
  const navLinks = document.getElementById("nav-links");
  if (toggle && navLinks) {
    toggle.addEventListener("click", () => {
      navLinks.classList.toggle("show");
    });
  }

  if (!window.AuthService) {
    return;
  }

  try {
    await window.AuthService.ensureValidAccessToken();
  } catch {
  }

  const isAuthenticated = window.AuthService.isAuthenticated();
  const role = (window.AuthService.getCurrentRole() || "").toLowerCase();

  document.querySelectorAll("[data-auth='authenticated']").forEach((el) => {
    el.classList.toggle("hidden", !isAuthenticated);
  });

  document.querySelectorAll("[data-auth='anonymous']").forEach((el) => {
    el.classList.toggle("hidden", isAuthenticated);
  });

  document.querySelectorAll("[data-auth='role']").forEach((el) => {
    const roles = String(el.getAttribute("data-roles") || "")
      .split(",")
      .map((x) => x.trim().toLowerCase())
      .filter(Boolean);
    const canSee = isAuthenticated && roles.includes(role);
    el.classList.toggle("hidden", !canSee);
  });

  const logoutBtn = document.getElementById("nav-logout");
  if (logoutBtn) {
    logoutBtn.addEventListener("click", async () => {
      await window.AuthService.logout();
      window.location.replace("/login.html");
    });
  }
}

window.setupNavbar = setupNavbar;
