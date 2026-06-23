#pragma once

#include <vector>
#include <string>

namespace sniff::capture {

class DeviceScanner {
public:
    
    static std::vector<std::string> ListDevices();
    
    static std::string GetDeviceNameAt(int index);
};

} 
