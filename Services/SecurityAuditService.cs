using FilmAPI.Data;
using FilmAPI.Model;
using Microsoft.EntityFrameworkCore;

namespace FilmAPI.Services;

public class SecurityAuditService
{
    private readonly FilmDbContext _db;

    public SecurityAuditService(FilmDbContext db)
    {
        _db = db;
    }

    public async Task LogEventAsync(int? utenteId, string eventType, string? provider = null,
        string? ipAddress = null, string? userAgent = null, string? details = null)
    {
        var log = new UserSecurityAuditLog
        {
            UtenteId = utenteId,
            EventType = eventType,
            Provider = provider,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            Details = details,
            CreatedAtUtc = DateTime.UtcNow
        };
        _db.UserSecurityAuditLogs.Add(log);
        await _db.SaveChangesAsync();
    }

    public async Task<List<DTOs.AuditLogEntryDTO>> GetRecentLogsAsync(int utenteId, int count = 20)
    {
        return await _db.UserSecurityAuditLogs
            .Where(l => l.UtenteId == utenteId)
            .OrderByDescending(l => l.CreatedAtUtc)
            .Take(count)
            .Select(l => new DTOs.AuditLogEntryDTO
            {
                Id = l.Id,
                EventType = l.EventType,
                Provider = l.Provider,
                IpAddress = l.IpAddress,
                Details = l.Details,
                CreatedAtUtc = l.CreatedAtUtc
            })
            .ToListAsync();
    }

    public async Task CleanupOldLogsAsync(int regularRetentionDays = 90, int criticalRetentionDays = 365)
    {
        var regularCutoff = DateTime.UtcNow.AddDays(-regularRetentionDays);
        var criticalCutoff = DateTime.UtcNow.AddDays(-criticalRetentionDays);

        var criticalEvents = new[] { "RoleChanged", "AccountDeleted", "AccountDisabled", "AccountEnabled" };

        await _db.UserSecurityAuditLogs
            .Where(l => !criticalEvents.Contains(l.EventType) && l.CreatedAtUtc < regularCutoff)
            .ExecuteDeleteAsync();

        await _db.UserSecurityAuditLogs
            .Where(l => criticalEvents.Contains(l.EventType) && l.CreatedAtUtc < criticalCutoff)
            .ExecuteDeleteAsync();
    }
}
