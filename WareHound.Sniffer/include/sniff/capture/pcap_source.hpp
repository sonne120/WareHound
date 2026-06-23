#pragma once

#include "sniff/capture/packet_source.hpp"

#include <atomic>
#include <memory>

// Forward-declare pcap types to keep the public header clean.
struct pcap;
using pcap_t = struct pcap;

namespace sniff::capture {

class PcapSource final : public IPacketSource {
public:
    PcapSource();
    ~PcapSource() override;

    PcapSource(const PcapSource&) = delete;
    PcapSource& operator=(const PcapSource&) = delete;

    void open(const CaptureConfig& cfg) override;
    void run(const PacketHandler& handler) override;
    void stop() override;
    const char* backend_name() const noexcept override { return "libpcap"; }


private:
    pcap_t* handle_{nullptr};
    std::atomic<bool> running_{false};
    int link_type_{-1};
};

} 
