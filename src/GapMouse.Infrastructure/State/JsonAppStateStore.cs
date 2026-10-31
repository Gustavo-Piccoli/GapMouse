using System.Globalization;
using System.Text.Json;
using GapMouse.Application.State;
using GapMouse.Domain.Engine;
using GapMouse.Infrastructure.IO;

namespace GapMouse.Infrastructure.State;

public sealed class JsonAppStateStore(string stateFilePath) : IAppStateStore
{
    private const int SchemaVersion = 1;

    public AppState? Load()
    {
        try
        {
            if (!File.Exists(stateFilePath))
            {
                return null;
            }

            var today = JsonSerializer.Deserialize(
                File.ReadAllText(stateFilePath),
                StateJsonContext.Default.StateDocument)?.Today;

            if (today?.Day is null
                || today.MovingSeconds is null
                || today.MovingActivations is null
                || !DateOnly.TryParseExact(today.Day, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day)
                || double.IsNaN(today.MovingSeconds.Value)
                || today.MovingSeconds < 0
                || today.MovingSeconds > TimeSpan.MaxValue.TotalSeconds
                || today.MovingActivations < 0)
            {
                return null;
            }

            return new AppState(new DailyStats(
                day,
                TimeSpan.FromSeconds(today.MovingSeconds.Value),
                today.MovingActivations.Value));
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    public void Save(AppState state)
    {
        var document = new StateDocument
        {
            SchemaVersion = SchemaVersion,
            Today = new StateDocument.TodaySection
            {
                Day = state.Today.Day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                MovingSeconds = state.Today.MovingTime.TotalSeconds,
                MovingActivations = state.Today.MovingActivations,
            },
        };

        AtomicFile.WriteAllText(
            stateFilePath,
            JsonSerializer.Serialize(document, StateJsonContext.Default.StateDocument));
    }
}
