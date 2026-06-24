using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using IPChecker.Helpers;
using IPChecker.Models;
using IPChecker.ViewModels;

namespace IPChecker;

public sealed partial class MainPage : Page
{
    private bool _isInitialized;
    private bool _isSyncingAnchor;

    public MainViewModel ViewModel { get; }

    public MainPage()
    {
        ViewModel = App.MainViewModel;
        InitializeComponent();
        Loaded += OnLoaded;
        SizeChanged += OnPageSizeChanged;
    }

    public void ShowSettingsFlyout()
    {
        if (ViewModel.IsSettingsOpen)
        {
            return;
        }

        CloseControllerTest();
        SyncWindowAnchorSelection();
        ViewModel.IsSettingsOpen = true;
        WindowHelper.EnterSettingsMode(App.Window);
        ScheduleDragRegionUpdate();
    }

    public void CloseSettings()
    {
        if (!ViewModel.IsSettingsOpen)
        {
            return;
        }

        ViewModel.IsSettingsOpen = false;
        WindowHelper.ExitSettingsMode(App.Window, ViewModel.DisplayMode, ViewModel.Adapters.Count);
        ScheduleDragRegionUpdate();
    }

    public void ShowControllerTest()
    {
        if (ViewModel.IsControllerTestOpen)
        {
            return;
        }

        CloseSettings();

        if (!WindowHelper.IsVisible(App.Window))
        {
            WindowHelper.ShowFromTray(App.Window);
        }

        ViewModel.IsControllerTestOpen = true;
        WindowHelper.EnterControllerTestMode(App.Window);
        ViewModel.ControllerTest.StartMonitoring();
        ScheduleDragRegionUpdate();
    }

    public void CloseControllerTest()
    {
        if (!ViewModel.IsControllerTestOpen)
        {
            return;
        }

        ViewModel.ControllerTest.StopMonitoring();
        ViewModel.IsControllerTestOpen = false;
        WindowHelper.ExitControllerTestMode(App.Window, ViewModel.DisplayMode, ViewModel.Adapters.Count);
        ScheduleDragRegionUpdate();
    }

    private int _dragRegionRetryCount;

    public bool ApplyDragRegions()
    {
        bool applied;

        if (ViewModel.SettingsVisibility == Visibility.Visible)
        {
            applied = WindowHelper.SetDragRegions(App.Window, SettingsTopDragBar);
        }
        else if (ViewModel.ControllerTestVisibility == Visibility.Visible)
        {
            applied = WindowHelper.SetDragRegions(App.Window, ControllerTestTitleDragRegion);
        }
        else if (ViewModel.DetailModeVisibility == Visibility.Visible)
        {
            applied = WindowHelper.SetDragRegions(App.Window, DetailTopDragBar);
        }
        else if (ViewModel.MiniModeVisibility == Visibility.Visible)
        {
            applied = WindowHelper.SetDragRegions(App.Window, MiniDragRegion);
        }
        else
        {
            applied = WindowHelper.SetDragRegions(App.Window);
        }

        if (!applied && _dragRegionRetryCount < 8)
        {
            _dragRegionRetryCount++;
            ScheduleDragRegionUpdate();
        }
        else
        {
            _dragRegionRetryCount = 0;
        }

        return applied;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;

        if (_isInitialized)
        {
            return;
        }

        _isInitialized = true;

        try
        {
            await ViewModel.InitializeAsync();
            SyncWindowAnchorSelection();
            ScheduleDragRegionUpdate();
            App.WriteStartupLog("InitializeAsync completed.");
        }
        catch (Exception ex)
        {
            App.WriteStartupLog($"Initialize failed: {ex}");
            ViewModel.ClearStartingUpState();
        }
    }

    private void OnPageSizeChanged(object sender, SizeChangedEventArgs e)
    {
        ScheduleDragRegionUpdate();
    }

    private void ScheduleDragRegionUpdate()
    {
        _ = DispatcherQueue.TryEnqueue(() =>
        {
            _ = DispatcherQueue.TryEnqueue(() =>
            {
                if (App.Window is MainWindow mainWindow)
                {
                    mainWindow.UpdateDragRegions();
                }
                else
                {
                    ApplyDragRegions();
                }
            });
        });
    }

    private void SyncWindowAnchorSelection()
    {
        _isSyncingAnchor = true;
        try
        {
            WindowAnchorComboBox.SelectedItem = ViewModel.WindowAnchorOptions
                .FirstOrDefault(o => o.Value == ViewModel.SelectedWindowAnchor)
                ?? ViewModel.WindowAnchorOptions.FirstOrDefault();
        }
        finally
        {
            _isSyncingAnchor = false;
        }
    }

    private void WindowAnchorComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isSyncingAnchor
            || WindowAnchorComboBox.SelectedItem is not WindowAnchorOption option)
        {
            return;
        }

        ViewModel.SelectedWindowAnchor = option.Value;
    }

    private void CloseSettingsButton_Click(object sender, RoutedEventArgs e)
    {
        CloseSettings();
    }

    private void CloseControllerTestButton_Click(object sender, RoutedEventArgs e)
    {
        CloseControllerTest();
    }

    private void OpenNetworkSharingCenterButton_Click(object sender, RoutedEventArgs e)
    {
        if (!WindowsNetworkSettingsHelper.TryOpenNetworkAndSharingCenter())
        {
            _ = ShowMessageDialogAsync(
                "ネットワーク設定",
                "ネットワークと共有センターを開けませんでした。");
        }
    }

    private async Task ShowMessageDialogAsync(string title, string message)
    {
        if (!DispatcherQueue.HasThreadAccess)
        {
            var tcs = new TaskCompletionSource();
            DispatcherQueue.TryEnqueue(async () =>
            {
                try
                {
                    await ShowMessageDialogAsync(title, message).ConfigureAwait(true);
                    tcs.SetResult();
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            });

            await tcs.Task.ConfigureAwait(true);
            return;
        }

        var dialog = new ContentDialog
        {
            Title = title,
            Content = new TextBlock
            {
                Text = message,
                TextWrapping = TextWrapping.WrapWholeWords
            },
            CloseButtonText = "OK",
            XamlRoot = XamlRoot
        };

        await dialog.ShowAsync();
    }
}
