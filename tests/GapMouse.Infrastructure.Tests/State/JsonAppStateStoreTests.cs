using GapMouse.Application.State;
using GapMouse.Domain.Engine;
using GapMouse.Infrastructure.State;
using GapMouse.Testing.IO;

namespace GapMouse.Infrastructure.Tests.State;

public class JsonAppStateStoreTests
{
    [Fact]
    public void Load_MissingFile_ReturnsNull()
    {
        using var dir = new TempDirectory();

        new JsonAppStateStore(Path.Combine(dir.Path, "GapMouse.state.json")).Load().ShouldBeNull();
    }

    [Fact]
    public void SaveThenLoad_RoundTrips()
    {
        using var dir = new TempDirectory();
        var store = new JsonAppStateStore(Path.Combine(dir.Path, "GapMouse.state.json"));
        var state = new AppState(new DailyStats(new DateOnly(2026, 10, 31), TimeSpan.FromSeconds(1234.5), 3));

        store.Save(state);

        store.Load().ShouldBe(state);
    }

    [Fact]
    public void Load_Corrupt_ReturnsNull()
    {
        using var dir = new TempDirectory();
        var path = Path.Combine(dir.Path, "GapMouse.state.json");
        File.WriteAllText(path, "{ not json");

        new JsonAppStateStore(path).Load().ShouldBeNull();

        File.Exists(path + ".bad").ShouldBeFalse();
    }

    [Fact]
    public void Load_FileLocked_ReturnsNull()
    {
        using var dir = new TempDirectory();
        var path = Path.Combine(dir.Path, "GapMouse.state.json");
        File.WriteAllText(path, "{}");
        using var held = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None);

        new JsonAppStateStore(path).Load().ShouldBeNull();
    }
}
