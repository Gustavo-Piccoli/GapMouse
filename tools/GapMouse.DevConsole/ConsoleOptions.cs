using System.Globalization;
using GapMouse.Domain.Settings;

namespace GapMouse.DevConsole;

internal static class ConsoleOptions
{
    /// <summary>
    /// Parses the command line over <paramref name="baseline"/>. Malformed or unknown arguments are reported in
    /// <paramref name="usageErrors"/>; range errors come back through the validation result.
    /// </summary>
    public static ValidationResult<EngineSettings> Parse(string[] args, EngineSettings baseline, out IReadOnlyList<string> usageErrors)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(baseline);

        EngineSettings d = baseline;
        int idle = (int)d.IdleThreshold.TotalSeconds;
        int check = (int)d.CheckInterval.TotalSeconds;
        int min = (int)d.MovementIntervalMin.TotalSeconds;
        int max = (int)d.MovementIntervalMax.TotalSeconds;
        int amplitude = d.AmplitudePixels;
        bool display = d.KeepDisplayOn;
        List<string> problems = [];

        for (int i = 0; i < args.Length; i++)
        {
            string flag = args[i];
            if (flag is not ("--idle" or "--check" or "--min" or "--max" or "--amplitude" or "--display"))
            {
                problems.Add($"Unknown argument '{flag}'.");
                continue;
            }

            if (i + 1 >= args.Length)
            {
                problems.Add($"Missing value for {flag}.");
                break;
            }

            string raw = args[++i];
            if (flag == "--display")
            {
                switch (raw)
                {
                    case "on":
                        display = true;
                        break;
                    case "off":
                        display = false;
                        break;
                    default:
                        problems.Add($"Invalid value '{raw}' for --display (expected on or off).");
                        break;
                }

                continue;
            }

            if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
            {
                problems.Add($"Invalid value '{raw}' for {flag} (expected an integer).");
                continue;
            }

            switch (flag)
            {
                case "--idle":
                    idle = value;
                    break;
                case "--check":
                    check = value;
                    break;
                case "--min":
                    min = value;
                    break;
                case "--max":
                    max = value;
                    break;
                default:
                    amplitude = value;
                    break;
            }
        }

        usageErrors = problems;
        return EngineSettings.Create(idle, check, min, max, amplitude, display);
    }
}
