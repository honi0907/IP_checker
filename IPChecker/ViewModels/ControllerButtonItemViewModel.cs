using CommunityToolkit.Mvvm.ComponentModel;

namespace IPChecker.ViewModels;

public partial class ControllerButtonItemViewModel : ObservableObject
{
    private bool _isPressed;
    private bool _isAvailable = true;

    public int Number { get; init; }

    public bool IsAvailable
    {
        get => _isAvailable;
        set => SetProperty(ref _isAvailable, value);
    }

    public bool IsPressed
    {
        get => _isPressed;
        set => SetProperty(ref _isPressed, value);
    }
}
