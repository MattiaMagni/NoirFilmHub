using System.Net.Http.Headers;
using System.Text.Json;
using FilmAPI.Data;
using FilmAPI.DTOs;
using FilmAPI.Model;
using Microsoft.EntityFrameworkCore;

namespace FilmAPI.Services;

public class TmdbService
{
    private readonly FilmDbContext _db;
    private readonly HttpClient _httpClient;
    private readonly ILogger<TmdbService> _logger;
    private readonly string _token;
    private readonly string _baseUrl;
    private readonly string _language;
    private readonly string _fallbackLanguage;

    public TmdbService(FilmDbContext db, HttpClient httpClient, ILogger<TmdbService> logger)
    {
        _db = db;
        _httpClient = httpClient;
        _logger = logger;
        _token = Environment.GetEnvironmentVariable("TMDB_API_READ_TOKEN") ?? string.Empty;
        _baseUrl = Environment.GetEnvironmentVariable("TMDB_BASE_URL") ?? "https://api.themoviedb.org/3";
        _language = Environment.GetEnvironmentVariable("TMDB_LANGUAGE") ?? "it-IT";
        _fallbackLanguage = Environment.GetEnvironmentVariable("TMDB_FALLBACK_LANGUAGE") ?? "en-US";
    }

    public bool IsConfigured() => !string.IsNullOrWhiteSpace(_token);

    public async Task<List<TmdbLatestItemDTO>> GetLatestReleasesAsync(int limit, int page)
    {
        if (!IsConfigured())
        {
            _logger.LogWarning("GetLatestReleasesAsync chiamato ma TMDB non configurato");
            return [];
        }

        var safeLimit = Math.Clamp(limit <= 0 ? 20 : limit, 1, 50);
        var safePage = Math.Clamp(page <= 0 ? 1 : page, 1, 50);
        var region = Environment.GetEnvironmentVariable("TMDB_REGION") ?? "IT";
        var today = DateTime.UtcNow.Date;
        var oneYearAgo = today.AddYears(-1);
        var todayIso = today.ToString("yyyy-MM-dd");
        var oneYearAgoIso = oneYearAgo.ToString("yyyy-MM-dd");

        var url =
            $"{_baseUrl}/discover/movie?include_adult=false&include_video=false&language={Uri.EscapeDataString(_language)}&sort_by=primary_release_date.desc&page={safePage}&region={Uri.EscapeDataString(region)}&primary_release_date.lte={todayIso}&primary_release_date.gte={oneYearAgoIso}";

        _logger.LogInformation("GetLatestReleasesAsync — URL: {Url}", url);
        var doc = await GetJsonAsync(url);
        if (doc is null)
        {
            _logger.LogWarning("GetLatestReleasesAsync — GetJsonAsync ha ritornato null per l'URL: {Url}", url);
            return [];
        }

        if (!doc.RootElement.TryGetProperty("results", out var results) || results.ValueKind != JsonValueKind.Array)
        {
            _logger.LogWarning("GetLatestReleasesAsync — Risposta TMDB senza 'results' array. Keys: {Keys}", string.Join(",", doc.RootElement.EnumerateObject().Select(p => p.Name)));
            return [];
        }

        _logger.LogInformation("GetLatestReleasesAsync — Ricevuti {Count} risultati da TMDB", results.GetArrayLength());

        var existingTmdbIds = await _db.Films
            .AsNoTracking()
            .Where(f => f.TmdbMovieId.HasValue)
            .Select(f => f.TmdbMovieId!.Value)
            .ToListAsync();
        var existingSet = existingTmdbIds.ToHashSet();

        var list = new List<TmdbLatestItemDTO>();
        foreach (var item in results.EnumerateArray())
        {
            var id = ReadInt(item, "id");
            if (!id.HasValue || id.Value <= 0)
            {
                continue;
            }

            var poster = BuildImagePath(ReadString(item, "poster_path"), "w500");
            if (string.IsNullOrWhiteSpace(poster))
            {
                continue;
            }

            var releaseDateRaw = ReadString(item, "release_date");
            DateTime? releaseDate = null;
            if (DateTime.TryParse(releaseDateRaw, out var parsedDate))
            {
                releaseDate = parsedDate;
            }

            var title = ReadString(item, "title") ?? string.Empty;
            var originalTitle = ReadString(item, "original_title") ?? string.Empty;
            var overview = ReadString(item, "overview") ?? string.Empty;
            var backdrop = BuildImagePath(ReadString(item, "backdrop_path"), "w1280");
            var voteAverage = ReadDouble(item, "vote_average");

            list.Add(new TmdbLatestItemDTO
            {
                TmdbMovieId = id.Value,
                Titolo = title,
                TitoloOriginale = originalTitle,
                DataUscita = releaseDate,
                PosterPath = poster,
                BackdropPath = backdrop,
                Overview = overview,
                VoteAverage = voteAverage,
                AlreadyInCatalog = existingSet.Contains(id.Value)
            });

            if (list.Count >= safeLimit)
            {
                break;
            }
        }

        return list;
    }

