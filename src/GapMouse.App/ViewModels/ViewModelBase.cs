using CommunityToolkit.Mvvm.ComponentModel;
using GapMouse.App.Localization;

namespace GapMouse.App.ViewModels;

public abstract class ViewModelBase(Localizer l) : ObservableObject
{
    public Localizer L { get; } = l;
}
