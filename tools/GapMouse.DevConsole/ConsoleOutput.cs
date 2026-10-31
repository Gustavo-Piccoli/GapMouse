namespace GapMouse.DevConsole;

/// <summary>Serializes colored writes because events arrive on the engine thread while keys are handled on the main one.</summary>
internal static class ConsoleOutput
{
    private static readonly Lock Gate = new();

    public static void WriteLine(ConsoleColor color, string text)
    {
        lock (Gate)
        {
            Console.ForegroundColor = color;
            Console.WriteLine(text);
            Console.ResetColor();
        }
    }
}
