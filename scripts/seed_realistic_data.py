import json
import sys
import urllib.error
import urllib.request
from datetime import date, datetime, timedelta
from typing import Any


BASE_URL = sys.argv[1] if len(sys.argv) > 1 else "http://localhost:5000"
OPENER = urllib.request.build_opener(urllib.request.ProxyHandler({}))
ADMIN_EMAIL = "admin@filmapi.local"
ADMIN_PASSWORD = "Admin123!"


def request_json(method: str, path: str, data=None, token: str | None = None):
    url = f"{BASE_URL}{path}"
    body = None
    headers = {}

    if data is not None:
        body = json.dumps(data).encode("utf-8")
        headers["Content-Type"] = "application/json"

    if token:
        headers["Authorization"] = f"Bearer {token}"

    req = urllib.request.Request(url=url, data=body, method=method, headers=headers)

    try:
        with OPENER.open(req) as response:
            content = response.read().decode("utf-8")
            if not content:
                return None
            return json.loads(content)
    except urllib.error.HTTPError as ex:
        details = ex.read().decode("utf-8", errors="ignore")
        raise RuntimeError(f"HTTP {ex.code} {method} {path}: {details}") from ex


def require_object(value: Any, context: str) -> dict:
    if not isinstance(value, dict):
        raise RuntimeError(f"Risposta non valida per {context}: {value}")
    return value


def get_ids(path: str, token: str | None = None):
    items = request_json("GET", path, token=token) or []
    return [item["id"] for item in items]


def get_admin_token() -> str:
    result = require_object(
        request_json(
            "POST",
            "/auth/login",
            {
                "email": ADMIN_EMAIL,
                "password": ADMIN_PASSWORD,
            },
        ),
        "POST /auth/login",
    )
    token = result.get("accessToken")
    if not token:
        raise RuntimeError("Login admin fallito: access token mancante")
    return token


def clear_existing_data(token: str):
    for item_id in get_ids("/prenotazioni", token=token):
        request_json("PUT", f"/prenotazioni/{item_id}/annulla", {}, token=token)

    for item_id in get_ids("/proiezioni", token=token):
        request_json("DELETE", f"/proiezioni/{item_id}", token=token)

    for item_id in get_ids("/films", token=token):
        request_json("DELETE", f"/films/{item_id}", token=token)

    for item_id in get_ids("/cinemas", token=token):
        request_json("DELETE", f"/cinemas/{item_id}", token=token)

    for item_id in get_ids("/registi", token=token):
        request_json("DELETE", f"/registi/{item_id}", token=token)


