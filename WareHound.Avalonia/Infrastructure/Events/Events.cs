using WareHound.Avalonia.Models;

namespace WareHound.Avalonia.Infrastructure.Events;

// Capture Events
public record CaptureStateChangedEvent(bool IsCapturing);
public record PacketCapturedEvent(PacketInfo Packet);
public record ClearPacketsEvent;

// Filter Events
public enum FilterType
{
    All,
    Protocol,
    SourceIP,
    DestIP,
    SourcePort,
    DestPort
}

public record FilterCriteria(FilterType Type = FilterType.All, string Value = "")
{
    public static FilterCriteria Empty => new(FilterType.All, "");

    /// <summary>Short label for UI such as filter chips.</summary>
    public string Display => Type switch
    {
        FilterType.Protocol => $"Protocol: {Value}",
        FilterType.SourceIP => $"Src IP: {Value}",
        FilterType.DestIP => $"Dst IP: {Value}",
        FilterType.SourcePort => $"Src port: {Value}",
        FilterType.DestPort => $"Dst port: {Value}",
        _ => Value
    };
}

/// <summary>The live (typed) filter changed.</summary>
public record FilterChangedEvent(FilterCriteria Criteria);

/// <summary>The set of pinned filters (chips) changed; all of them must match.</summary>
public record ActiveFiltersChangedEvent(IReadOnlyList<FilterCriteria> Filters);

/// <summary>A view asks for a filter to be pinned, e.g. from a packet context menu.</summary>
public record AddFilterRequestedEvent(FilterCriteria Criteria);

// Device Events
public record DevicesLoadingEvent(bool IsLoading);
public record DevicesLoadedEvent;
public record DevicesLoadFailedEvent(string ErrorMessage);

// UI Settings Events
public record AutoScrollChangedEvent(bool IsEnabled);
public record ShowMacAddressesChangedEvent(bool ShowMacAddresses);
public record ThemeChangedEvent(bool IsDarkTheme);

public enum TimeFormatType
{
    Relative,
    Absolute,
    Delta
}

public record TimeFormatChangedEvent(TimeFormatType Format);

// Pcap Events
public record PcapLoadedEvent(IList<PacketInfo> Packets);
public record PcapSaveRequestEvent;
public record PcapSaveResponseEvent(IList<PacketInfo> Packets);

public enum PcapBackend
{
    Native,
    SharpPcap
}

public record PcapBackendChangedEvent(PcapBackend Backend);

// Grpc Events
public record GrpcSettings(bool Enabled, string ServerAddress = "https://localhost:5001");
public record GrpcEnabledChangedEvent(GrpcSettings Settings);
