#include "SnifferExports.h"
#include "Sniffer.h"
#include "builderDevice.h"
#include <vector>
#include <string>

static std::vector<std::string> g_deviceNames;

extern "C" {

    SNIFFER_API void* Sniffer_Create() {
        return new Sniffer();
    }

    SNIFFER_API void Sniffer_Destroy(void* sniffer) {
        delete static_cast<Sniffer*>(sniffer);
    }

    SNIFFER_API int Sniffer_GetDeviceCount() {
        g_deviceNames = builderDevice::Builder(0).ListDevices().Build().getDevices();
        return static_cast<int>(g_deviceNames.size());
    }

    SNIFFER_API const char* Sniffer_GetDeviceName(int index) {
        if (index >= 0 && index < static_cast<int>(g_deviceNames.size())) {
            return g_deviceNames[index].c_str();
        }
        return nullptr;
    }

    SNIFFER_API void Sniffer_SelectDevice(void* sniffer, int deviceIndex) {
        static_cast<Sniffer*>(sniffer)->selectDevice(deviceIndex);
    }

    SNIFFER_API void Sniffer_Start(void* sniffer, PacketCallback callback) {
        static_cast<Sniffer*>(sniffer)->start(callback);
    }

    SNIFFER_API void Sniffer_Stop(void* sniffer) {
        static_cast<Sniffer*>(sniffer)->stop();
    }
}
