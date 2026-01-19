#define _CRT_SECURE_NO_WARNINGS
#include "StatisticsExports.h"
#include "FlowTracker.h"
#include <unordered_map>
#include <algorithm>
#include <mutex>
#include <cstring>
#include <ws2tcpip.h>

using namespace WareHound;

//=============================================================================
// GLOBAL FLOW TRACKER INSTANCE
//=============================================================================

static std::unique_ptr<FlowTracker> g_flowTracker;
static std::mutex g_flowTrackerMutex;
static bool g_nativeStatsEnabled = false;

// IP address counters
static std::unordered_map<uint32_t, uint64_t> g_sourceIPCounts;
static std::unordered_map<uint32_t, uint64_t> g_destIPCounts;
static std::unordered_map<uint16_t, uint64_t> g_portCounts;
static std::mutex g_ipStatsMutex;

//=============================================================================
// HELPER FUNCTIONS
//=============================================================================

static void IP4ToString(uint32_t ip, char* buffer, size_t bufferSize) {
    struct in_addr addr;
    addr.s_addr = ip;
    inet_ntop(AF_INET, &addr, buffer, static_cast<socklen_t>(bufferSize));
}

static const char* GetServiceName(uint16_t port) {
    switch (port) {
        case 20: return "FTP-DATA";
        case 21: return "FTP";
        case 22: return "SSH";
        case 23: return "TELNET";
        case 25: return "SMTP";
        case 53: return "DNS";
        case 67: case 68: return "DHCP";
        case 80: return "HTTP";
        case 110: return "POP3";
        case 123: return "NTP";
        case 143: return "IMAP";
        case 161: case 162: return "SNMP";
        case 389: return "LDAP";
        case 443: return "HTTPS";
        case 445: return "SMB";
        case 993: return "IMAPS";
        case 995: return "POP3S";
        case 3306: return "MySQL";
        case 3389: return "RDP";
        case 5432: return "PostgreSQL";
        case 6379: return "Redis";
        case 8080: return "HTTP-ALT";
        case 8443: return "HTTPS-ALT";
        case 27017: return "MongoDB";
        default: return "";
    }
}

//=============================================================================
// INITIALIZATION
//=============================================================================

void InitFlowTracker() {
    std::lock_guard<std::mutex> lock(g_flowTrackerMutex);
    if (!g_flowTracker) {
        FlowTracker::Config config;
        config.table_size = 65536;
        config.max_flows = 100000;
        config.flow_timeout_us = 300 * 1000000ULL;  // 5 minutes
        g_flowTracker = std::make_unique<FlowTracker>(config);
    }
}

void ProcessPacketForStats(const uint8_t* data, uint32_t len, uint64_t timestamp_us) {
    if (!g_nativeStatsEnabled) return;
    
    InitFlowTracker();
    
    std::lock_guard<std::mutex> lock(g_flowTrackerMutex);
    FlowEntry* flow = g_flowTracker->ProcessPacket(data, len, timestamp_us);
    
    if (flow) {
        // Update IP/port statistics
        std::lock_guard<std::mutex> ipLock(g_ipStatsMutex);
        g_sourceIPCounts[flow->key.src_ip]++;
        g_destIPCounts[flow->key.dst_ip]++;
        if (flow->key.src_port > 0) g_portCounts[flow->key.src_port]++;
        if (flow->key.dst_port > 0) g_portCounts[flow->key.dst_port]++;
    }
}

//=============================================================================
// EXPORTS IMPLEMENTATION
//=============================================================================

