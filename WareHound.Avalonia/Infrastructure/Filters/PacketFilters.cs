using System;
using WareHound.Avalonia.Infrastructure.Events;
using WareHound.Avalonia.Models;

namespace WareHound.Avalonia.Infrastructure.Filters;

public interface IPacketFilter
{
    bool IsMatch(PacketInfo packet);
}

public class NoOpFilter : IPacketFilter
{
    public bool IsMatch(PacketInfo packet) => true;
}

public class ProtocolFilter : IPacketFilter
{
    private readonly string _protocol;

    public ProtocolFilter(string protocol)
    {
        _protocol = protocol?.Trim().ToLowerInvariant() ?? string.Empty;
    }

    public bool IsMatch(PacketInfo packet)
    {
        return !string.IsNullOrEmpty(packet.Protocol) &&
               packet.Protocol.ToLowerInvariant().Contains(_protocol);
    }
}

public class SourceIpFilter : IPacketFilter
{
    private readonly string _ip;

    public SourceIpFilter(string ip)
    {
        _ip = ip?.Trim() ?? string.Empty;
    }

    public bool IsMatch(PacketInfo packet)
    {
        return !string.IsNullOrEmpty(packet.SourceIp) &&
               packet.SourceIp.Contains(_ip);
    }
}

public class DestIpFilter : IPacketFilter
{
    private readonly string _ip;

    public DestIpFilter(string ip)
    {
        _ip = ip?.Trim() ?? string.Empty;
    }

    public bool IsMatch(PacketInfo packet)
    {
        return !string.IsNullOrEmpty(packet.DestIp) &&
               packet.DestIp.Contains(_ip);
    }
}

public class SourcePortFilter : IPacketFilter
{
    private readonly int _port;
    private readonly bool _isValid;

    public SourcePortFilter(string port)
    {
        _isValid = int.TryParse(port, out _port);
    }

    public bool IsMatch(PacketInfo packet)
    {
        if (!_isValid) return true;
        return packet.SourcePort == _port;
    }
}

public class DestPortFilter : IPacketFilter
{
    private readonly int _port;
    private readonly bool _isValid;

    public DestPortFilter(string port)
    {
        _isValid = int.TryParse(port, out _port);
    }

    public bool IsMatch(PacketInfo packet)
    {
        if (!_isValid) return true;
        return packet.DestPort == _port;
    }
}

public class AllFieldsFilter : IPacketFilter
{
    private readonly string _value;

    public AllFieldsFilter(string value)
    {
        _value = value?.Trim().ToLowerInvariant() ?? string.Empty;
    }

    public bool IsMatch(PacketInfo packet)
    {
        if (string.IsNullOrEmpty(_value)) return true;

        return (packet.Protocol?.ToLowerInvariant().Contains(_value) ?? false) ||
               (packet.SourceIp?.Contains(_value) ?? false) ||
               (packet.DestIp?.Contains(_value) ?? false) ||
               packet.SourcePort.ToString().Contains(_value) ||
               packet.DestPort.ToString().Contains(_value);
    }
}

public static class FilterFactory
{
    public static IPacketFilter Create(FilterType type, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return new NoOpFilter();
        }

        return type switch
        {
            FilterType.Protocol => new ProtocolFilter(value),
            FilterType.SourceIP => new SourceIpFilter(value),
            FilterType.DestIP => new DestIpFilter(value),
            FilterType.SourcePort => new SourcePortFilter(value),
            FilterType.DestPort => new DestPortFilter(value),
            FilterType.All => new AllFieldsFilter(value),
            _ => new NoOpFilter()
        };
    }
}
