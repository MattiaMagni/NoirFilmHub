(function () {
  function parseShowDate(dataValue, oraValue) {
    const date = String(dataValue || "").slice(0, 10);
    const time = String(oraValue || "");

    let hhmm = "";
    if (/^\d{2}:\d{2}/.test(time)) {
      hhmm = time.slice(0, 5);
    } else if (time.length >= 16) {
      hhmm = time.slice(11, 16);
    }

    if (!date || !/^\d{2}:\d{2}$/.test(hhmm)) {
      return null;
    }

    const value = new Date(`${date}T${hhmm}:00`);
    return Number.isNaN(value.getTime()) ? null : value;
  }

  function isFutureShow(dataValue, oraValue) {
    const showDate = parseShowDate(dataValue, oraValue);
    if (!showDate) {
      return false;
    }
    return showDate.getTime() >= Date.now();
  }

  function formatShowTime(oraValue) {
    const raw = String(oraValue || "");
    if (/^\d{2}:\d{2}/.test(raw)) {
      return raw.slice(0, 5);
    }
    if (raw.length >= 16) {
      return raw.slice(11, 16);
    }
    const date = new Date(oraValue);
    if (Number.isNaN(date.getTime())) {
      return "--:--";
    }
    const hh = String(date.getHours()).padStart(2, "0");
    const mm = String(date.getMinutes()).padStart(2, "0");
    return `${hh}:${mm}`;
  }

  function formatShowDate(dataValue) {
    const iso = String(dataValue || "").slice(0, 10);
    return iso || "";
  }

  window.ShowUtils = {
    parseShowDate,
    isFutureShow,
    formatShowTime,
    formatShowDate
  };
})();
