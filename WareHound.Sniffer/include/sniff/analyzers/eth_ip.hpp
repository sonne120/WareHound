// libsniff/analyzers/eth_ip.hpp
// Minimal Ethernet + IPv4 + TCP/UDP header parser. Fills PacketView::flow.
//
// Deliberately tiny — no VLAN, no IPv6, no MPLS. Real implementations should
// use a state machine; for the skeleton it's a straight cast-and-extract.
#pragma once

#include "sniff/pipeline/stage.hpp"

#include <cstdint>
#include <cstring>

namespace sniff::analyzers {

// Reads a big-endian uint16 from an unaligned pointer.
inline uint16_t rd_be16(const uint8_t* p) noexcept {
    return static_cast<uint16_t>((uint16_t(p[0]) << 8) | p[1]);
}
inline uint32_t rd_be32(const uint8_t* p) noexcept {
    return (uint32_t(p[0]) << 24) | (uint32_t(p[1]) << 16)
         | (uint32_t(p[2]) << 8)  |  uint32_t(p[3]);
}

// Returns true if parsing succeeded and pv.flow is valid.
inline bool parse_eth_ipv4(pipeline::PacketView& pv) noexcept {
    if (pv.length < 14 + 20) return false; // Ethernet + min IPv4 header
    const uint8_t* p = pv.data;

    // Ethernet: dst[6] src[6] type[2]
    const uint16_t ethertype = rd_be16(p + 12);
    if (ethertype != 0x0800) return false; // only IPv4 for now

    const uint8_t* ip = p + 14;
    const uint8_t ver_ihl = ip[0];
    if ((ver_ihl >> 4) != 4) return false;
    const uint8_t ihl = (ver_ihl & 0x0F) * 4;
    if (ihl < 20 || pv.length < 14u + ihl) return false;

    const uint8_t proto = ip[9];
    const uint32_t src_ip = rd_be32(ip + 12);
    const uint32_t dst_ip = rd_be32(ip + 16);

    uint16_t src_port = 0, dst_port = 0;
    pipeline::L4Protocol l4 = static_cast<pipeline::L4Protocol>(proto);
    const uint8_t* l4hdr = ip + ihl;
    const std::size_t l4_avail = pv.length - 14u - ihl;

    if (proto == 6 && l4_avail >= 4) {        // TCP
        src_port = rd_be16(l4hdr + 0);
        dst_port = rd_be16(l4hdr + 2);
    } else if (proto == 17 && l4_avail >= 4) { // UDP
        src_port = rd_be16(l4hdr + 0);
        dst_port = rd_be16(l4hdr + 2);
    }

    pv.flow = flow::FlowKey::make(src_ip, src_port, dst_ip, dst_port, proto);
    pv.l4 = l4;
    pv.l4_offset = static_cast<uint16_t>(14 + ihl);
    pv.parsed = true;
    return true;
}

} // namespace sniff::analyzers