    public async Task<List<TmdbLatestItemDTO>> SearchMoviesByTitleAsync(string title, int limit, int page)
    {
        if (!IsConfigured())
        {
            return [];
        }

        var query = (title ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        var safeLimit = Math.Clamp(limit <= 0 ? 20 : limit, 1, 50);
        var safePage = Math.Clamp(page <= 0 ? 1 : page, 1, 50);
        var url =
            $"{_baseUrl}/search/movie?include_adult=false&language={Uri.EscapeDataString(_language)}&page={safePage}&query={Uri.EscapeDataString(query)}";

        var doc = await GetJsonAsync(url);
        if (doc is null || !doc.RootElement.TryGetProperty("results", out var results) || results.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var existingTmdbIds = await _db.Films
            .AsNoTracking()
            .Where(f => f.TmdbMovieId.HasValue)
            .Select(f => f.TmdbMovieId!.Value)
            .ToListAsync();
        var existingSet = existingTmdbIds.ToHashSet();

        var list = new List<TmdbLatestItemDTO>();
        foreach (var item in results.EnumerateArray())
        {
            var id = ReadInt(item, "id");
            if (!id.HasValue || id.Value <= 0)
            {
                continue;
            }

            var releaseDateRaw = ReadString(item, "release_date");
            DateTime? releaseDate = null;
            if (DateTime.TryParse(releaseDateRaw, out var parsedDate))
            {
                releaseDate = parsedDate;
            }

            list.Add(new TmdbLatestItemDTO
            {
                TmdbMovieId = id.Value,
                Titolo = ReadString(item, "title") ?? string.Empty,
                TitoloOriginale = ReadString(item, "original_title") ?? string.Empty,
                DataUscita = releaseDate,
                PosterPath = BuildImagePath(ReadString(item, "poster_path"), "w500"),
                BackdropPath = BuildImagePath(ReadString(item, "backdrop_path"), "w1280"),
                Overview = ReadString(item, "overview") ?? string.Empty,
                VoteAverage = ReadDouble(item, "vote_average"),
                AlreadyInCatalog = existingSet.Contains(id.Value)
            });

            if (list.Count >= safeLimit)
            {
                break;
            }
        }

        return list;
    }

    public async Task<(int Created, int SkippedExisting, int Failed, List<int> CreatedFilmIds)> ImportMoviesAsync(List<int> tmdbMovieIds)
    {
        if (!IsConfigured())
        {
            return (0, 0, tmdbMovieIds?.Count ?? 0, []);
        }

        var ids = (tmdbMovieIds ?? [])
            .Where(x => x > 0)
            .Distinct()
            .Take(50)
            .ToList();

        if (ids.Count == 0)
        {
            return (0, 0, 0, []);
        }

        var created = 0;
        var skipped = 0;
        var failed = 0;
        var createdIds = new List<int>();

        foreach (var movieId in ids)
        {
            var exists = await _db.Films.AnyAsync(f => f.TmdbMovieId == movieId);
            if (exists)
            {
                skipped++;
                continue;
            }

            var details = await GetMovieDetailsAsync(movieId, _language) ?? await GetMovieDetailsAsync(movieId, _fallbackLanguage);
            if (details is null)
            {
                failed++;
                continue;
            }

            try
            {
                var film = await BuildFilmFromTmdbAsync(movieId, details);
                _db.Films.Add(film);
                await _db.SaveChangesAsync();

                await AssignDefaultCategoryIfNeededAsync(film.Id);
                await _db.SaveChangesAsync();

                created++;
                createdIds.Add(film.Id);
            }
            catch
            {
                failed++;
            }
        }

        return (created, skipped, failed, createdIds);
    }

    public async Task<(bool Success, string Message)> SyncFilmAsync(int filmId)
    {
        if (!IsConfigured())
        {
            _logger.LogWarning("SyncFilmAsync({FilmId}) fallito: TMDB non configurato (token vuoto)", filmId);
            return (false, "TMDB non configurato: manca TMDB_API_READ_TOKEN");
        }

        var film = await _db.Films.FirstOrDefaultAsync(f => f.Id == filmId);
        if (film is null)
        {
            _logger.LogWarning("SyncFilmAsync({FilmId}) fallito: film non trovato nel DB", filmId);
            return (false, $"Film con ID {filmId} non trovato");
        }

        _logger.LogInformation("SyncFilmAsync({FilmId}) — Film: {Titolo}, TmdbMovieId: {TmdbId}", filmId, film.Titolo, film.TmdbMovieId);

        var year = (film.DataUscita ?? film.DataProduzione).Year;
        var movieId = film.TmdbMovieId ?? await SearchMovieIdAsync(film.Titolo, year);
        if (!movieId.HasValue)
        {
            _logger.LogWarning("SyncFilmAsync({FilmId}) — Nessun match TMDB per '{Titolo}' (anno: {Year})", filmId, film.Titolo, year);
            film.TmdbSyncStato = "NotFound";
            film.UltimaSyncTmdbUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            return (false, $"Nessun match TMDB trovato per '{film.Titolo}'");
        }

        _logger.LogInformation("SyncFilmAsync({FilmId}) — TmdbMovieId trovato: {TmdbId}, recupero dettagli...", filmId, movieId.Value);
        var details = await GetMovieDetailsAsync(movieId.Value, _language) ?? await GetMovieDetailsAsync(movieId.Value, _fallbackLanguage);
        if (details is null)
        {
            _logger.LogError("SyncFilmAsync({FilmId}) — Dettagli TMDB non disponibili per movieId {TmdbId} (simple e fallback fallite)", filmId, movieId.Value);
            film.TmdbSyncStato = "Failed";
            film.UltimaSyncTmdbUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            return (false, $"Dettagli TMDB non disponibili per movie ID {movieId.Value}");
        }

        ApplyDetails(film, details, movieId.Value);
        await UpdateDirectorAsync(film, details);
        await _db.SaveChangesAsync();
        _logger.LogInformation("SyncFilmAsync({FilmId}) — Completata con successo (TmdbMovieId: {TmdbId})", filmId, movieId.Value);
        return (true, "Sync TMDB completata");
    }

    public async Task<(int Success, int Failed)> SyncMissingAsync()
    {
        var films = await _db.Films
            .Where(f => f.TmdbMovieId == null || string.IsNullOrWhiteSpace(f.DescrizioneLunga) || string.IsNullOrWhiteSpace(f.CastPrincipale))
            .Select(f => f.Id)
            .ToListAsync();

        var success = 0;
        var failed = 0;
        foreach (var id in films)
        {
            var result = await SyncFilmAsync(id);
            if (result.Success)
            {
                success++;
            }
            else
            {
                failed++;
            }
        }

        return (success, failed);
    }

    private async Task<int?> SearchMovieIdAsync(string titolo, int year)
    {
        var encoded = Uri.EscapeDataString(titolo);
        var url = $"{_baseUrl}/search/movie?query={encoded}&language={Uri.EscapeDataString(_language)}&year={year}";
        var doc = await GetJsonAsync(url);
        if (doc is null)
        {
            return null;
        }

        if (!doc.RootElement.TryGetProperty("results", out var results) || results.ValueKind != JsonValueKind.Array || results.GetArrayLength() == 0)
        {
            return null;
        }

        var first = results[0];
        return first.TryGetProperty("id", out var idElement) && idElement.TryGetInt32(out var id)
            ? id
            : null;
    }

    private async Task<JsonDocument?> GetMovieDetailsAsync(int movieId, string language)
    {
        var url = $"{_baseUrl}/movie/{movieId}?append_to_response=videos,credits&language={Uri.EscapeDataString(language)}";
        return await GetJsonAsync(url);
    }

    private async Task<JsonDocument?> GetJsonAsync(string url)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);

            using var response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                _logger.LogError("TMDB API GET {Url} → {StatusCode} — Body: {Body}",
                    url, (int)response.StatusCode,
                    body.Length > 500 ? body[..500] : body);
                return null;
            }