extern "C" {

SNIFFER_API void Sniffer_EnableNativeStats(void* sniffer, bool enable) {
    g_nativeStatsEnabled = enable;
    if (enable) {
        InitFlowTracker();
    }
}

SNIFFER_API bool Sniffer_IsNativeStatsEnabled(void* sniffer) {
    return g_nativeStatsEnabled;
}

SNIFFER_API bool Sniffer_GetCaptureStatistics(void* sniffer, NativeCaptureStatistics* stats) {
    if (!stats || !g_flowTracker) {
        if (stats) memset(stats, 0, sizeof(NativeCaptureStatistics));
        return false;
    }
    
    std::lock_guard<std::mutex> lock(g_flowTrackerMutex);
    
    stats->totalPackets = g_flowTracker->GetPacketsProcessed();
    stats->totalBytes = g_flowTracker->GetBytesProcessed();
    stats->activeFlows = g_flowTracker->GetFlowCount();
    stats->captureDurationSeconds = g_flowTracker->GetCaptureDurationSeconds();
    
    if (stats->captureDurationSeconds > 0) {
        stats->packetsPerSecond = static_cast<double>(stats->totalPackets) / stats->captureDurationSeconds;
        stats->bytesPerSecond = static_cast<double>(stats->totalBytes) / stats->captureDurationSeconds;
    } else {
        stats->packetsPerSecond = 0;
        stats->bytesPerSecond = 0;
    }
    
    // Count unique protocols from flow table
    auto flows = g_flowTracker->GetFlowTable().GetAllFlows();
    std::unordered_map<int, bool> uniqueProtos;
    for (const auto& flow : flows) {
        if (flow.stats.app_protocol != AppProtocol::UNKNOWN) {
            uniqueProtos[static_cast<int>(flow.stats.app_protocol)] = true;
        }
    }
    stats->uniqueProtocols = static_cast<int>(uniqueProtos.size());
    
    std::lock_guard<std::mutex> ipLock(g_ipStatsMutex);
    stats->uniqueSourceIPs = static_cast<int>(g_sourceIPCounts.size());
    stats->uniqueDestIPs = static_cast<int>(g_destIPCounts.size());
    
    return true;
}

SNIFFER_API int Sniffer_GetProtocolStats(void* sniffer, NativeProtocolStats* stats, int maxCount) {
    if (!stats || !g_flowTracker || maxCount <= 0) return 0;
    
    std::lock_guard<std::mutex> lock(g_flowTrackerMutex);
    
    // Aggregate protocol statistics from flows
    std::unordered_map<AppProtocol, std::pair<uint64_t, uint64_t>> protoCounts;  // protocol -> (packets, bytes)
    uint64_t totalPackets = 0;
    
    auto flows = g_flowTracker->GetFlowTable().GetAllFlows();
    for (const auto& flow : flows) {
        AppProtocol proto = flow.stats.app_protocol;
        uint64_t packets = flow.stats.TotalPackets();
        uint64_t bytes = flow.stats.TotalBytes();
        
        protoCounts[proto].first += packets;
        protoCounts[proto].second += bytes;
        totalPackets += packets;
    }
    
    // Sort by packet count descending
    std::vector<std::pair<AppProtocol, std::pair<uint64_t, uint64_t>>> sorted(
        protoCounts.begin(), protoCounts.end());
    std::sort(sorted.begin(), sorted.end(),
        [](const auto& a, const auto& b) { return a.second.first > b.second.first; });
    
    int count = (std::min)(maxCount, static_cast<int>(sorted.size()));
    for (int i = 0; i < count; i++) {
        strncpy(stats[i].protocolName, ProtocolDetector::GetProtocolName(sorted[i].first), 31);
        stats[i].protocolName[31] = '\0';
        stats[i].packetCount = sorted[i].second.first;
        stats[i].byteCount = sorted[i].second.second;
        stats[i].percentage = totalPackets > 0 
            ? (static_cast<double>(sorted[i].second.first) / totalPackets) * 100.0 
            : 0.0;
    }
    
    return count;
}

SNIFFER_API int Sniffer_GetTopSourceIPs(void* sniffer, NativeTalkerStats* stats, int maxCount) {
    if (!stats || maxCount <= 0) return 0;
    
    std::lock_guard<std::mutex> lock(g_ipStatsMutex);
    
    // Sort by count descending
    std::vector<std::pair<uint32_t, uint64_t>> sorted(
        g_sourceIPCounts.begin(), g_sourceIPCounts.end());
    std::sort(sorted.begin(), sorted.end(),
        [](const auto& a, const auto& b) { return a.second > b.second; });
    
    int count = (std::min)(maxCount, static_cast<int>(sorted.size()));
    for (int i = 0; i < count; i++) {
        IP4ToString(sorted[i].first, stats[i].ipAddress, 64);
        stats[i].packetCount = sorted[i].second;
        stats[i].byteCount = 0;  // Not tracked per-IP currently
    }
    
    return count;
}

SNIFFER_API int Sniffer_GetTopDestIPs(void* sniffer, NativeTalkerStats* stats, int maxCount) {
    if (!stats || maxCount <= 0) return 0;
    
    std::lock_guard<std::mutex> lock(g_ipStatsMutex);
    
    std::vector<std::pair<uint32_t, uint64_t>> sorted(
        g_destIPCounts.begin(), g_destIPCounts.end());
    std::sort(sorted.begin(), sorted.end(),
        [](const auto& a, const auto& b) { return a.second > b.second; });
    
    int count = (std::min)(maxCount, static_cast<int>(sorted.size()));
    for (int i = 0; i < count; i++) {
        IP4ToString(sorted[i].first, stats[i].ipAddress, 64);
        stats[i].packetCount = sorted[i].second;
        stats[i].byteCount = 0;
    }
    
    return count;
}

SNIFFER_API int Sniffer_GetTopPorts(void* sniffer, NativePortStats* stats, int maxCount) {
    if (!stats || maxCount <= 0) return 0;
    
    std::lock_guard<std::mutex> lock(g_ipStatsMutex);
    
    std::vector<std::pair<uint16_t, uint64_t>> sorted(
        g_portCounts.begin(), g_portCounts.end());
    std::sort(sorted.begin(), sorted.end(),
        [](const auto& a, const auto& b) { return a.second > b.second; });
    
    int count = (std::min)(maxCount, static_cast<int>(sorted.size()));
    for (int i = 0; i < count; i++) {
        stats[i].port = sorted[i].first;
        strncpy(stats[i].serviceName, GetServiceName(sorted[i].first), 31);
        stats[i].serviceName[31] = '\0';
        stats[i].packetCount = sorted[i].second;
    }
    
    return count;
}

SNIFFER_API void Sniffer_ClearStatistics(void* sniffer) {
    {
        std::lock_guard<std::mutex> lock(g_flowTrackerMutex);
        if (g_flowTracker) {
            g_flowTracker->Clear();
        }
    }
    
    {
        std::lock_guard<std::mutex> lock(g_ipStatsMutex);
        g_sourceIPCounts.clear();
        g_destIPCounts.clear();
        g_portCounts.clear();
    }
}

SNIFFER_API uint64_t Sniffer_GetFlowCount(void* sniffer) {
    std::lock_guard<std::mutex> lock(g_flowTrackerMutex);
    return g_flowTracker ? g_flowTracker->GetFlowCount() : 0;
}

} // extern "C"
