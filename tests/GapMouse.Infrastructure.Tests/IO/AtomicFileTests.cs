using System.Text;
using GapMouse.Infrastructure.IO;
using GapMouse.Testing.IO;

namespace GapMouse.Infrastructure.Tests.IO;

public class AtomicFileTests
{
    [Fact]
    public void WriteAllText_CreatesMissingDirectory()
    {
        using var dir = new TempDirectory();
        var path = Path.Combine(dir.Path, "a", "b", "file.txt");

        AtomicFile.WriteAllText(path, "hello");

        File.ReadAllText(path).ShouldBe("hello");
    }

    [Fact]
    public void WriteAllText_ReplacesExistingContent()
    {
        using var dir = new TempDirectory();
        var path = Path.Combine(dir.Path, "file.txt");
        File.WriteAllText(path, "old content that is longer");

        AtomicFile.WriteAllText(path, "new");

        File.ReadAllText(path).ShouldBe("new");
    }

    [Fact]
    public void WriteAllText_StaleTmpFromCrash_IsOverwritten()
    {
        using var dir = new TempDirectory();
        var path = Path.Combine(dir.Path, "file.txt");
        File.WriteAllText(path + ".tmp", "garbage garbage garbage");

        AtomicFile.WriteAllText(path, "fresh");

        File.ReadAllText(path).ShouldBe("fresh");
        File.Exists(path + ".tmp").ShouldBeFalse();
    }

    [Fact]
    public void WriteAllText_WritesUtf8WithoutBom()
    {
        using var dir = new TempDirectory();
        var path = Path.Combine(dir.Path, "file.txt");

        AtomicFile.WriteAllText(path, "ação");

        var bytes = File.ReadAllBytes(path);
        bytes.Take(3).ToArray().ShouldNotBe([0xEF, 0xBB, 0xBF]);
        bytes.ShouldBe(new UTF8Encoding(false).GetBytes("ação"));
    }
}
