using Microsoft.UI;
using Microsoft.UI.Input;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using System.Runtime.InteropServices;
using Windows.Foundation;
using Windows.Graphics;
using IPChecker.Models;

namespace IPChecker.Helpers;

public static class WindowHelper
{
    private const int DetailWidthDip = 440;
    private const int CompactWindowWidthDip = 400;
    private const int MiniWidthDip = 440;
    private const int MiniContentHeightDip = 54;
    private const int DetailTopDragBarHeightDip = 36;
    private const int DetailHeaderHeightDip = 64;
    private const int AdapterRowHeightDip = 112;
    private const int DetailFooterHeightDip = 20;
    private const int VerticalPaddingDip = 56;
    private const int MinDetailHeightDip = 280;
    private const int SettingsWidthDip = CompactWindowWidthDip;
    private const int SettingsHeightDip = 404;
    private const int ControllerTestWidthDip = CompactWindowWidthDip;
    private const int ControllerTestHeightDip = 220;
    private const int AnchorMarginDip = 16;

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(nint hwnd);

    public static double GetDpiScale(Window window)
    {
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
        return GetDpiForWindow(hwnd) / 96.0;
    }

    public static void ApplyStartupSettings(Window window, DisplayMode displayMode, int adapterCount)
    {
        ResizeForDisplayMode(window, displayMode, adapterCount);
        ApplyWindowPosition(window);
        SetAlwaysOnTop(window, SettingsHelper.IsAlwaysOnTop);
    }

    public static void ApplyWindowPosition(Window window)
    {
        var anchor = SettingsHelper.WindowAnchor;

        if (anchor == WindowAnchor.Custom)
        {
            var savedPosition = SettingsHelper.GetWindowPosition();

            if (savedPosition is { } position)
            {
                MoveWindowDip(window, position.X, position.Y);
            }
            else
            {
                ApplyWindowAnchor(window, WindowAnchor.TopRight);
            }

            return;
        }

        ApplyWindowAnchor(window, anchor);
    }

    public static void ApplyWindowAnchor(Window window, WindowAnchor anchor)
    {
        if (anchor == WindowAnchor.Custom)
        {
            ApplyWindowPosition(window);
            return;
        }

        var scale = GetDpiScale(window);
        var margin = (int)(AnchorMarginDip * scale);
        var displayArea = DisplayArea.GetFromWindowId(
            window.AppWindow.Id,
            DisplayAreaFallback.Primary);

        var workArea = displayArea.WorkArea;
        var size = window.AppWindow.Size;

        var x = anchor switch
        {
            WindowAnchor.TopLeft or WindowAnchor.BottomLeft => workArea.X + margin,
            WindowAnchor.TopRight or WindowAnchor.BottomRight => workArea.X + workArea.Width - size.Width - margin,
            _ => workArea.X + workArea.Width - size.Width - margin
        };

        var y = anchor switch
        {
            WindowAnchor.TopLeft or WindowAnchor.TopRight => workArea.Y + margin,
            WindowAnchor.BottomLeft or WindowAnchor.BottomRight => workArea.Y + workArea.Height - size.Height - margin,
            _ => workArea.Y + margin
        };

        window.AppWindow.Move(new PointInt32(x, y));
        SettingsHelper.SaveWindowPosition(
            (int)(x / scale),
            (int)(y / scale));
    }

    public static void ResizeForDisplayMode(Window window, DisplayMode displayMode, int adapterCount)
    {
        var scale = GetDpiScale(window);
        int widthDip;
        int heightDip;

        if (displayMode == DisplayMode.Mini)
        {
            widthDip = MiniWidthDip;
            heightDip = MiniContentHeightDip + 10;
        }
        else
        {
            widthDip = DetailWidthDip;
            var rowCount = Math.Max(adapterCount, 1);
            heightDip = DetailTopDragBarHeightDip
                + DetailHeaderHeightDip
                + (rowCount * AdapterRowHeightDip)
                + DetailFooterHeightDip
                + VerticalPaddingDip;
            heightDip = Math.Max(heightDip, MinDetailHeightDip);
        }

        window.AppWindow.Resize(new SizeInt32(
            (int)(widthDip * scale),
            (int)(heightDip * scale)));

        if (SettingsHelper.WindowAnchor != WindowAnchor.Custom)
        {
            ApplyWindowAnchor(window, SettingsHelper.WindowAnchor);
        }
    }

