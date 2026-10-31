using System.Text;

namespace GapMouse.Infrastructure.IO;

/// <summary>Writes files so readers never observe a half-written file.</summary>
internal static class AtomicFile
{
    private static readonly UTF8Encoding Utf8NoBom = new(false);

    public static void WriteAllText(string path, string contents)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tmp = path + ".tmp";
        using (var stream = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            var bytes = Utf8NoBom.GetBytes(contents);
            stream.Write(bytes, 0, bytes.Length);
            stream.Flush(flushToDisk: true);
        }

        File.Move(tmp, path, overwrite: true);
    }
}
