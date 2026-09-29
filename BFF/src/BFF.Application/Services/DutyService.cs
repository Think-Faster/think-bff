using BFF.Context;
using BFF.Models.Entities;
using BFF.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace BFF.Application.Services;

/// <summary>
/// Кто сейчас на смене. График (schedule_entries) ведётся по московскому времени: строка Working —
/// смена в каждый день интервала с ShiftStart на ShiftHours часов (без ShiftStart — весь день). Смена
/// «сутки через трое» с 08:00 на 24 ч переходит через полночь, поэтому смотрим и вчерашний день.
/// Отпуск (OnLeave) или выходной (NotWorking) на сегодня перекрывает смену.
/// </summary>
public sealed class DutyService : IDutyService
{
    // Москва без перехода на летнее время с 2014 года; в образе alpine нет tzdata, поэтому смещение — константа.
    private static readonly TimeSpan Msk = TimeSpan.FromHours(3);

    private readonly BffDbContext _context;

    public DutyService(BffDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<DutyRecipient>> OnDutyAsync(DateTimeOffset now, CancellationToken ct)
    {
        var local = now.ToOffset(Msk).DateTime;
        var today = DateOnly.FromDateTime(local);
        var yesterday = today.AddDays(-1);

        var entries = await _context.ScheduleEntries.AsNoTracking()
            .Where(s => s.DateFrom <= today && s.DateTo >= yesterday)
            .ToListAsync(ct);

        var off = entries
            .Where(s => s.Status != ScheduleStatus.Working && s.DateFrom <= today && s.DateTo >= today)
            .Select(s => s.UserId)
            .ToHashSet();

        var onShift = entries
            .Where(s => s.Status == ScheduleStatus.Working && !off.Contains(s.UserId) && IsOnShift(s, local, today))
            .Select(s => s.UserId)
            .Distinct()
            .ToList();

        if (onShift.Count == 0)
        {
            return Array.Empty<DutyRecipient>();
        }

        var users = await _context.Users.AsNoTracking()
            .Where(u => u.IsActive && onShift.Contains(u.Id))
            .Select(u => new { u.Id, u.Email, u.Telegram })
            .ToListAsync(ct);

        return users
            .Select(u => new DutyRecipient(u.Id, Clean(u.Email), Clean(u.Telegram)))
            .Where(r => r.Email is not null || r.Telegram is not null)
            .ToList();
    }

    public static bool IsOnShift(ScheduleEntry entry, DateTime local, DateOnly today)
    {
        foreach (var day in new[] { today.AddDays(-1), today })
        {
            if (day < entry.DateFrom || day > entry.DateTo)
            {
                continue;
            }

            if (entry.ShiftStart is not { } start)
            {
                if (day == today)
                {
                    return true;
                }

                continue;
            }

            var from = day.ToDateTime(start);
            var to = from.AddHours(entry.ShiftHours ?? 24);
            if (local >= from && local < to)
            {
                return true;
            }
        }

        return false;
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
