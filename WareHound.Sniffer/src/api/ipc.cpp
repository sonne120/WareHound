#include "sniff/api/ipc.h"
#include "sniff/sniff.hpp"
#include "sniff/capture/device_scanner.hpp"
#include "ipc_pipe.hpp"
#include <pcap.h>
#include <memory>
#include <mutex>
#include <vector>
#include <string>
#include <stdexcept>
#include <cstring>

static std::unique_ptr<sniff::Pipeline> g_pipeline;
static std::mutex g_pipeline_mutex;
static sniff::PipelineConfig g_config;

static int g_ipc_mode = SNIFFER_IPC_MODE_PIPE;
static PacketDataCallback g_packetCallback = nullptr;

extern "C" {

IPC_EXPORT void IPC_CALL fnSetPacketCallback(PacketDataCallback cb) {
    g_packetCallback = cb;
}

IPC_EXPORT void IPC_CALL fnSetIpcMode(int mode) {
    std::lock_guard<std::mutex> lock(g_pipeline_mutex);
    if (!g_pipeline) { 
        g_ipc_mode = mode;
    }
}

IPC_EXPORT void IPC_CALL fnDevCPPDLL(char** data, int* sizes, int* count) {
    if (!count) return;
    
    std::vector<std::string> deviceList = sniff::capture::DeviceScanner::ListDevices();
    
    *count = static_cast<int>(deviceList.size());
    if (data && sizes) {
        for (int i = 0; i < *count; ++i) {
            sizes[i] = static_cast<int>(deviceList[i].size());
            data[i] = new char[sizes[i] + 1];
#ifdef _WIN32
            strcpy_s(data[i], sizes[i] + 1, deviceList[i].c_str());
#else
            std::strncpy(data[i], deviceList[i].c_str(), sizes[i]);
            data[i][sizes[i]] = '\0';
#endif
        }
    }
}

IPC_EXPORT void IPC_CALL fnCPPDLL(int d) {
    fnPutdevCPPDLL(d);
    fnSetIpcMode(SNIFFER_IPC_MODE_PIPE);
    //fnStartCapture();
}

IPC_EXPORT void IPC_CALL fnPutdevCPPDLL(int dev) {    
    g_config.interface_name = sniff::capture::DeviceScanner::GetDeviceNameAt(dev); 
}

IPC_EXPORT void IPC_CALL fnStartCapture() {
    std::lock_guard<std::mutex> lock(g_pipeline_mutex);
    if (!g_pipeline) {
        if (g_config.interface_name.empty()) {
            g_config.interface_name = sniff::capture::DeviceScanner::GetDeviceNameAt(0);
        }
        
        if (g_ipc_mode == SNIFFER_IPC_MODE_PIPE || g_ipc_mode == SNIFFER_IPC_MODE_BOTH) {
            StartPipeWriter();
        }

        g_config.raw_packet_callback = [](const uint8_t* data, uint32_t len) {
            if (g_ipc_mode == SNIFFER_IPC_MODE_CALLBACK || g_ipc_mode == SNIFFER_IPC_MODE_BOTH) {
                if (g_packetCallback) {
                    g_packetCallback(data, len);
                }
            }
            if (g_ipc_mode == SNIFFER_IPC_MODE_PIPE || g_ipc_mode == SNIFFER_IPC_MODE_BOTH) {
                PushToPipe(data, len);
            }
        };

       

        g_pipeline = std::make_unique<sniff::Pipeline>(g_config);
        g_pipeline->start();

    }
}

IPC_EXPORT void IPC_CALL fnStopCapture() {
    std::lock_guard<std::mutex> lock(g_pipeline_mutex);
    if (g_pipeline) {
        g_pipeline->stop();
        StopPipeWriter();
        g_pipeline.reset();
    }
}

IPC_EXPORT void IPC_CALL fnCloseApp() {
    std::lock_guard<std::mutex> lock(g_pipeline_mutex);
    if (g_pipeline) {
        g_pipeline->stop();
        StopPipeWriter();
        g_pipeline.reset();
    }
}

IPC_EXPORT void IPC_CALL fnWarmupDevice(int dev) {
    std::string name = sniff::capture::DeviceScanner::GetDeviceNameAt(dev);
    if (name.empty()) return;

    char errbuf[PCAP_ERRBUF_SIZE] = {0};
    pcap_t* handle = pcap_open_live(name.c_str(), 2048, 1, 100, errbuf);
    if (handle) {
        pcap_close(handle);
    }
}

}

sniff::Pipeline* GetGlobalPipeline() {
    return g_pipeline.get();
}
