#include "sniff/api/statistic_exports.h"
#include "sniff/sniff.hpp"
#include "sniff/analyzers/handleProto.hpp"
#include <unordered_map>
#include <unordered_set>
#include <vector>
#include <algorithm>
#include <chrono>
#include <mutex>
#include <cstdio>
#include <cstring>

extern sniff::Pipeline* GetGlobalPipeline();

namespace {

// Capture timing baseline so packets/sec and duration come from C++ too.
std::mutex g_time_mutex;
std::chrono::steady_clock::time_point g_start{};
bool g_started = false;
const void* g_last_pipeline = nullptr;

double elapsed_seconds() {
    std::lock_guard<std::mutex> lock(g_time_mutex);
    if (!g_started) {
        g_start = std::chrono::steady_clock::now();
        g_started = true;
        return 0.0;
    }
    return std::chrono::duration<double>(std::chrono::steady_clock::now() - g_start).count();
}

void reset_timing() {
    std::lock_guard<std::mutex> lock(g_time_mutex);
    g_started = false;
}

void copy_str(char* dst, std::size_t cap, const char* src) {
    if (cap == 0) return;
#ifdef _WIN32
    strncpy_s(dst, cap, src ? src : "", _TRUNCATE);
#else
    std::strncpy(dst, src ? src : "", cap - 1);
    dst[cap - 1] = '\0';
#endif
}

void format_ipv4(uint32_t ip, char* out, std::size_t cap) {
    std::snprintf(out, cap, "%u.%u.%u.%u",
                  (ip >> 24) & 0xFF, (ip >> 16) & 0xFF, (ip >> 8) & 0xFF, ip & 0xFF);
}

const char* service_name(uint16_t port) {
    switch (port) {
        case 20:   return "FTP-Data";
        case 21:   return "FTP";
        case 22:   return "SSH";
        case 23:   return "Telnet";
        case 25:   return "SMTP";
        case 53:   return "DNS";
        case 67:
        case 68:   return "DHCP";
        case 80:   return "HTTP";
        case 110:  return "POP3";
        case 123:  return "NTP";
        case 143:  return "IMAP";
        case 443:  return "HTTPS";
        case 445:  return "SMB";
        case 3306: return "MySQL";
        case 3389: return "RDP";
        case 5432: return "PostgreSQL";
        case 6379: return "Redis";
        case 8080: return "HTTP-Alt";
        default:   return "";
    }
}

} // namespace

