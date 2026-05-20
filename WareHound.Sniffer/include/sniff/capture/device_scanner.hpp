#pragma once

#include <vector>
#include <string>

namespace sniff::capture {

class DeviceScanner {
public:
    // Returns a list of formatted strings for the UI: "1_Intel(R) Ethernet..."
    static std::vector<std::string> ListDevices();
    
    // Maps a 0-based index to the actual system interface name: "eth0", "en0", etc.
    static std::string GetDeviceNameAt(int index);
};

} // namespace sniff::capture
