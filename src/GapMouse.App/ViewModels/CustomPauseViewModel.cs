using System.Globalization;
using GapMouse.App.Localization;

namespace GapMouse.App.ViewModels;

public sealed class CustomPauseViewModel(Localizer l) : ViewModelBase(l)
{
    private string _minutesText = "30";

    public string MinutesText
    {
        get => _minutesText;
        set
        {
            if (SetProperty(ref _minutesText, value ?? string.Empty))
            {
                OnPropertyChanged(nameof(Minutes));
                OnPropertyChanged(nameof(Error));
                OnPropertyChanged(nameof(CanConfirm));
            }
        }
    }

    /// <summary>Gets the parsed whole minutes from 1 to 1440, or null when the text is invalid.</summary>
    public int? Minutes =>
        int.TryParse(_minutesText.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var minutes)
        && minutes is >= 1 and <= 1440
            ? minutes
            : null;

    public string? Error => Minutes is null ? L.CustomPauseInvalid : null;

    public bool CanConfirm => Minutes is not null;
}
