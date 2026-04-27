(function () {
  async function resolvePreferredCinemaId() {
    if (window.AuthService && window.AuthService.isAuthenticated()) {
      try {
        const data = await window.ApiClient.get("/auth/me/cinema-preferito");
        if (data && data.cinemaPreferitoId) {
          return Number(data.cinemaPreferitoId);
        }
      } catch {
      }
    }

    const local = Number(localStorage.getItem("selected_cinema_id"));
    return local > 0 ? local : null;
  }

  function buildAcquistaUrl(data) {
    return `/acquista.html?idCinema=${data.idCinema}&idFilm=${data.idFilm}&idSala=${data.idSala}&idShow=${data.idShow}`;
  }

  function redirectToLoginForDestination(destination) {
    if (window.AuthService && typeof window.AuthService.buildLoginUrl === "function") {
      window.location.replace(window.AuthService.buildLoginUrl(destination));
      return;
    }
    window.location.replace(`/login.html?callback=${encodeURIComponent(destination)}`);
  }

  window.ProgrammazioneShared = {
    resolvePreferredCinemaId,
    buildAcquistaUrl,
    redirectToLoginForDestination
  };
})();
