async function setupNavbar() {
  try {
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
    const nav = document.querySelector(".site-nav");
    if (toggle && navLinks) {
      toggle.addEventListener("click", () => {
        navLinks.classList.toggle("show");
      });
    }


    const manageToggle = document.getElementById("nav-manage-toggle");
    const manageMenu = document.getElementById("nav-manage-menu");
    const closeManageMenu = () => {
      if (manageMenu) {
        manageMenu.classList.remove("show");
      }
      if (manageToggle) {
        manageToggle.setAttribute("aria-expanded", "false");
      }
    };

    if (manageToggle && manageMenu) {
      manageToggle.addEventListener("click", (event) => {
        event.stopPropagation();
        const willOpen = !manageMenu.classList.contains("show");
        manageMenu.classList.toggle("show", willOpen);
        manageToggle.setAttribute("aria-expanded", willOpen ? "true" : "false");
      });

      document.addEventListener("click", (event) => {
        if (!event.target.closest(".nav-dropdown")) {
          closeManageMenu();
        }
      });

      document.addEventListener("keydown", (event) => {
        if (event.key === "Escape") {
          closeManageMenu();
        }
      });
    }

    if (window.ThemeService && typeof window.ThemeService.initThemeToggle === "function") {
      window.ThemeService.initThemeToggle();
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

    if (manageMenu) {
      const visibleMenuItems = Array.from(manageMenu.querySelectorAll("[data-auth='role']"))
        .filter((el) => !el.classList.contains("hidden"));
      const hasAdminOnly = visibleMenuItems.some((el) => {
        const roleAttr = (el.getAttribute("data-roles") || "").toLowerCase();
        return roleAttr.includes("admin") && !roleAttr.includes("power_user");
      });
      manageMenu.classList.toggle("admin-has-extra", hasAdminOnly);
    }

    const logoutBtn = document.getElementById("nav-logout");
    if (logoutBtn) {
      logoutBtn.addEventListener("click", async () => {
        await window.AuthService.logout();
        window.location.replace("/login.html");
      });
    }

    const activeInManage = ["/dashboard.html", "/films.html", "/registi.html", "/proiezioni.html", "/cinemas.html", "/categorie.html", "/utenti.html", "/sale.html", "/validazione-biglietti.html"].includes(pathname);
    if (manageToggle) {
      manageToggle.classList.toggle("active", activeInManage);
    }
  } catch {
  }
}

window.setupNavbar = setupNavbar;
