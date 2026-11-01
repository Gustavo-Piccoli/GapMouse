using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using GapMouse.App.ViewModels;
using GapMouse.App.Views;
using GapMouse.Application.Settings;
using GapMouse.Screenshots;

var outDir = Path.GetFullPath("artifacts/screenshots");
for (var i = 0; i < args.Length; i++)
{
    if (args[i] == "--out" && i + 1 < args.Length)
    {
        outDir = Path.GetFullPath(args[++i]);
    }
}

AppBuilder.Configure<GapMouse.App.App>()
    .UseSkia()
    .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
    .SetupWithoutStarting();

var failures = 0;
var written = 0;
foreach (var (theme, variant) in new[] { ("dark", ThemeVariant.Dark), ("light", ThemeVariant.Light) })
{
    Avalonia.Application.Current!.RequestedThemeVariant = variant;
    foreach (var (lang, language) in new[] { ("en", UiLanguage.English), ("pt", UiLanguage.Portuguese) })
    {
        var dir = Path.Combine(outDir, $"{theme}-{lang}");
        Directory.CreateDirectory(dir);
        foreach (var scenario in Scenarios.Names)
        {
            foreach (var (suffix, width, height) in new[] { ("", 760, 600), ("-min", 640, 520) })
            {
                var path = Path.Combine(dir, scenario + suffix + ".png");
                if (Capture(scenario, language, width, height, path))
                {
                    Console.WriteLine(path);
                    written++;
                }
                else
                {
                    Console.Error.WriteLine($"FAILED (null frame): {path}");
                    failures++;
                }
            }
        }
    }
}

Console.WriteLine($"{written} written, {failures} failed.");
return failures == 0 ? 0 : 1;

static bool Capture(string scenario, UiLanguage language, int width, int height, string path)
{
    var scene = Scenarios.Build(scenario, language);
    Window window = scenario == "custom-pause"
        ? new CustomPauseWindow { DataContext = new CustomPauseViewModel(scene.Localizer) { MinutesText = "abc" } } // invalid input, so the error line shows
        : new MainWindow { DataContext = scene.Main };

    if (window is MainWindow)
    {
        window.Width = width;
        window.Height = height;
    }
    // The custom-pause dialog keeps its real size (Width 340, SizeToContent height), so both size variants match.
    window.Show();
    if (window is MainWindow)
    {
        var tabs = window.GetVisualDescendants().OfType<TabControl>().Single();
        tabs.SelectedIndex = Scenarios.TabIndex(scenario);
    }

    Dispatcher.UIThread.RunJobs();
    // Animations follow wall-clock time, so let the face flip (220 ms) finish between tick batches.
    for (var i = 0; i < 5; i++)
    {
        AvaloniaHeadlessPlatform.ForceRenderTimerTick(6);
        Dispatcher.UIThread.RunJobs();
        Thread.Sleep(80);
    }

    AvaloniaHeadlessPlatform.ForceRenderTimerTick(2);
    Dispatcher.UIThread.RunJobs();
    var frame = window.CaptureRenderedFrame();
    var ok = frame is not null;
    if (frame is not null)
    {
        frame.Save(path, new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
        frame.Dispose();
    }

    window.Close();
    foreach (var d in scene.Disposables)
    {
        d.Dispose();
    }

    return ok;
}
