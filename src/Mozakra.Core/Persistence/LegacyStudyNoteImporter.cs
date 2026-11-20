using System.Globalization;
using System.Text.Json;
using Mozakra.Core.Models;

namespace Mozakra.Core.Persistence;

/// <summary>One-time conversion of the owner's old Study Note <c>data.json</c> into an <see cref="AppState"/>.</summary>
public sealed class LegacyStudyNoteImporter
{
    public const string LinqPlanName = "كورس LINQ";
    public const string EfCorePlanName = "كورس Entity Framework Core";
    public const string SkipLabel = "سفر";

    // Sunday..Saturday, matching (int)DayOfWeek.
    private static readonly int[] WeeklyMinutes = { 45, 45, 120, 45, 120, 180, 0 };

    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    private readonly IClock _clock;

    public LegacyStudyNoteImporter(IClock? clock = null) => _clock = clock ?? new SystemClock();

    /// <summary>Reads and converts a Study Note file. Never modifies the source file.</summary>
    public AppState ImportFile(string path) => Convert(File.ReadAllText(path));

    /// <summary>Converts Study Note JSON. Throws <see cref="InvalidDataException"/> on unreadable input.</summary>
    public AppState Convert(string json)
    {
        LegacyStudyNoteData data;
        try
        {
            data = JsonSerializer.Deserialize<LegacyStudyNoteData>(json, Options)
                ?? throw new InvalidDataException("The Study Note file is empty.");
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("The Study Note file is not valid JSON.", ex);
        }

        var now = _clock.UtcNow;
        var start = data.Start is null ? _clock.Today : ParseDate(data.Start, "Start");
        var units = data.Units ?? new();

        var linq = new StudyPlan
        {
            Name = LinqPlanName,
            Kind = PlanKind.Course,
            StartDate = start,
            TotalVideos = 61,
            TotalVideoMinutes = 640,
            VideosDone = Math.Clamp(units.GetValueOrDefault("linq"), 0, 61),
            CreatedAtUtc = now,
        };
        var efCore = new StudyPlan
        {
            Name = EfCorePlanName,
            Kind = PlanKind.Course,
            StartDate = start,
            StartsAfterPlanId = linq.Id,
            TotalVideos = 70,
            TotalVideoMinutes = 456,
            VideosDone = Math.Clamp(units.GetValueOrDefault("efcore"), 0, 70),
            CreatedAtUtc = now,
        };

        var state = new AppState
        {
            Plans = { linq, efCore },
            Weekly = new WeeklyAvailability { MinutesByDay = (int[])WeeklyMinutes.Clone() },
            Settings = new AppSettings
            {
                Language = "ar",
                OnboardingDone = true,
                NoteLeft = data.Left,
                NoteTop = data.Top,
                ShowDetails = data.ShowDetails,
            },
        };

        foreach (var (key, minutes) in (data.Logs ?? new()).OrderBy(kv => kv.Key, StringComparer.Ordinal))
        {
            state.Logs.Add(new LogEntry
            {
                PlanId = linq.Id,
                Date = ParseDate(key, "Logs"),
                Minutes = minutes,
                CreatedAtUtc = now,
            });
        }

        foreach (var date in (data.Skips ?? new()).Distinct().Order(StringComparer.Ordinal))
        {
            var day = ParseDate(date, "Skips");
            state.Exceptions.Add(new AvailabilityException { From = day, To = day, Minutes = 0, Label = SkipLabel });
        }

        if (data.Timer is { } timer)
            state.Timer = ConvertTimer(timer, linq.Id, now);

        return state;
    }

    private static ActiveTimer? ConvertTimer(LegacyTimerState timer, Guid planId, DateTime now)
    {
        var ms = timer.AccumulatedMs;
        if (timer.Running && timer.StartedAtUtc != default && now > timer.StartedAtUtc)
            ms += (now - timer.StartedAtUtc).TotalMilliseconds;   // fold a running session into the paused total
        if (ms <= 0)
            return null;

        return new ActiveTimer { PlanId = planId, StartedAtUtc = timer.StartedAtUtc, AccumulatedMs = ms, Running = false };
    }

    private static DateOnly ParseDate(string value, string field) =>
        DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? date
            : throw new InvalidDataException($"Invalid date '{value}' in Study Note field '{field}'.");
}
