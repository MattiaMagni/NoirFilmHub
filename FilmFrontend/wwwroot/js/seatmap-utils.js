(function () {
  function defaultSeatMap(rows, cols) {
    const list = [];
    for (let r = 0; r < rows; r += 1) {
      const rowLetter = String.fromCharCode(65 + r);
      for (let c = 1; c <= cols; c += 1) {
        list.push(`${rowLetter}${c}`);
      }
    }
    return list;
  }

  function parseSeatMap(rawJson, rows, cols) {
    if (!rawJson) {
      return defaultSeatMap(rows, cols);
    }

    try {
      const parsed = JSON.parse(rawJson);
      if (Array.isArray(parsed.seats) && parsed.seats.length) {
        return parsed.seats.map((x) => String(x).toUpperCase());
      }
      return defaultSeatMap(Number(parsed.rows || rows), Number(parsed.cols || cols));
    } catch {
      return defaultSeatMap(rows, cols);
    }
  }

  window.SeatMapUtils = {
    defaultSeatMap,
    parseSeatMap
  };
})();
