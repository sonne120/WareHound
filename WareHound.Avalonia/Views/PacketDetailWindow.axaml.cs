using System;
using System.Collections.Generic;
using System.Text;
using Avalonia.Controls;
using Avalonia.Interactivity;
using WareHound.Avalonia.Models;

namespace WareHound.Avalonia.Views;

public partial class PacketDetailWindow : Window
{
    public PacketDetailWindow()
    {
        InitializeComponent();
    }

    public PacketDetailWindow(PacketInfo packet) : this()
    {
        LoadPacketData(packet);
    }

    private void LoadPacketData(PacketInfo packet)
    {
        HeaderProtocol.Text = packet.Protocol;
        HeaderTime.Text = packet.TimeDisplay;
        HeaderPacketNo.Text = $"Packet #{packet.Number}";

        SourceIp.Text = packet.SourceIp;
        DestIp.Text = packet.DestIp;
        SourcePort.Text = packet.SourcePort.ToString();
        DestPort.Text = packet.DestPort.ToString();

        SourceMac.Text = packet.SourceMac;
        DestMac.Text = packet.DestMac;

        Protocol.Text = packet.Protocol;
        PacketId.Text = packet.Id.ToString();
        HostName.Text = string.IsNullOrEmpty(packet.HostName) ? "Unknown" : packet.HostName;
        CaptureTime.Text = packet.CaptureTime.ToString("yyyy-MM-dd HH:mm:ss.fff");

        HexDump.Text = GenerateHexDump(packet);
    }

    private string GenerateHexDump(PacketInfo packet)
    {
        var sb = new StringBuilder();

        byte[] bytes;

        if (packet.RawData != null && packet.RawData.Length > 0)
        {
            bytes = packet.RawData;
        }
        else
        {
            var packetBytes = new List<byte>();

            packetBytes.AddRange(ParseMacAddress(packet.DestMac));
            packetBytes.AddRange(ParseMacAddress(packet.SourceMac));
            packetBytes.Add(0x08); packetBytes.Add(0x00);

            packetBytes.Add(0x45);
            packetBytes.Add(0x00);
            packetBytes.Add(0x00); packetBytes.Add(0x28);
            packetBytes.Add((byte)(packet.Id >> 8)); packetBytes.Add((byte)(packet.Id & 0xFF));
            packetBytes.Add(0x40); packetBytes.Add(0x00);
            packetBytes.Add(0x40);
            packetBytes.Add(GetProtocolNumber(packet.Protocol));
            packetBytes.Add(0x00); packetBytes.Add(0x00);
            packetBytes.AddRange(ParseIpAddress(packet.SourceIp));
            packetBytes.AddRange(ParseIpAddress(packet.DestIp));

            packetBytes.Add((byte)(packet.SourcePort >> 8)); packetBytes.Add((byte)(packet.SourcePort & 0xFF));
            packetBytes.Add((byte)(packet.DestPort >> 8)); packetBytes.Add((byte)(packet.DestPort & 0xFF));

            if (packet.Protocol.Equals("TCP", StringComparison.OrdinalIgnoreCase))
            {
                packetBytes.Add(0x00); packetBytes.Add(0x00); packetBytes.Add(0x00); packetBytes.Add(0x01);
                packetBytes.Add(0x00); packetBytes.Add(0x00); packetBytes.Add(0x00); packetBytes.Add(0x00);
                packetBytes.Add(0x50);
                packetBytes.Add(0x02);
                packetBytes.Add(0xFF); packetBytes.Add(0xFF);
                packetBytes.Add(0x00); packetBytes.Add(0x00);
                packetBytes.Add(0x00); packetBytes.Add(0x00);
            }
            else
            {
                packetBytes.Add(0x00); packetBytes.Add(0x08);
                packetBytes.Add(0x00); packetBytes.Add(0x00);
            }

            bytes = packetBytes.ToArray();
        }

        for (int i = 0; i < bytes.Length; i += 16)
        {
            sb.Append($"{i:X8}  ");

            for (int j = 0; j < 16; j++)
            {
                if (i + j < bytes.Length)
                    sb.Append($"{bytes[i + j]:X2} ");
                else
                    sb.Append("   ");

                if (j == 7) sb.Append(" ");
            }

            sb.Append(" ");

            for (int j = 0; j < 16 && i + j < bytes.Length; j++)
            {
                byte b = bytes[i + j];
                sb.Append(b >= 32 && b < 127 ? (char)b : '.');
            }

            sb.AppendLine();
        }

        return sb.ToString();
    }

    private byte[] ParseMacAddress(string mac)
    {
        var result = new byte[6];
        if (string.IsNullOrEmpty(mac)) return result;

        try
        {
            var parts = mac.Split(':');
            for (int i = 0; i < Math.Min(6, parts.Length); i++)
            {
                result[i] = Convert.ToByte(parts[i], 16);
            }
        }
        catch { }
        return result;
    }

    private byte[] ParseIpAddress(string ip)
    {
        var result = new byte[4];
        if (string.IsNullOrEmpty(ip)) return result;

        try
        {
            var parts = ip.Split('.');
            for (int i = 0; i < Math.Min(4, parts.Length); i++)
            {
                result[i] = byte.Parse(parts[i]);
            }
        }
        catch { }
        return result;
    }

    private byte GetProtocolNumber(string protocol)
    {
        return protocol?.ToUpperInvariant() switch
        {
            "TCP" => 0x06,
            "UDP" => 0x11,
            "ICMP" => 0x01,
            "IGMP" => 0x02,
            _ => 0x00
        };
    }

    private void CloseButton_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
