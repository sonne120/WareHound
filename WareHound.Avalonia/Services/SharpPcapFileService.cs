using PacketDotNet;
using SharpPcap;
using SharpPcap.LibPcap;
using WareHound.Avalonia.Models;

namespace WareHound.Avalonia.Services;

public class SharpPcapFileService : IPcapFileService
{
    public string BackendName => "SharpPcap (Managed)";

    public bool CanHandle(string filePath)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        return ext == ".pcap" || ext == ".pcapng" || ext == ".cap";
    }

    public async Task SaveAsync(string filePath, IEnumerable<PacketInfo> packets,
        IProgress<int>? progress = null, CancellationToken cancellationToken = default)
    {
        var packetList = packets.Where(p => p.RawData != null && p.CaptureLen > 0).ToList();

        if (packetList.Count == 0)
        {
            throw new InvalidOperationException("No packets with raw data to save. Packets must have been captured (not just metadata).");
        }

        await Task.Run(() =>
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }

            using var writer = new CaptureFileWriterDevice(filePath);
            writer.Open(LinkLayers.Ethernet);

            for (int i = 0; i < packetList.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var pkt = packetList[i];
                if (pkt.RawData == null) continue;

                var timestamp = new PosixTimeval(pkt.CaptureTime);
                var rawCapture = new RawCapture(LinkLayers.Ethernet, timestamp, pkt.RawData);

                writer.Write(rawCapture);

                progress?.Report((i + 1) * 100 / packetList.Count);
            }

            writer.Close();
        }, cancellationToken);
    }

    public async Task<IList<PacketInfo>> LoadAsync(string filePath,
        IProgress<int>? progress = null, CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            var packets = new List<PacketInfo>();

            using var reader = new CaptureFileReaderDevice(filePath);
            reader.Open();

            int packetNumber = 0;
            GetPacketStatus status;
            PacketCapture e;

            while ((status = reader.GetNextPacket(out e)) == GetPacketStatus.PacketRead)
            {
                cancellationToken.ThrowIfCancellationRequested();

                packetNumber++;
                var rawCapture = e.GetPacket();
                var packet = ParsePacket(rawCapture, packetNumber);
                packets.Add(packet);

                if (packetNumber % 100 == 0)
                {
                    progress?.Report(packetNumber);
                }
            }

            reader.Close();
            return packets;
        }, cancellationToken);
    }

    private static PacketInfo ParsePacket(RawCapture rawCapture, int number)
    {
        var packetInfo = new PacketInfo
        {
            Number = number,
            CaptureTime = rawCapture.Timeval.Date,
            RawData = rawCapture.Data,
            CaptureLen = (uint)rawCapture.Data.Length,
            OriginalLen = (uint)rawCapture.Data.Length
        };

        try
        {
            var packet = Packet.ParsePacket(rawCapture.LinkLayerType, rawCapture.Data);

            // Parse Ethernet layer
            var ethernetPacket = packet.Extract<EthernetPacket>();
            if (ethernetPacket != null)
            {
                packetInfo.SourceMac = ethernetPacket.SourceHardwareAddress.ToString();
                packetInfo.DestMac = ethernetPacket.DestinationHardwareAddress.ToString();
            }

            // Parse ARP
            var arpPacket = packet.Extract<ArpPacket>();
            if (arpPacket != null)
            {
                packetInfo.Protocol = "ARP";
                packetInfo.SourceIp = arpPacket.SenderProtocolAddress?.ToString() ?? "";
                packetInfo.DestIp = arpPacket.TargetProtocolAddress?.ToString() ?? "";
                return packetInfo;
            }

            // Parse IP layer
            var ipPacket = packet.Extract<IPPacket>();
            if (ipPacket != null)
            {
                packetInfo.SourceIp = ipPacket.SourceAddress.ToString();
                packetInfo.DestIp = ipPacket.DestinationAddress.ToString();
                packetInfo.Id = ipPacket is IPv4Packet ipv4 ? ipv4.Id : 0;
            }

            // Parse TCP layer
            var tcpPacket = packet.Extract<TcpPacket>();
            if (tcpPacket != null)
            {
                packetInfo.SourcePort = tcpPacket.SourcePort;
                packetInfo.DestPort = tcpPacket.DestinationPort;
                packetInfo.Protocol = GetTcpProtocolName(tcpPacket.SourcePort, tcpPacket.DestinationPort);
                return packetInfo;
            }

            // Parse UDP layer
            var udpPacket = packet.Extract<UdpPacket>();
            if (udpPacket != null)
            {
                packetInfo.SourcePort = udpPacket.SourcePort;
                packetInfo.DestPort = udpPacket.DestinationPort;
                packetInfo.Protocol = GetUdpProtocolName(udpPacket.SourcePort, udpPacket.DestinationPort);
                return packetInfo;
            }

            // Parse ICMP layer
            var icmpPacket = packet.Extract<IcmpV4Packet>();
            if (icmpPacket != null)
            {
                packetInfo.Protocol = "ICMP";
                return packetInfo;
            }

            var icmpV6Packet = packet.Extract<IcmpV6Packet>();
            if (icmpV6Packet != null)
            {
                packetInfo.Protocol = "ICMPv6";
                return packetInfo;
            }

            if (string.IsNullOrEmpty(packetInfo.Protocol))
            {
                packetInfo.Protocol = ipPacket?.Protocol.ToString().ToUpper() ?? "UNKNOWN";
            }
        }
        catch
        {
            packetInfo.Protocol = "UNKNOWN";
        }

        return packetInfo;
    }

    private static string GetTcpProtocolName(int srcPort, int dstPort)
    {
        // HTTP
        if (srcPort == 80 || dstPort == 80 || srcPort == 8080 || dstPort == 8080)
            return "HTTP";

        // HTTPS/TLS
        if (srcPort == 443 || dstPort == 443 || srcPort == 8443 || dstPort == 8443)
            return "HTTPS";

        // SSH
        if (srcPort == 22 || dstPort == 22)
            return "SSH";

        // FTP
        if (srcPort == 21 || dstPort == 21)
            return "FTP";

        // FTP Data
        if (srcPort == 20 || dstPort == 20)
            return "FTP-DATA";

        // Telnet
        if (srcPort == 23 || dstPort == 23)
            return "TELNET";

        // SMTP
        if (srcPort == 25 || dstPort == 25 || srcPort == 587 || dstPort == 587 || srcPort == 465 || dstPort == 465)
            return "SMTP";

        // POP3
        if (srcPort == 110 || dstPort == 110 || srcPort == 995 || dstPort == 995)
            return "POP3";

        // IMAP
        if (srcPort == 143 || dstPort == 143 || srcPort == 993 || dstPort == 993)
            return "IMAP";

        // DNS over TCP
        if (srcPort == 53 || dstPort == 53)
            return "DNS";

        // MySQL
        if (srcPort == 3306 || dstPort == 3306)
            return "MySQL";

        // PostgreSQL
        if (srcPort == 5432 || dstPort == 5432)
            return "PostgreSQL";

        // MS SQL Server
        if (srcPort == 1433 || dstPort == 1433)
            return "MSSQL";

        // Redis
        if (srcPort == 6379 || dstPort == 6379)
            return "Redis";

        // MongoDB
        if (srcPort == 27017 || dstPort == 27017)
            return "MongoDB";

        // RDP
        if (srcPort == 3389 || dstPort == 3389)
            return "RDP";

        // SMB
        if (srcPort == 445 || dstPort == 445 || srcPort == 139 || dstPort == 139)
            return "SMB";

        // LDAP
        if (srcPort == 389 || dstPort == 389 || srcPort == 636 || dstPort == 636)
            return "LDAP";

        // Kerberos
        if (srcPort == 88 || dstPort == 88)
            return "Kerberos";

        // MQTT
        if (srcPort == 1883 || dstPort == 1883 || srcPort == 8883 || dstPort == 8883)
            return "MQTT";

        return "TCP";
    }

    private static string GetUdpProtocolName(int srcPort, int dstPort)
    {
        // DNS
        if (srcPort == 53 || dstPort == 53)
            return "DNS";

        // DHCP
        if (srcPort == 67 || dstPort == 67 || srcPort == 68 || dstPort == 68)
            return "DHCP";

        // NTP
        if (srcPort == 123 || dstPort == 123)
            return "NTP";

        // SNMP
        if (srcPort == 161 || dstPort == 161 || srcPort == 162 || dstPort == 162)
            return "SNMP";

        // TFTP
        if (srcPort == 69 || dstPort == 69)
            return "TFTP";

        // Syslog
        if (srcPort == 514 || dstPort == 514)
            return "Syslog";

        // NetBIOS
        if (srcPort == 137 || dstPort == 137 || srcPort == 138 || dstPort == 138)
            return "NetBIOS";

        // mDNS
        if (srcPort == 5353 || dstPort == 5353)
            return "mDNS";

        // LLMNR
        if (srcPort == 5355 || dstPort == 5355)
            return "LLMNR";

        // QUIC (HTTP/3)
        if (srcPort == 443 || dstPort == 443)
            return "QUIC";

        // SIP
        if (srcPort == 5060 || dstPort == 5060 || srcPort == 5061 || dstPort == 5061)
            return "SIP";

        // RADIUS
        if (srcPort == 1812 || dstPort == 1812 || srcPort == 1813 || dstPort == 1813)
            return "RADIUS";

        // OpenVPN
        if (srcPort == 1194 || dstPort == 1194)
            return "OpenVPN";

        // WireGuard
        if (srcPort == 51820 || dstPort == 51820)
            return "WireGuard";

        return "UDP";
    }
}
