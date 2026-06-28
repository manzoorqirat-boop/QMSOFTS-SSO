using Microsoft.EntityFrameworkCore;
using QMSofts.Identity.Data;
using QMSofts.Identity.Models;

namespace QMSofts.Identity.Services;

/// <summary>
/// Reads/writes the configurable security knobs and enforces the password policy,
/// mirroring ERES (helpers/validators.js). Defaults are GxP-aligned and editable
/// by admins via the settings endpoints.
/// </summary>
public sealed class SettingsService
{
    private readonly IdentityDbContext _db;

    public SettingsService(IdentityDbContext db) => _db = db;

    // Canonical keys + GxP defaults (match ERES seed defaults).
    public static readonly Dictionary<string, string> Defaults = new()
    {
        ["minPasswordLength"] = "8",
        ["forceComplexity"] = "Yes",
        ["passwordHistory"] = "3",    // cannot reuse last N
        ["passwordExpiry"] = "60",    // days; 0 disables
        ["maxFailedAttempts"] = "5",
        ["lockoutDuration"] = "15",   // minutes
        ["sessionTimeout"] = "480",   // minutes (8h)
    };

    public async Task<string> GetAsync(string key, CancellationToken ct = default)
    {
        var row = await _db.Settings.FirstOrDefaultAsync(s => s.Key == key, ct);
        return row?.Value ?? Defaults.GetValueOrDefault(key, "");
    }

    public async Task<int> GetIntAsync(string key, int fallback, CancellationToken ct = default)
    {
        var raw = await GetAsync(key, ct);
        return int.TryParse(raw, out var v) ? v : fallback;
    }

    public async Task<Dictionary<string, string>> GetAllAsync(CancellationToken ct = default)
    {
        var rows = await _db.Settings.ToDictionaryAsync(s => s.Key, s => s.Value, ct);
        // Fill any missing keys with defaults.
        var result = new Dictionary<string, string>(Defaults);
        foreach (var (k, v) in rows) result[k] = v;
        return result;
    }

    public async Task SetAsync(string key, string value, string updatedBy, CancellationToken ct = default)
    {
        var row = await _db.Settings.FirstOrDefaultAsync(s => s.Key == key, ct);
        if (row is null)
            _db.Settings.Add(new Setting { Key = key, Value = value, UpdatedBy = updatedBy });
        else
        {
            row.Value = value;
            row.UpdatedBy = updatedBy;
            row.UpdatedAt = DateTimeOffset.UtcNow;
        }
        await _db.SaveChangesAsync(ct);
    }

    // ── Lockout config (mirrors getLockoutConfig) ──────────────────────
    public async Task<(int maxFailedAttempts, TimeSpan lockoutDuration)> GetLockoutConfigAsync(
        CancellationToken ct = default)
    {
        var max = Math.Max(1, await GetIntAsync("maxFailedAttempts", 5, ct));
        var mins = Math.Max(1, await GetIntAsync("lockoutDuration", 15, ct));
        return (max, TimeSpan.FromMinutes(mins));
    }

    // ── Password policy (mirrors validatePassword) ─────────────────────
    /// <summary>Returns an error message, or null if the password is acceptable.</summary>
    public async Task<string?> ValidatePasswordAsync(string? password, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(password)) return "Password is required";
        if (password.Length > 128) return "Password must be 128 characters or fewer";

        var minLen = Math.Max(8, await GetIntAsync("minPasswordLength", 8, ct));
        if (password.Length < minLen) return $"Password must be at least {minLen} characters";

        var forceComplexity = (await GetAsync("forceComplexity", ct)) != "No";
        if (forceComplexity)
        {
            if (!password.Any(char.IsUpper)) return "Password must contain at least one uppercase letter";
            if (!password.Any(char.IsDigit)) return "Password must contain at least one number";
            const string specials = "!@#$%^&*()_+-=[]{};':\"\\|,.<>/?";
            if (!password.Any(c => specials.Contains(c)))
                return "Password must contain at least one special character";
        }
        return null;
    }
}
