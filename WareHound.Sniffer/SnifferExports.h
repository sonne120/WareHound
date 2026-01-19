#pragma once

#ifdef WAREHOUND_SNIFFER_EXPORTS
#define SNIFFER_API __declspec(dllexport)
#else
#define SNIFFER_API __declspec(dllimport)
#endif

extern "C" {
    typedef void(__cdecl* PacketCallback)(const char* packetData);

    SNIFFER_API void* Sniffer_Create();
    SNIFFER_API void Sniffer_Destroy(void* sniffer);
    SNIFFER_API int Sniffer_GetDeviceCount();
    SNIFFER_API const char* Sniffer_GetDeviceName(int index);
    SNIFFER_API void Sniffer_SelectDevice(void* sniffer, int deviceIndex);
    SNIFFER_API void Sniffer_Start(void* sniffer, PacketCallback callback);
    SNIFFER_API void Sniffer_Stop(void* sniffer);
}
