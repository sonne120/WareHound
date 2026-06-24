#pragma once

#include <stdint.h>
#include <stdbool.h>

#if defined(_WIN32)
    #define SNIFFER_API __declspec(dllexport)
#else
    #define SNIFFER_API __attribute__((visibility("default")))
#endif

#ifdef __cplusplus
extern "C" {
#endif

#pragma pack(push, 1)

    typedef struct {
        uint64_t totalPackets;
        uint64_t totalBytes;
        uint64_t activeFlows;
        double   packetsPerSecond;
        double   bytesPerSecond;
        double   captureDurationSeconds;
        int32_t  uniqueProtocols;
        int32_t  uniqueSourceIPs;
        int32_t  uniqueDestIPs;
    } NativeCaptureStatistics;

    typedef struct {
        char     protocolName[32];
        uint64_t packetCount;
        uint64_t byteCount;
        double   percentage;
    } NativeProtocolStats;

    typedef struct {
        char     ipAddress[64];
        uint64_t packetCount;
        uint64_t byteCount;
    } NativeTalkerStats;

    typedef struct {
        uint16_t port;
        char     serviceName[32];
        uint64_t packetCount;
    } NativePortStats;

#pragma pack(pop)

    // Enable/disable native statistics collection
    SNIFFER_API void Sniffer_EnableNativeStats(void* sniffer, bool enable);
    SNIFFER_API bool Sniffer_IsNativeStatsEnabled(void* sniffer);
    
    // Get overall capture statistics
    SNIFFER_API bool Sniffer_GetCaptureStatistics(void* sniffer, NativeCaptureStatistics* stats);
    
    // Get protocol breakdown (returns count of protocols)
    SNIFFER_API int Sniffer_GetProtocolStats(void* sniffer, NativeProtocolStats* stats, int maxCount);
    
    // Get top talkers (returns count of talkers)
    SNIFFER_API int Sniffer_GetTopSourceIPs(void* sniffer, NativeTalkerStats* stats, int maxCount);
    SNIFFER_API int Sniffer_GetTopDestIPs(void* sniffer, NativeTalkerStats* stats, int maxCount);
    
    // Get top ports (returns count of ports)
    SNIFFER_API int Sniffer_GetTopPorts(void* sniffer, NativePortStats* stats, int maxCount);
    
    // Clear statistics
    SNIFFER_API void Sniffer_ClearStatistics(void* sniffer);
    
    // Get flow count
    SNIFFER_API uint64_t Sniffer_GetFlowCount(void* sniffer);

#ifdef __cplusplus
}
#endif
