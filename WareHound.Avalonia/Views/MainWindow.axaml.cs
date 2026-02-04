using System;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace WareHound.Avalonia.Views;

public partial class MainWindow : Window
{
    public static FuncValueConverter<bool, double> SidebarWidthConverter { get; } =
        new(isExpanded => isExpanded ? 200 : 60);

    public static FuncValueConverter<bool, Color> StatusColorConverter { get; } =
        new(isCapturing => isCapturing ? Color.Parse("#34C759") : Color.Parse("#8E8E93"));

    public MainWindow()
    {
        InitializeComponent();
    }

    private void TitleBar_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            BeginMoveDrag(e);
        }
    }

    private void MinimizeWindow(object? sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void MaximizeWindow(object? sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }

    private void CloseWindow(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