extern "C" {

SNIFFER_API void Sniffer_EnableNativeStats(void* /*sniffer*/, bool /*enable*/) {
}

SNIFFER_API bool Sniffer_IsNativeStatsEnabled(void* /*sniffer*/) {
    return true;
}

SNIFFER_API bool Sniffer_GetCaptureStatistics(void* sniffer, NativeCaptureStatistics* stats) {
    if (!stats) return false;

    sniff::Pipeline* p = sniffer ? static_cast<sniff::Pipeline*>(sniffer) : GetGlobalPipeline();
    if (!p) return false;

    // A new capture creates a fresh pipeline; restart the timing baseline so
    // packets/sec and duration reflect the current capture, not previous ones.
    if (p != g_last_pipeline) {
        reset_timing();
        g_last_pipeline = p;
    }

    auto s = p->stats();

    uint64_t total_packets = s.captured;
    uint64_t total_bytes = 0;
    for (uint64_t b : s.reducer_bytes) total_bytes += b;

    // Aggregate protocol packet counts across reducers to count distinct protocols.
    uint64_t proto_counts[256] = {0};
    for (const auto& rc : s.proto_counts) {
        for (int i = 0; i < 256; ++i) proto_counts[i] += rc[i];
    }
    int unique_protocols = 0;
    for (int i = 0; i < 256; ++i) if (proto_counts[i] > 0) ++unique_protocols;

    // Distinct source/dest IPs and active flows from the reducers.
    std::unordered_set<uint32_t> src_ips, dst_ips;
    uint64_t active_flows = 0;
    for (const auto& r : p->reducers()) {
        if (auto l4 = std::dynamic_pointer_cast<sniff::pipeline::L4CounterReducer>(r)) {
            std::vector<sniff::pipeline::L4CounterReducer::StatEntryIP> src, dst;
            l4->get_top_src_ips(src);
            l4->get_top_dst_ips(dst);
            for (const auto& e : src) src_ips.insert(e.ip);
            for (const auto& e : dst) dst_ips.insert(e.ip);
            active_flows += l4->get_flow_count();
        }
    }

    double secs = elapsed_seconds();

    stats->totalPackets = total_packets;
    stats->totalBytes = total_bytes;
    stats->activeFlows = active_flows;
    stats->packetsPerSecond = secs > 0.0 ? static_cast<double>(total_packets) / secs : 0.0;
    stats->bytesPerSecond = secs > 0.0 ? static_cast<double>(total_bytes) / secs : 0.0;
    stats->captureDurationSeconds = secs;
    stats->uniqueProtocols = unique_protocols;
    stats->uniqueSourceIPs = static_cast<int32_t>(src_ips.size());
    stats->uniqueDestIPs = static_cast<int32_t>(dst_ips.size());

    return true;
}

SNIFFER_API int Sniffer_GetProtocolStats(void* sniffer, NativeProtocolStats* stats, int maxCount) {
    if (!stats || maxCount <= 0) return 0;

    sniff::Pipeline* p = sniffer ? static_cast<sniff::Pipeline*>(sniffer) : GetGlobalPipeline();
    if (!p) return 0;

    auto s = p->stats();

    uint64_t proto_counts[256] = {0};
    uint64_t proto_bytes[256] = {0};
    for (const auto& rc : s.proto_counts) {
        for (int i = 0; i < 256; ++i) proto_counts[i] += rc[i];
    }
    for (const auto& rb : s.proto_bytes) {
        for (int i = 0; i < 256; ++i) proto_bytes[i] += rb[i];
    }

    uint64_t total = 0;
    for (int i = 0; i < 256; ++i) total += proto_counts[i];
    double divisor = total > 0 ? static_cast<double>(total) : 1.0;

    // Collect non-empty protocols and sort by packet count (descending).
    std::vector<int> idx;
    for (int i = 0; i < 256; ++i) if (proto_counts[i] > 0) idx.push_back(i);
    std::sort(idx.begin(), idx.end(),
              [&](int a, int b) { return proto_counts[a] > proto_counts[b]; });

    int count = 0;
    for (int i = 0; i < static_cast<int>(idx.size()) && count < maxCount; ++i) {
        int proto = idx[i];
        const char* name = sniff::analyzers::HandleProto::get_name(
            static_cast<sniff::pipeline::L4Protocol>(proto));
        copy_str(stats[count].protocolName, sizeof(stats[count].protocolName), name);
        stats[count].packetCount = proto_counts[proto];
        stats[count].byteCount = proto_bytes[proto];
        stats[count].percentage = static_cast<double>(proto_counts[proto]) / divisor * 100.0;
        ++count;
    }
    return count;
}

SNIFFER_API int Sniffer_GetTopSourceIPs(void* sniffer, NativeTalkerStats* stats, int maxCount) {
    if (!stats || maxCount <= 0) return 0;
    sniff::Pipeline* p = sniffer ? static_cast<sniff::Pipeline*>(sniffer) : GetGlobalPipeline();
    if (!p) return 0;

    std::unordered_map<uint32_t, sniff::pipeline::L4CounterReducer::StatEntryIP> aggregated;
    for (const auto& r : p->reducers()) {
        if (auto l4 = std::dynamic_pointer_cast<sniff::pipeline::L4CounterReducer>(r)) {
            std::vector<sniff::pipeline::L4CounterReducer::StatEntryIP> local_stats;
            l4->get_top_src_ips(local_stats);
            for (const auto& s : local_stats) {
                aggregated[s.ip].ip = s.ip;
                aggregated[s.ip].count += s.count;
                aggregated[s.ip].bytes += s.bytes;
            }
        }
    }

    std::vector<sniff::pipeline::L4CounterReducer::StatEntryIP> sorted;
    for (const auto& kv : aggregated) sorted.push_back(kv.second);
    std::sort(sorted.begin(), sorted.end(), [](const auto& a, const auto& b) { return a.count > b.count; });

    int count = 0;
    for (int i = 0; i < static_cast<int>(sorted.size()) && count < maxCount; ++i) {
        format_ipv4(sorted[i].ip, stats[count].ipAddress, sizeof(stats[count].ipAddress));
        stats[count].packetCount = sorted[i].count;
        stats[count].byteCount = sorted[i].bytes;
        ++count;
    }
    return count;
}

SNIFFER_API int Sniffer_GetTopDestIPs(void* sniffer, NativeTalkerStats* stats, int maxCount) {
    if (!stats || maxCount <= 0) return 0;
    sniff::Pipeline* p = sniffer ? static_cast<sniff::Pipeline*>(sniffer) : GetGlobalPipeline();
    if (!p) return 0;

    std::unordered_map<uint32_t, sniff::pipeline::L4CounterReducer::StatEntryIP> aggregated;
    for (const auto& r : p->reducers()) {
        if (auto l4 = std::dynamic_pointer_cast<sniff::pipeline::L4CounterReducer>(r)) {
            std::vector<sniff::pipeline::L4CounterReducer::StatEntryIP> local_stats;
            l4->get_top_dst_ips(local_stats);
            for (const auto& s : local_stats) {
                aggregated[s.ip].ip = s.ip;
                aggregated[s.ip].count += s.count;
                aggregated[s.ip].bytes += s.bytes;
            }
        }
    }

    std::vector<sniff::pipeline::L4CounterReducer::StatEntryIP> sorted;
    for (const auto& kv : aggregated) sorted.push_back(kv.second);
    std::sort(sorted.begin(), sorted.end(), [](const auto& a, const auto& b) { return a.count > b.count; });

    int count = 0;
    for (int i = 0; i < static_cast<int>(sorted.size()) && count < maxCount; ++i) {
        format_ipv4(sorted[i].ip, stats[count].ipAddress, sizeof(stats[count].ipAddress));
        stats[count].packetCount = sorted[i].count;
        stats[count].byteCount = sorted[i].bytes;
        ++count;
    }
    return count;
}

SNIFFER_API int Sniffer_GetTopPorts(void* sniffer, NativePortStats* stats, int maxCount) {
    if (!stats || maxCount <= 0) return 0;
    sniff::Pipeline* p = sniffer ? static_cast<sniff::Pipeline*>(sniffer) : GetGlobalPipeline();
    if (!p) return 0;

    std::unordered_map<uint16_t, sniff::pipeline::L4CounterReducer::StatEntryPort> aggregated;
    for (const auto& r : p->reducers()) {
        if (auto l4 = std::dynamic_pointer_cast<sniff::pipeline::L4CounterReducer>(r)) {
            std::vector<sniff::pipeline::L4CounterReducer::StatEntryPort> local_stats;
            l4->get_top_ports(local_stats);
            for (const auto& s : local_stats) {
                aggregated[s.port].port = s.port;
                aggregated[s.port].count += s.count;
                aggregated[s.port].bytes += s.bytes;
            }
        }
    }

    std::vector<sniff::pipeline::L4CounterReducer::StatEntryPort> sorted;
    for (const auto& kv : aggregated) sorted.push_back(kv.second);
    std::sort(sorted.begin(), sorted.end(), [](const auto& a, const auto& b) { return a.count > b.count; });

    int count = 0;
    for (int i = 0; i < static_cast<int>(sorted.size()) && count < maxCount; ++i) {
        stats[count].port = sorted[i].port;
        copy_str(stats[count].serviceName, sizeof(stats[count].serviceName), service_name(sorted[i].port));
        stats[count].packetCount = sorted[i].count;
        ++count;
    }
    return count;
}

SNIFFER_API void Sniffer_ClearStatistics(void* sniffer) {
    sniff::Pipeline* p = sniffer ? static_cast<sniff::Pipeline*>(sniffer) : GetGlobalPipeline();
    reset_timing();
    if (!p) return;

    for (const auto& r : p->reducers()) {
        if (auto l4 = std::dynamic_pointer_cast<sniff::pipeline::L4CounterReducer>(r)) {
            l4->clear_stats();
        }
    }
}

SNIFFER_API uint64_t Sniffer_GetFlowCount(void* sniffer) {
    sniff::Pipeline* p = sniffer ? static_cast<sniff::Pipeline*>(sniffer) : GetGlobalPipeline();
    if (!p) return 0;

    uint64_t total_flows = 0;
    for (const auto& r : p->reducers()) {
        if (auto l4 = std::dynamic_pointer_cast<sniff::pipeline::L4CounterReducer>(r)) {
            total_flows += l4->get_flow_count();
        }
    }
    return total_flows;
}

}