    public static void EnterSettingsMode(Window window)
    {
        var scale = GetDpiScale(window);
        window.AppWindow.Resize(new SizeInt32(
            (int)(SettingsWidthDip * scale),
            (int)(SettingsHeightDip * scale)));

        if (SettingsHelper.WindowAnchor != WindowAnchor.Custom)
        {
            ApplyWindowAnchor(window, SettingsHelper.WindowAnchor);
        }
    }

    public static void ExitSettingsMode(Window window, DisplayMode displayMode, int adapterCount)
    {
        ResizeForDisplayMode(window, displayMode, adapterCount);
    }

    public static void EnterControllerTestMode(Window window)
    {
        var scale = GetDpiScale(window);
        window.AppWindow.Resize(new SizeInt32(
            (int)(ControllerTestWidthDip * scale),
            (int)(ControllerTestHeightDip * scale)));

        if (SettingsHelper.WindowAnchor != WindowAnchor.Custom)
        {
            ApplyWindowAnchor(window, SettingsHelper.WindowAnchor);
        }
    }

    public static void ExitControllerTestMode(Window window, DisplayMode displayMode, int adapterCount)
    {
        ResizeForDisplayMode(window, displayMode, adapterCount);
    }

    public static void SetAlwaysOnTop(Window window, bool isAlwaysOnTop)
    {
        if (window.AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsAlwaysOnTop = isAlwaysOnTop;
        }
    }

    public static void SaveWindowPosition(Window window)
    {
        var scale = GetDpiScale(window);
        var position = window.AppWindow.Position;
        SettingsHelper.SetCustomWindowPosition(
            (int)(position.X / scale),
            (int)(position.Y / scale));
    }

    public static void HideToTray(Window window)
    {
        SaveWindowPosition(window);
        App.NetworkMonitor.SetWindowVisible(false);
        window.AppWindow.Hide();
    }

    public static void ShowFromTray(Window window)
    {
        App.NetworkMonitor.SetWindowVisible(true);
        window.AppWindow.Show();
        window.Activate();
    }

    public static bool IsVisible(Window window) => window.AppWindow.IsVisible;

    public static bool SetDragRegions(Window window, params FrameworkElement[] dragTargets)
    {
        if (window.Content is not UIElement root)
        {
            return false;
        }

        var scale = dragTargets.FirstOrDefault()?.XamlRoot?.RasterizationScale ?? GetDpiScale(window);
        var captionRects = new List<RectInt32>();

        foreach (var target in dragTargets)
        {
            if (target.Visibility != Visibility.Visible || target.ActualWidth <= 0 || target.ActualHeight <= 0)
            {
                continue;
            }

            var bounds = target.TransformToVisual(root).TransformBounds(
                new Rect(0, 0, target.ActualWidth, target.ActualHeight));

            captionRects.Add(new RectInt32(
                (int)Math.Round(bounds.X * scale),
                (int)Math.Round(bounds.Y * scale),
                Math.Max(1, (int)Math.Round(bounds.Width * scale)),
                Math.Max(1, (int)Math.Round(bounds.Height * scale))));
        }

        var nonClient = InputNonClientPointerSource.GetForWindowId(window.AppWindow.Id);
        nonClient.ClearRegionRects(NonClientRegionKind.Caption);

        if (captionRects.Count > 0)
        {
            nonClient.SetRegionRects(NonClientRegionKind.Caption, captionRects.ToArray());
            return true;
        }

        return false;
    }

    private static void MoveWindowDip(Window window, int xDip, int yDip)
    {
        var scale = GetDpiScale(window);
        window.AppWindow.Move(new PointInt32(
            (int)(xDip * scale),
            (int)(yDip * scale)));
    }
}
