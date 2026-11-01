using System.Buffers.Binary;
using GapMouse.Infrastructure.Shell;

namespace GapMouse.Infrastructure.Tests.Shell;

public sealed class IcoImageTests
{
    private static byte[] Ico => File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Shell", "tray-waiting.ico"));

    private static int PngWidth(byte[] png)
    {
        png[..8].ShouldBe(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A });
        return (int)BinaryPrimitives.ReadUInt32BigEndian(png.AsSpan(16, 4));
    }

    [Fact]
    public void SelectImage_16_ReturnsSixteenPixelEntry()
    {
        PngWidth(IcoImage.SelectImage(Ico, 16)).ShouldBe(16);
    }

    [Fact]
    public void SelectImage_100_ReturnsTwoFiftySix()
    {
        PngWidth(IcoImage.SelectImage(Ico, 100)).ShouldBe(256);
    }

    [Fact]
    public void SelectImage_BadHeader_Throws()
    {
        Should.Throw<InvalidDataException>(() => IcoImage.SelectImage([1, 2, 3, 4, 5, 6, 7], 16));
    }
}
