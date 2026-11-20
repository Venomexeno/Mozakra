using System.Text.Json;
using System.Text.Json.Serialization;
using Mozakra.Core.Models;

namespace Mozakra.Core.Persistence;

/// <summary>Shared JSON settings and parsing for <see cref="AppState"/> files (state, backups, exports).</summary>
public static class StateJson
{
    /// <summary>The newest schema version this build can read and write.</summary>
    public const int CurrentSchemaVersion = 1;

    public static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static string Serialize(AppState state) => JsonSerializer.Serialize(state, Options);

    /// <summary>
    /// Parses and normalizes a state document.
    /// Throws <see cref="JsonException"/> when the text isn't a valid state and
    /// <see cref="UnsupportedSchemaVersionException"/> when it was written by a newer version.
    /// </summary>
    public static AppState Deserialize(string json)
    {
        var state = JsonSerializer.Deserialize<AppState>(json, Options)
            ?? throw new JsonException("The file does not contain a state object.");

        if (state.SchemaVersion > CurrentSchemaVersion)
            throw new UnsupportedSchemaVersionException(state.SchemaVersion, CurrentSchemaVersion);
        if (state.SchemaVersion < 1)
            throw new JsonException($"Invalid schema version {state.SchemaVersion}.");

        return Normalize(state);
    }

    /// <summary>Replaces nulls left by hand-edited or partial files with empty defaults.</summary>
    public static AppState Normalize(AppState state)
    {
        state.Plans ??= new();
        state.Exceptions ??= new();
        state.Logs ??= new();
        state.Settings ??= new();
        state.Weekly ??= new();
        state.Weekly.MinutesByDay = Resize(state.Weekly.MinutesByDay);
        state.Weekly.ReminderByDay = Resize(state.Weekly.ReminderByDay);
        foreach (var plan in state.Plans)
            plan.Checklist ??= new();
        return state;
    }

    private static T[] Resize<T>(T[]? values)
    {
        if (values is { Length: 7 })
            return values;
        var result = new T[7];
        if (values is not null)
            Array.Copy(values, result, Math.Min(values.Length, 7));
        return result;
    }
}
