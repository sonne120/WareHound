using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using WareHound.Avalonia.Models;

namespace WareHound.Avalonia.ViewModels;

public class DashboardViewModel : ViewModelBase
{
    [Reactive] public SavedCollection? SelectedCollection { get; set; }
    [Reactive] public PacketInfo? SelectedPacket { get; set; }
    [Reactive] public ObservableCollection<TreeNode> PacketDetails { get; set; } = new();

    public ObservableCollection<SavedCollection> Collections { get; } = new();

    // Commands
    public ReactiveCommand<Unit, Unit> DeleteCommand { get; }
    public ReactiveCommand<Unit, Unit> ExportCommand { get; }

    public DashboardViewModel()
    {
        var hasSelectedCollection = this.WhenAnyValue(x => x.SelectedCollection)
            .Select(c => c != null);

        DeleteCommand = ReactiveCommand.Create(DeleteCollection, hasSelectedCollection);
        ExportCommand = ReactiveCommand.Create(ExportToCsv, hasSelectedCollection);

        // Subscribe to SelectedPacket changes
        this.WhenAnyValue(x => x.SelectedPacket)
            .Subscribe(_ => UpdatePacketDetails());

        // Subscribe to SelectedCollection changes
        this.WhenAnyValue(x => x.SelectedCollection)
            .Subscribe(_ => SelectedPacket = null);
    }

    public void SaveCollection(string name, IEnumerable<PacketInfo> packets)
    {
        var packetList = packets.ToList();
        var collection = new SavedCollection
        {
            Id = Guid.NewGuid(),
            Name = name,
            PacketCount = packetList.Count,
            CreatedAt = DateTime.Now,
            Packets = new ObservableCollection<PacketInfo>(packetList)
        };
        Collections.Add(collection);
    }

    private void DeleteCollection()
    {
        if (SelectedCollection == null) return;
        Collections.Remove(SelectedCollection);
        SelectedCollection = null;
    }

    private void ExportToCsv()
    {
        // Export functionality - can be implemented with file dialog
        if (SelectedCollection == null) return;

        // TODO: Implement file save dialog for Avalonia
        // For now, this is a placeholder
    }

    private void UpdatePacketDetails()
    {
        PacketDetails.Clear();
        if (SelectedPacket == null) return;

        var p = SelectedPacket;

        // Frame
        var frame = new TreeNode($"Packet #{p.Number}: {p.Protocol}");
        frame.AddChild($"Capture Time: {p.CaptureTime:yyyy-MM-dd HH:mm:ss.fff}");
        frame.AddChild($"Packet ID: {p.Id}");
        frame.AddChild($"Original Length: {p.OriginalLen} bytes");
        frame.AddChild($"Captured Length: {p.CaptureLen} bytes");
        PacketDetails.Add(frame);

        // Ethernet
        if (!string.IsNullOrEmpty(p.SourceMac) || !string.IsNullOrEmpty(p.DestMac))
        {
            var eth = new TreeNode($"Ethernet II, Src: {p.SourceMac}, Dst: {p.DestMac}");
            eth.AddChild($"Source MAC: {p.SourceMac}");
            eth.AddChild($"Destination MAC: {p.DestMac}");
            PacketDetails.Add(eth);
        }

        // IP
        if (!string.IsNullOrEmpty(p.SourceIp) || !string.IsNullOrEmpty(p.DestIp))
        {
            var ip = new TreeNode($"Internet Protocol, Src: {p.SourceIp}, Dst: {p.DestIp}");
            ip.AddChild($"Source IP: {p.SourceIp}");
            ip.AddChild($"Destination IP: {p.DestIp}");
            PacketDetails.Add(ip);
        }

        // Protocol
        if (!string.IsNullOrEmpty(p.Protocol))
        {
            var proto = new TreeNode($"{p.Protocol}, Src Port: {p.SourcePort}, Dst Port: {p.DestPort}");
            proto.AddChild($"Source Port: {p.SourcePort}");
            proto.AddChild($"Destination Port: {p.DestPort}");
            if (!string.IsNullOrEmpty(p.Info))
            {
                proto.AddChild($"Info: {p.Info}");
            }
            PacketDetails.Add(proto);
        }
    }
}

public class SavedCollection
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public int PacketCount { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public ObservableCollection<PacketInfo> Packets { get; set; } = new();
}
