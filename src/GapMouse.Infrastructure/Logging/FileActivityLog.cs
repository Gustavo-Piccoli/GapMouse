using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using GapMouse.Application.Logging;

namespace GapMouse.Infrastructure.Logging;

/// <summary>Best-effort daily log files (<c>gapmouse-yyyy-MM-dd.log</c>) with retention.</summary>
public sealed partial class FileActivityLog(string logsDirectory, TimeProvider time) : IActivityLog
{
    private static readonly UTF8Encoding Utf8NoBom = new(false);

    private readonly object _gate = new();
    private bool _enabled = true;
    private int _retentionDays = 7;
    private DateOnly? _lastRetentionDay;

    public void Configure(bool enabled, int retentionDays)
    {
        lock (_gate)
        {
            _enabled = enabled;
            _retentionDays = retentionDays;
            _lastRetentionDay = null;
            if (_enabled)
            {
                ApplyRetention();
            }
        }
    }

    public void Append(LogEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        lock (_gate)
        {
            if (!_enabled)
            {
                return;
            }

            try
            {
                Directory.CreateDirectory(logsDirectory);
                if (_lastRetentionDay != DateOnly.FromDateTime(time.GetLocalNow().DateTime))
                {
                    ApplyRetention();
                }

                var name = $"gapmouse-{entry.At.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}.log";
                File.AppendAllText(Path.Combine(logsDirectory, name), entry.ToLine() + Environment.NewLine, Utf8NoBom);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    [GeneratedRegex(@"^gapmouse-(\d{4}-\d{2}-\d{2})\.log$")]
    private static partial Regex LogFileName();

    private void ApplyRetention()
    {
        var today = DateOnly.FromDateTime(time.GetLocalNow().DateTime);
        _lastRetentionDay = today;
        var cutoff = today.AddDays(-_retentionDays);
        try
        {
            if (!Directory.Exists(logsDirectory))
            {
                return;
            }

            foreach (var file in Directory.EnumerateFiles(logsDirectory))
            {
                var match = LogFileName().Match(Path.GetFileName(file));
                if (match.Success
                    && DateOnly.TryParseExact(match.Groups[1].Value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
                    && date < cutoff)
                {
                    File.Delete(file);
                }
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