            var json = await response.Content.ReadAsStringAsync();
            return JsonDocument.Parse(json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Eccezione in GetJsonAsync({Url})", url);
            return null;
        }
    }

    private static void ApplyDetails(Film film, JsonDocument details, int movieId)
    {
        var root = details.RootElement;
        film.TmdbMovieId = movieId;
        film.TitoloOriginale = ReadString(root, "original_title") ?? film.TitoloOriginale;
        film.DescrizioneLunga = ReadString(root, "overview") ?? film.DescrizioneLunga;
        film.CopertinaPath = BuildImagePath(ReadString(root, "poster_path"), "w500") ?? film.CopertinaPath;
        film.BackdropPath = BuildImagePath(ReadString(root, "backdrop_path"), "w780") ?? film.BackdropPath;

        var releaseDate = ReadString(root, "release_date");
        if (DateTime.TryParse(releaseDate, out var parsedDate))
        {
            film.DataUscita = parsedDate;
        }

        var runtime = ReadInt(root, "runtime");
        if (runtime.HasValue && runtime.Value > 0)
        {
            film.Durata = runtime.Value;
        }

        if (root.TryGetProperty("credits", out var credits))
        {
            film.CastPrincipale = BuildCast(credits);
        }

        film.FilmatoPath = ResolveTrailer(root) ?? film.FilmatoPath;
        film.TmdbSyncStato = "Synced";
        film.UltimaSyncTmdbUtc = DateTime.UtcNow;
    }

    private async Task UpdateDirectorAsync(Film film, JsonDocument details)
    {
        var root = details.RootElement;
        if (!root.TryGetProperty("credits", out var credits) || !credits.TryGetProperty("crew", out var crew) || crew.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        JsonElement? director = null;
        foreach (var member in crew.EnumerateArray())
        {
            var job = ReadString(member, "job");
            if (string.Equals(job, "Director", StringComparison.OrdinalIgnoreCase))
            {
                director = member;
                break;
            }
        }

        if (!director.HasValue)
        {
            return;
        }

        var name = ReadString(director.Value, "name");
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var nome = parts.Length > 0 ? parts[0] : name;
        var cognome = parts.Length > 1 ? string.Join(' ', parts.Skip(1)) : "-";

        var regista = await _db.Registi.FirstOrDefaultAsync(r => r.Nome == nome && r.Cognome == cognome);
        if (regista is null)
        {
            regista = new Regista
            {
                Nome = nome,
                Cognome = cognome,
                Nazionalita = "N/D"
            };
            _db.Registi.Add(regista);
            await _db.SaveChangesAsync();
        }

        film.RegistaId = regista.Id;
    }

    private async Task<Film> BuildFilmFromTmdbAsync(int movieId, JsonDocument details)
    {
        var root = details.RootElement;
        var title = ReadString(root, "title") ?? ReadString(root, "original_title") ?? $"Film TMDB {movieId}";
        var originalTitle = ReadString(root, "original_title") ?? title;
        var releaseDateRaw = ReadString(root, "release_date");
        DateTime releaseDate;
        if (!DateTime.TryParse(releaseDateRaw, out releaseDate))
        {
            releaseDate = DateTime.UtcNow.Date;
        }

        var runtime = ReadInt(root, "runtime") ?? 100;
        if (runtime <= 0)
        {
            runtime = 100;
        }

        var registaId = await ResolveDirectorIdAsync(root);

        var film = new Film
        {
            Titolo = title,
            TitoloOriginale = originalTitle,
            DataProduzione = releaseDate,
            DataUscita = releaseDate,
            RegistaId = registaId,
            Durata = runtime,
            CopertinaPath = BuildImagePath(ReadString(root, "poster_path"), "w500"),
            BackdropPath = BuildImagePath(ReadString(root, "backdrop_path"), "w1280"),
            FilmatoPath = ResolveTrailer(root),
            DescrizioneLunga = ReadString(root, "overview") ?? string.Empty,
            CastPrincipale = root.TryGetProperty("credits", out var credits) ? BuildCast(credits) : string.Empty,
            TmdbMovieId = movieId,
            UltimaSyncTmdbUtc = DateTime.UtcNow,
            TmdbSyncStato = "Synced"
        };

        return film;
    }

    private async Task<int> ResolveDirectorIdAsync(JsonElement movieRoot)
    {
        if (!movieRoot.TryGetProperty("credits", out var credits)
            || !credits.TryGetProperty("crew", out var crew)
            || crew.ValueKind != JsonValueKind.Array)
        {
            return await EnsureUnknownDirectorAsync();
        }

        JsonElement? director = null;
        foreach (var member in crew.EnumerateArray())
        {
            var job = ReadString(member, "job");
            if (string.Equals(job, "Director", StringComparison.OrdinalIgnoreCase))
            {
                director = member;
                break;
            }
        }

        if (!director.HasValue)
        {
            return await EnsureUnknownDirectorAsync();
        }

        var name = ReadString(director.Value, "name");
        if (string.IsNullOrWhiteSpace(name))
        {
            return await EnsureUnknownDirectorAsync();
        }

        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var nome = parts.Length > 0 ? parts[0] : name;
        var cognome = parts.Length > 1 ? string.Join(' ', parts.Skip(1)) : "-";

        var regista = await _db.Registi.FirstOrDefaultAsync(r => r.Nome == nome && r.Cognome == cognome);
        if (regista is null)
        {
            regista = new Regista
            {
                Nome = nome,
                Cognome = cognome,
                Nazionalita = "N/D"
            };
            _db.Registi.Add(regista);
            await _db.SaveChangesAsync();
        }

        return regista.Id;
    }

    private async Task<int> EnsureUnknownDirectorAsync()
    {
        var existing = await _db.Registi.FirstOrDefaultAsync(r => r.Nome == "Regista" && r.Cognome == "Sconosciuto");
        if (existing is not null)
        {
            return existing.Id;
        }

        var regista = new Regista
        {
            Nome = "Regista",
            Cognome = "Sconosciuto",
            Nazionalita = "N/D"
        };
        _db.Registi.Add(regista);
        await _db.SaveChangesAsync();
        return regista.Id;
    }

    private async Task AssignDefaultCategoryIfNeededAsync(int filmId)
    {
        var hasAny = await _db.FilmCategorie.AnyAsync(fc => fc.FilmId == filmId);
        if (hasAny)
        {
            return;
        }

        var categoria = await _db.Categorie.OrderBy(c => c.Id).FirstOrDefaultAsync();
        if (categoria is null)
        {
            categoria = new Categoria { Nome = "Cinema", Descrizione = "Categoria generica" };
            _db.Categorie.Add(categoria);
            await _db.SaveChangesAsync();
        }

        _db.FilmCategorie.Add(new FilmCategoria
        {
            FilmId = filmId,
            CategoriaId = categoria.Id
        });
    }

    private static string BuildCast(JsonElement credits)
    {
        if (!credits.TryGetProperty("cast", out var cast) || cast.ValueKind != JsonValueKind.Array)
        {
            return string.Empty;
        }

        var names = new List<string>();
        foreach (var member in cast.EnumerateArray().Take(8))
        {
            var name = ReadString(member, "name");
            if (!string.IsNullOrWhiteSpace(name))
            {
                names.Add(name.Trim());
            }
        }

        return string.Join(", ", names);
    }

    private static string? ResolveTrailer(JsonElement root)
    {
        if (!root.TryGetProperty("videos", out var videosObj) || !videosObj.TryGetProperty("results", out var videos) || videos.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        JsonElement? chosen = null;
        foreach (var item in videos.EnumerateArray())
        {
            var site = ReadString(item, "site");
            var type = ReadString(item, "type");
            var official = ReadBool(item, "official");
            if (string.Equals(site, "YouTube", StringComparison.OrdinalIgnoreCase) && string.Equals(type, "Trailer", StringComparison.OrdinalIgnoreCase) && official == true)
            {
                chosen = item;
                break;
            }
        }

        if (!chosen.HasValue)
        {
            foreach (var item in videos.EnumerateArray())
            {
                var site = ReadString(item, "site");
                if (string.Equals(site, "YouTube", StringComparison.OrdinalIgnoreCase))
                {
                    chosen = item;
                    break;
                }
            }
        }

        if (!chosen.HasValue)
        {
            return null;
        }

        var key = ReadString(chosen.Value, "key");
        return string.IsNullOrWhiteSpace(key) ? null : $"https://www.youtube.com/watch?v={key}";
    }

    private static string? BuildImagePath(string? path, string size)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        return $"https://image.tmdb.org/t/p/{size}{path}";
    }

    private static string? ReadString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static int? ReadInt(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var value) && value.TryGetInt32(out var intValue)
            ? intValue
            : null;
    }

    private static bool? ReadBool(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? value.GetBoolean()
            : null;
    }

    private static double? ReadDouble(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var value) && value.TryGetDouble(out var doubleValue)
            ? doubleValue
            : null;
    }
}

public class TmdbLatestItemDTO
{
    public int TmdbMovieId { get; set; }
    public string Titolo { get; set; } = string.Empty;
    public string TitoloOriginale { get; set; } = string.Empty;
    public DateTime? DataUscita { get; set; }
    public string? PosterPath { get; set; }
    public string? BackdropPath { get; set; }
    public string Overview { get; set; } = string.Empty;
    public double? VoteAverage { get; set; }
    public bool AlreadyInCatalog { get; set; }
}
