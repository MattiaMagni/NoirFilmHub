(function () {
  const shortDays = ["dom", "lun", "mar", "mer", "gio", "ven", "sab"];
  const shortMonths = ["gen", "feb", "mar", "apr", "mag", "giu", "lug", "ago", "set", "ott", "nov", "dic"];

  function pad(num) {
    return String(num).padStart(2, "0");
  }

  function toIsoDate(value) {
    if (!value) {
      return "";
    }
    const date = value instanceof Date ? value : new Date(value);
    if (Number.isNaN(date.getTime())) {
      return "";
    }
    return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}`;
  }

  function formatDatePill(value) {
    const date = value instanceof Date ? value : new Date(value);
    if (Number.isNaN(date.getTime())) {
      return "";
    }

    const now = new Date();
    const isToday = now.getFullYear() === date.getFullYear() && now.getMonth() === date.getMonth() && now.getDate() === date.getDate();
    if (isToday) {
      return "oggi";
    }

    return `${shortDays[date.getDay()]} ${pad(date.getDate())} ${shortMonths[date.getMonth()]}`;
  }

  function addDays(date, amount) {
    const d = new Date(date);
    d.setDate(d.getDate() + amount);
    return d;
  }

  function nextDays(count) {
    const total = Math.max(1, Number(count) || 1);
    const start = new Date();
    const days = [];
    for (let i = 0; i < total; i += 1) {
      const day = addDays(start, i);
      days.push({
        date: day,
        iso: toIsoDate(day),
        label: formatDatePill(day)
      });
    }
    return days;
  }

  window.DateUtils = {
    toIsoDate,
    formatDatePill,
    nextDays
  };
})();
