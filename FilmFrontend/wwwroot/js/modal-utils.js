(function () {
  function openModal(contentHtml) {
    closeModal();
    var overlay = document.createElement("div");
    overlay.id = "reusable-modal-overlay";
    overlay.className = "modal-overlay";
    overlay.innerHTML = '<div class="modal-card" id="reusable-modal-card">' + contentHtml + '</div>';
    document.body.appendChild(overlay);
    overlay.addEventListener("click", function (e) {
      if (e.target === overlay) closeModal();
    });
    document.addEventListener("keydown", handleEscape);
    return document.getElementById("reusable-modal-card");
  }

  function closeModal() {
    var overlay = document.getElementById("reusable-modal-overlay");
    if (overlay) overlay.remove();
    document.removeEventListener("keydown", handleEscape);
  }

  function handleEscape(e) {
    if (e.key === "Escape") closeModal();
  }

  window.ModalUtils = { open: openModal, close: closeModal };
})();
