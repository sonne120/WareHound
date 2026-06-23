#include "sniff/capture/device_scanner.hpp"
#include <pcap.h>
#include <stdexcept>
#include <mutex>

namespace sniff::capture {

namespace {
    std::mutex g_cache_mutex;
    std::vector<std::string> g_device_names;

    std::vector<std::string> ScanDevices(std::vector<std::string>* out_names) {
        std::vector<std::string> descriptions;
        pcap_if_t* alldevs;
        char errbuf[PCAP_ERRBUF_SIZE];

#ifdef _WIN32
        if (pcap_findalldevs_ex(PCAP_SRC_IF_STRING, nullptr, &alldevs, errbuf) == -1) {
#else
        if (pcap_findalldevs(&alldevs, errbuf) == -1) {
#endif
            return descriptions;
        }

        int idx = 0;
        for (pcap_if_t* it = alldevs; it; it = it->next) {
            ++idx;
            std::string desc;
            if (it->description) {
                desc = std::to_string(idx) + "_" + it->description;
            } else {
                desc = std::to_string(idx) + "_" + it->name;
            }

            if (desc.size() > 53) desc.resize(53);
            size_t lastWhitespace = desc.find_last_of(" \t\n\r");
            if (lastWhitespace != std::string::npos) {
                desc.erase(lastWhitespace);
            }
            descriptions.push_back(std::move(desc));

            if (out_names) {
                out_names->push_back(it->name ? it->name : "");
            }
        }

        if (alldevs) {
            pcap_freealldevs(alldevs);
        }

        return descriptions;
    }
}

std::vector<std::string> DeviceScanner::ListDevices() {
    std::vector<std::string> names;
    std::vector<std::string> descriptions = ScanDevices(&names);

    {
        std::lock_guard<std::mutex> lock(g_cache_mutex);
        g_device_names = std::move(names);
    }

    return descriptions;
}

std::string DeviceScanner::GetDeviceNameAt(int index) {
    {
        std::lock_guard<std::mutex> lock(g_cache_mutex);
        if (index >= 0 && index < static_cast<int>(g_device_names.size())) {
            return g_device_names[index];
        }
    }

    std::vector<std::string> names;
    ScanDevices(&names);

    std::lock_guard<std::mutex> lock(g_cache_mutex);
    g_device_names = names;
    if (index >= 0 && index < static_cast<int>(names.size())) {
        return names[index];
    }
    return "en0";
}

}
