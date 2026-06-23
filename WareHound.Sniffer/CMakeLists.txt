# CMakeLists.txt for WareHound.Sniffer (adapted from libsniff)
cmake_minimum_required(VERSION 3.16)
project(WareHound.Sniffer CXX)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_CXX_EXTENSIONS OFF)
set(CMAKE_POSITION_INDEPENDENT_CODE ON)

if(NOT CMAKE_BUILD_TYPE)
    set(CMAKE_BUILD_TYPE Release CACHE STRING "" FORCE)
endif()

# ---- Warnings & per-compiler tuning ----
if(MSVC)
    add_compile_options(/W4 /permissive- /Zc:__cplusplus)
    add_compile_definitions(_CRT_SECURE_NO_WARNINGS)
else()
    add_compile_options(-Wall -Wextra -Wpedantic -Wno-unused-parameter)
endif()

# ---- Find libpcap / Npcap ----
find_package(PkgConfig QUIET)
if(PkgConfig_FOUND)
    pkg_check_modules(PCAP IMPORTED_TARGET libpcap)
endif()

if(WIN32)
    if(NOT DEFINED NPCAP_SDK AND DEFINED ENV{NPCAP_SDK})
        set(NPCAP_SDK $ENV{NPCAP_SDK})
    endif()
    # Fallback: use the bundled WpdPack in the repo
    set(_WPDPACK_FALLBACK "${CMAKE_CURRENT_SOURCE_DIR}/../header/WpdPack/WpdPack")
    if(NOT NPCAP_SDK AND EXISTS "${_WPDPACK_FALLBACK}/Include")
        message(STATUS "NPCAP_SDK not set — using bundled WpdPack at ${_WPDPACK_FALLBACK}")
        set(PCAP_INCLUDE_DIRS "${_WPDPACK_FALLBACK}/Include")
        if(CMAKE_SIZEOF_VOID_P EQUAL 8)
            set(PCAP_LIBRARY "${_WPDPACK_FALLBACK}/Lib/x64/wpcap.lib")
        else()
            set(PCAP_LIBRARY "${_WPDPACK_FALLBACK}/Lib/wpcap.lib")
        endif()
    elseif(NPCAP_SDK)
        set(PCAP_INCLUDE_DIRS "${NPCAP_SDK}/Include")
        if(CMAKE_SIZEOF_VOID_P EQUAL 8)
            set(PCAP_LIBRARY "${NPCAP_SDK}/Lib/x64/wpcap.lib")
        else()
            set(PCAP_LIBRARY "${NPCAP_SDK}/Lib/wpcap.lib")
        endif()
    else()
        message(WARNING
            "NPCAP_SDK not set and bundled WpdPack not found. "
            "Pass -DNPCAP_SDK=<path> to cmake.")
    endif()
    set(WS2_32 ws2_32.lib)
else()
    if(NOT PCAP_FOUND)
        find_path(PCAP_INCLUDE_DIR NAMES pcap/pcap.h pcap.h PATHS /usr/local/include /opt/homebrew/include)
        find_library(PCAP_LIBRARY NAMES pcap PATHS /usr/local/lib /opt/homebrew/lib)
        if(PCAP_INCLUDE_DIR AND PCAP_LIBRARY)
            set(PCAP_FOUND TRUE)
            set(PCAP_INCLUDE_DIRS ${PCAP_INCLUDE_DIR})
        endif()
    endif()
    if(NOT PCAP_FOUND AND NOT PCAP_INCLUDE_DIRS)
        message(FATAL_ERROR
            "libpcap not found. Install libpcap-dev (Debian/Ubuntu) "
            "or libpcap-devel (Fedora) or 'brew install libpcap' (macOS).")
    endif()
endif()

# ---- The library ----
add_library(WareHound.Sniffer SHARED
    src/capture/device_scanner.cpp
    src/pcap_source.cpp
    src/api/ipc_pipe.cpp
    src/api/ipc.cpp
    src/api/statistic_exports.cpp
)

# Set output name based on platform
if(APPLE)
    set_target_properties(WareHound.Sniffer PROPERTIES
        OUTPUT_NAME "WareHound.Sniffer"
        PREFIX "lib"
        SUFFIX ".dylib"
    )
elseif(UNIX)
    set_target_properties(WareHound.Sniffer PROPERTIES
        OUTPUT_NAME "WareHound.Sniffer"
        PREFIX "lib"
        SUFFIX ".so"
    )
elseif(WIN32)
    set_target_properties(WareHound.Sniffer PROPERTIES
        OUTPUT_NAME "WareHound.Sniffer"
        SUFFIX ".dll"
    )
endif()

target_include_directories(WareHound.Sniffer
    PUBLIC
        $<BUILD_INTERFACE:${CMAKE_CURRENT_SOURCE_DIR}/include>
        $<INSTALL_INTERFACE:include>
)

if(WIN32)
    if(PCAP_INCLUDE_DIRS)
        target_include_directories(WareHound.Sniffer PRIVATE ${PCAP_INCLUDE_DIRS})
    endif()
    if(PCAP_LIBRARY)
        target_link_libraries(WareHound.Sniffer PUBLIC ${PCAP_LIBRARY} ${WS2_32})
    endif()
    target_compile_definitions(WareHound.Sniffer PUBLIC WPCAP HAVE_REMOTE)
    
    # Export all symbols on Windows
    set_target_properties(WareHound.Sniffer PROPERTIES WINDOWS_EXPORT_ALL_SYMBOLS ON)
    target_compile_definitions(WareHound.Sniffer PRIVATE
        _WINDOWS
        _USRDLL
        WAREHOUND_SNIFFER_EXPORTS
    )
else()
    if(TARGET PkgConfig::PCAP)
        target_link_libraries(WareHound.Sniffer PUBLIC PkgConfig::PCAP)
    else()
        target_include_directories(WareHound.Sniffer PRIVATE ${PCAP_INCLUDE_DIRS})
        target_link_libraries(WareHound.Sniffer PUBLIC ${PCAP_LIBRARY})
    endif()
endif()

# Threads
find_package(Threads REQUIRED)
target_link_libraries(WareHound.Sniffer PUBLIC Threads::Threads)

# ---- Example executable ----
add_executable(simple_sniff examples/simple_sniff.cpp)
target_link_libraries(simple_sniff PRIVATE WareHound.Sniffer)
set_target_properties(simple_sniff PROPERTIES
    RUNTIME_OUTPUT_DIRECTORY "${CMAKE_BINARY_DIR}"
)
