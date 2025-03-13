using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

using BackgroundMuter.ViewModels;
using BackgroundMuter.Views;
using Splat;
using System;
using System.Diagnostics;

namespace BackgroundMuter;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.ShutdownRequested += This_ShutdownRequested;
            desktop.MainWindow = new MainWindow()
            {
                DataContext = Locator.Current.GetService<MainViewModel>(),
            };

            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainViewModel()
            };
        }
        else if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform)
        {
            singleViewPlatform.MainView = new MainView
            {
                DataContext = new MainViewModel()
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void This_ShutdownRequested(object? sender, ShutdownRequestedEventArgs e)
    {
        ShutdownRequested?.Invoke(this, e);
    }

    public event EventHandler<ShutdownRequestedEventArgs>? ShutdownRequested;
}
