using System;
using System.Reactive;
using System.Reactive.Disposables;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using WareHound.Avalonia.Infrastructure.Events;

namespace WareHound.Avalonia.ViewModels;

public class SettingsViewModel : ViewModelBase
{
    [Reactive] public bool DarkModeEnabled { get; set; }
    [Reactive] public int MaxPacketBuffer { get; set; } = 10000;
    [Reactive] public bool AutoScroll { get; set; } = true;
    [Reactive] public bool ShowMacAddresses { get; set; } = true;
    [Reactive] public string CaptureFilter { get; set; } = "";
    [Reactive] public int SelectedTimeFormatIndex { get; set; }
    [Reactive] public int SelectedThemeIndex { get; set; }
    [Reactive] public int SelectedPcapBackendIndex { get; set; } = 0;
    [Reactive] public bool GrpcEnabled { get; set; }
    [Reactive] public string GrpcServerAddress { get; set; } = "https://localhost:5001";

    public string[] TimeFormats { get; } = { "Relative", "Absolute", "Delta" };
    public string[] Themes { get; } = { "Light", "Dark" };
    public string[] PcapBackends { get; } = { "Native (C++ pcap)", "SharpPcap (Managed)" };

    // Commands
    public ReactiveCommand<Unit, Unit> ResetCommand { get; }
    public ReactiveCommand<Unit, Unit> ApplyCommand { get; }

    public SettingsViewModel()
    {
        ResetCommand = ReactiveCommand.Create(ResetSettings);
        ApplyCommand = ReactiveCommand.Create(ApplySettings);

        // Auto-publish events when properties change
        this.WhenAnyValue(x => x.DarkModeEnabled)
            .Subscribe(isDark => Publish(new ThemeChangedEvent(isDark)))
            .DisposeWith(Disposables);

        this.WhenAnyValue(x => x.AutoScroll)
            .Subscribe(enabled => Publish(new AutoScrollChangedEvent(enabled)))
            .DisposeWith(Disposables);

        this.WhenAnyValue(x => x.ShowMacAddresses)
            .Subscribe(show => Publish(new ShowMacAddressesChangedEvent(show)))
            .DisposeWith(Disposables);

        this.WhenAnyValue(x => x.SelectedTimeFormatIndex)
            .Subscribe(index => Publish(new TimeFormatChangedEvent((TimeFormatType)index)))
            .DisposeWith(Disposables);

        this.WhenAnyValue(x => x.SelectedPcapBackendIndex)
            .Subscribe(index => Publish(new PcapBackendChangedEvent((PcapBackend)index)))
            .DisposeWith(Disposables);

        this.WhenAnyValue(x => x.GrpcEnabled, x => x.GrpcServerAddress)
            .Subscribe(tuple => Publish(new GrpcEnabledChangedEvent(new GrpcSettings(tuple.Item1, tuple.Item2))))
            .DisposeWith(Disposables);
    }

    private void ResetSettings()
    {
        DarkModeEnabled = false;
        MaxPacketBuffer = 10000;
        AutoScroll = true;
        ShowMacAddresses = true;
        CaptureFilter = "";
        SelectedTimeFormatIndex = 0;
        SelectedThemeIndex = 0;
        SelectedPcapBackendIndex = 0;
        GrpcEnabled = false;
        GrpcServerAddress = "https://localhost:5001";
    }

    private void ApplySettings()
    {
        // Settings are auto-published on change, but this can be used for explicit apply
        Publish(new ThemeChangedEvent(DarkModeEnabled));
        Publish(new AutoScrollChangedEvent(AutoScroll));
        Publish(new ShowMacAddressesChangedEvent(ShowMacAddresses));
        Publish(new TimeFormatChangedEvent((TimeFormatType)SelectedTimeFormatIndex));
        Publish(new PcapBackendChangedEvent((PcapBackend)SelectedPcapBackendIndex));
        Publish(new GrpcEnabledChangedEvent(new GrpcSettings(GrpcEnabled, GrpcServerAddress)));
    }
}
