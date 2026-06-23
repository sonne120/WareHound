#pragma once

#include <stdint.h>

#if defined(_WIN32)
    #define IPC_EXPORT __declspec(dllexport)
    #define IPC_CALL __stdcall
#else
    #define IPC_EXPORT __attribute__((visibility("default")))
    #define IPC_CALL
#endif

#ifdef __cplusplus
extern "C" {
#endif

    // Delivery methods configuration
    typedef enum {
        SNIFFER_IPC_MODE_CALLBACK = 0,
        SNIFFER_IPC_MODE_PIPE     = 1,
        SNIFFER_IPC_MODE_BOTH     = 2,
        SNIFFER_IPC_MODE_NONE     = 3
    } SnifferIpcMode;

    // Delegate layout for direct zero-copy callback data
    typedef void (IPC_CALL *PacketDataCallback)(const uint8_t* raw_data, int length);

    // Provide the callback pointer from C# Delegate or Qt slot
    IPC_EXPORT void IPC_CALL fnSetPacketCallback(PacketDataCallback cb);

    // Set operational mode (Callback, Pipe, or Both). Default is CALLBACK(0).
    IPC_EXPORT void IPC_CALL fnSetIpcMode(int mode);

    IPC_EXPORT void IPC_CALL fnDevCPPDLL(char** data, int* sizes, int* count);
    IPC_EXPORT void IPC_CALL fnCPPDLL(int d);
    IPC_EXPORT void IPC_CALL fnPutdevCPPDLL(int dev);
    IPC_EXPORT void IPC_CALL fnStartCapture();
    IPC_EXPORT void IPC_CALL fnStopCapture();
    IPC_EXPORT void IPC_CALL fnCloseApp();
    IPC_EXPORT void IPC_CALL fnWarmupDevice(int dev);

#ifdef __cplusplus
}
#endif
