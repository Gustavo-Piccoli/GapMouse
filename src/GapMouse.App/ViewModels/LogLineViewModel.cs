using GapMouse.Application.Logging;

namespace GapMouse.App.ViewModels;

public sealed record LogLineViewModel(string Time, string Tag, string Message, LogKind Kind);