def seed_data(token: str):
    registi_seed = [
        {"key": "nolan", "nome": "Christopher", "cognome": "Nolan", "nazionalita": "Britannica"},
        {"key": "villeneuve", "nome": "Denis", "cognome": "Villeneuve", "nazionalita": "Canadese"},
        {"key": "gerwig", "nome": "Greta", "cognome": "Gerwig", "nazionalita": "Statunitense"},
        {"key": "sorrentino", "nome": "Paolo", "cognome": "Sorrentino", "nazionalita": "Italiana"},
        {"key": "guadagnino", "nome": "Luca", "cognome": "Guadagnino", "nazionalita": "Italiana"},
        {"key": "bigelow", "nome": "Kathryn", "cognome": "Bigelow", "nazionalita": "Statunitense"},
        {"key": "fincher", "nome": "David", "cognome": "Fincher", "nazionalita": "Statunitense"},
        {"key": "bong", "nome": "Bong", "cognome": "Joon-ho", "nazionalita": "Sudcoreana"},
        {"key": "chazelle", "nome": "Damien", "cognome": "Chazelle", "nazionalita": "Statunitense"},
        {"key": "scott", "nome": "Ridley", "cognome": "Scott", "nazionalita": "Britannica"},
    ]

    registi_ids = {}
    for regista in registi_seed:
        created = require_object(
            request_json(
            "POST",
                "/registi",
                {
                    "nome": regista["nome"],
                    "cognome": regista["cognome"],
                    "nazionalita": regista["nazionalita"],
                },
                token=token,
            ),
            "POST /registi",
        )
        registi_ids[regista["key"]] = created["id"]

    today = date.today()

    category_map = {
        "fantasy": 1,
        "horror": 2,
        "drammatico": 3,
        "commedia": 4,
        "azione": 5,
        "thriller": 6,
    }

    films_seed = [
        {"titolo": "Inception", "dataProduzione": "2010-07-16T00:00:00", "regista": "nolan", "durata": 148, "categorie": ["thriller", "azione"], "copertinaPath": "https://image.tmdb.org/t/p/w500/oYuLEt3zVCKq57qu2F8dT7NIa6f.jpg"},
        {"titolo": "Interstellar", "dataProduzione": "2014-11-07T00:00:00", "regista": "nolan", "durata": 169, "categorie": ["fantasy", "drammatico"], "copertinaPath": "https://image.tmdb.org/t/p/w500/gEU2QniE6E77NI6lCU6MxlNBvIx.jpg"},
        {"titolo": "Dune - Parte Due", "dataProduzione": "2024-02-28T00:00:00", "regista": "villeneuve", "durata": 166, "categorie": ["fantasy", "azione"], "copertinaPath": "https://image.tmdb.org/t/p/w500/1pdfLvkbY9ohJlCjQH2CZjjYVvJ.jpg"},
        {"titolo": "Arrival", "dataProduzione": "2016-11-11T00:00:00", "regista": "villeneuve", "durata": 116, "categorie": ["drammatico", "thriller"], "copertinaPath": "https://image.tmdb.org/t/p/w500/x2FJsf1ElAgr63Y3PNPtJrcmpoe.jpg"},
        {"titolo": "Barbie", "dataProduzione": "2023-07-21T00:00:00", "regista": "gerwig", "durata": 114, "categorie": ["commedia"], "copertinaPath": "https://image.tmdb.org/t/p/w500/iuFNMS8U5cb6xfzi51Dbkovj7vM.jpg"},
        {"titolo": "Lady Bird", "dataProduzione": "2017-11-03T00:00:00", "regista": "gerwig", "durata": 94, "categorie": ["drammatico", "commedia"], "copertinaPath": "https://image.tmdb.org/t/p/w500/iySFtKLrWvVzXzlFj7x1zalxi5G.jpg"},
        {"titolo": "La grande bellezza", "dataProduzione": "2013-05-21T00:00:00", "regista": "sorrentino", "durata": 141, "categorie": ["drammatico"], "copertinaPath": "https://upload.wikimedia.org/wikipedia/en/1/19/The_Great_Beauty_poster.jpg"},
        {"titolo": "E' stata la mano di Dio", "dataProduzione": "2021-11-24T00:00:00", "regista": "sorrentino", "durata": 130, "categorie": ["drammatico"], "copertinaPath": "https://upload.wikimedia.org/wikipedia/en/4/4c/The_Hand_of_God_%282021%29_film_poster.jpg"},
        {"titolo": "Challengers", "dataProduzione": "2024-04-24T00:00:00", "regista": "guadagnino", "durata": 131, "categorie": ["drammatico"], "copertinaPath": "https://image.tmdb.org/t/p/w500/H6vke7zGiuLsz4v4RPeReb9rsv.jpg"},
        {"titolo": "The Hurt Locker", "dataProduzione": "2008-09-04T00:00:00", "regista": "bigelow", "durata": 131, "categorie": ["azione", "drammatico"], "copertinaPath": "https://upload.wikimedia.org/wikipedia/en/6/6c/HLposterUSA2.jpg"},
        {"titolo": "Gone Girl", "dataProduzione": "2014-10-03T00:00:00", "regista": "fincher", "durata": 149, "categorie": ["thriller"], "copertinaPath": "https://upload.wikimedia.org/wikipedia/en/0/05/Gone_Girl_Poster.jpg"},
        {"titolo": "Parasite", "dataProduzione": "2019-05-30T00:00:00", "regista": "bong", "durata": 132, "categorie": ["drammatico", "thriller"], "copertinaPath": "https://image.tmdb.org/t/p/w500/7IiTTgloJzvGI1TAYymCfbfl3vT.jpg"},
        {"titolo": "La La Land", "dataProduzione": "2016-12-09T00:00:00", "regista": "chazelle", "durata": 128, "categorie": ["commedia", "drammatico"], "copertinaPath": "https://image.tmdb.org/t/p/w500/uDO8zWDhfWwoFdKS4fzkUJt0Rf0.jpg"},
        {"titolo": "Blade Runner 2049", "dataProduzione": "2017-10-05T00:00:00", "regista": "scott", "durata": 164, "categorie": ["fantasy", "thriller"], "copertinaPath": "https://image.tmdb.org/t/p/w500/gajva2L0rPYkEWjzgFlBXCAVBE5.jpg"},
    ]

    film_ids = []
    for film in films_seed:
        created = require_object(
            request_json(
            "POST",
            "/films",
            {
                "titolo": film["titolo"],
                "dataProduzione": film["dataProduzione"],
                "registaId": registi_ids[film["regista"]],
                "durata": film["durata"],
                "copertinaPath": film["copertinaPath"],
                "filmatoPath": None,
                "categorieIds": [category_map[key] for key in film["categorie"]],
            },
            token=token,
            ),
            "POST /films",
        )
        film_ids.append(created["id"])

    cinemas_seed = [
        {"nome": "Cinema Odeon", "indirizzo": "Via Santa Radegonda 8", "citta": "Milano", "capienza": 220},
        {"nome": "Cinema Adriano", "indirizzo": "Piazza Cavour 22", "citta": "Roma", "capienza": 260},
        {"nome": "Cinema Massimo", "indirizzo": "Via Verdi 18", "citta": "Torino", "capienza": 180},
        {"nome": "Cinema Ariston", "indirizzo": "Piazza Ottaviani 12", "citta": "Firenze", "capienza": 200},
        {"nome": "Cinema Modernissimo", "indirizzo": "Piazza Re Enzo 1", "citta": "Bologna", "capienza": 240},
        {"nome": "Cinema Metropolitan", "indirizzo": "Via Chiaia 149", "citta": "Napoli", "capienza": 230},
    ]

    cinema_ids = []
    for cinema in cinemas_seed:
        created = require_object(request_json("POST", "/cinemas", cinema, token=token), "POST /cinemas")
        cinema_ids.append(created["id"])

    show_dates = [
        datetime.combine(today + timedelta(days=offset), datetime.min.time()).isoformat()
        for offset in range(0, 7)
    ]
    show_times = [
        "0001-01-01T16:30:00",
        "0001-01-01T18:30:00",
        "0001-01-01T21:00:00",
        "0001-01-01T22:30:00",
    ]

    screenings_to_create = []
    for idx, cinema_id in enumerate(cinema_ids):
        for slot in range(4):
            film_id = film_ids[(idx * 2 + slot) % len(film_ids)]
            data = show_dates[(idx + slot) % len(show_dates)]
            ora = show_times[slot % len(show_times)]
            screenings_to_create.append(
                {
                    "cinemaId": cinema_id,
                    "filmId": film_id,
                    "data": data,
                    "ora": ora,
                }
            )

    for screening in screenings_to_create:
        request_json("POST", "/proiezioni", screening, token=token)


def main():
    token = get_admin_token()
    clear_existing_data(token)
    seed_data(token)

    registi_count = len(get_ids("/registi"))
    films_count = len(get_ids("/films"))
    cinemas_count = len(get_ids("/cinemas"))
    proiezioni_count = len(get_ids("/proiezioni"))

    print(
        "Seed completato:",
        f"registi={registi_count}",
        f"films={films_count}",
        f"cinemas={cinemas_count}",
        f"proiezioni={proiezioni_count}",
    )


if __name__ == "__main__":
    main()
