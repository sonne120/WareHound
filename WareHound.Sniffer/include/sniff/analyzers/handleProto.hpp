#pragma once

#include "sniff/pipeline/stage.hpp"

namespace sniff::analyzers {

class HandleProto {
public:
    static const char* get_name(pipeline::L4Protocol proto) {
        switch (proto) {
            case pipeline::L4Protocol::Unknown:      return "HOPOPT";
            case pipeline::L4Protocol::Icmp:         return "ICMP";
            case pipeline::L4Protocol::Igmp:         return "IGMP";
            case pipeline::L4Protocol::Ggp:          return "GGP";
            case pipeline::L4Protocol::Ipv4:         return "IPV4";
            case pipeline::L4Protocol::St:           return "ST";
            case pipeline::L4Protocol::Tcp:          return "TCP";
            case pipeline::L4Protocol::Cbt:          return "CBT";
            case pipeline::L4Protocol::Egp:          return "EGP";
            case pipeline::L4Protocol::Igp:          return "IGP";
            case pipeline::L4Protocol::Pup:          return "PUP";
            case pipeline::L4Protocol::Emcon:        return "EMCON";
            case pipeline::L4Protocol::Chaos:        return "CHAOS";
            case pipeline::L4Protocol::Udp:          return "UDP";
            case pipeline::L4Protocol::Mux:          return "MUX";
            case pipeline::L4Protocol::Hmp:          return "HMP";
            case pipeline::L4Protocol::Prm:          return "PRM";
            case pipeline::L4Protocol::Idp:          return "IDP";
            case pipeline::L4Protocol::Trunk1:       return "TRUNK1";
            case pipeline::L4Protocol::Trunk2:       return "TRUNK2";
            case pipeline::L4Protocol::Leaf1:        return "LEAF1";
            case pipeline::L4Protocol::Rdp:          return "RDP";
            case pipeline::L4Protocol::Irtp:         return "IRTP";
            case pipeline::L4Protocol::IsoTp4:       return "ISO-TP4";
            case pipeline::L4Protocol::Netblt:       return "NETBLT";
            case pipeline::L4Protocol::MfeNsp:       return "MFE-NSP";
            case pipeline::L4Protocol::MeritInp:     return "MERIT-INP";
            case pipeline::L4Protocol::Dccp:         return "DCCP";
            case pipeline::L4Protocol::ThreePc:      return "3PC";
            case pipeline::L4Protocol::Idpr:         return "IDPR";
            case pipeline::L4Protocol::Xtp:          return "XTP";
            case pipeline::L4Protocol::Ddp:          return "DDP";
            case pipeline::L4Protocol::IdprCmtp:     return "IDPR-CMTP";
            case pipeline::L4Protocol::Tp:           return "TP";
            case pipeline::L4Protocol::Il:           return "IL";
            case pipeline::L4Protocol::Ipv6:         return "IPV6";
            case pipeline::L4Protocol::Sdrp:         return "SDRP";
            case pipeline::L4Protocol::Routing:      return "ROUTING";
            case pipeline::L4Protocol::Fragment:     return "FRAGMENT";
            case pipeline::L4Protocol::Idrp:         return "IDRP";
            case pipeline::L4Protocol::Rsvp:         return "RSVP";
            case pipeline::L4Protocol::Gre:          return "GRE";
            case pipeline::L4Protocol::Esp:          return "ESP";
            case pipeline::L4Protocol::Ah:           return "AH";
            case pipeline::L4Protocol::Mobile:       return "MOBILE";
            case pipeline::L4Protocol::Tlsp:         return "TLSP";
            case pipeline::L4Protocol::Skip:         return "SKIP";
            case pipeline::L4Protocol::Icmpv6:       return "ICMPV6";
            case pipeline::L4Protocol::None:         return "NONE";
            case pipeline::L4Protocol::Dstopts:      return "DSTOPTS";
            case pipeline::L4Protocol::Nd:           return "ND";
            case pipeline::L4Protocol::Eigrp:        return "EIGRP";
            case pipeline::L4Protocol::Ospf:         return "OSPF";
            case pipeline::L4Protocol::Ipip:         return "IPIP";
            case pipeline::L4Protocol::Encap:        return "ENCAP";
            case pipeline::L4Protocol::Pim:          return "PIM";
            case pipeline::L4Protocol::Ipcomp:       return "IPCOMP";
            case pipeline::L4Protocol::Vrrp:         return "VRRP";
            case pipeline::L4Protocol::Pgm:          return "PGM";
            case pipeline::L4Protocol::L2tp:         return "L2TP";
            case pipeline::L4Protocol::Stp:          return "STP";
            case pipeline::L4Protocol::Smp:          return "SMP";
            case pipeline::L4Protocol::Isis:         return "ISIS";
            case pipeline::L4Protocol::Sscopmce:     return "SSCOPMCE";
            case pipeline::L4Protocol::Sctp:         return "SCTP";
            case pipeline::L4Protocol::Fc:           return "FC";
            case pipeline::L4Protocol::Mh:           return "MH";
            case pipeline::L4Protocol::Udplite:      return "UDPLITE";
            case pipeline::L4Protocol::Mpls:         return "MPLS";
            case pipeline::L4Protocol::Manet:        return "MANET";
            case pipeline::L4Protocol::Hip:          return "HIP";
            case pipeline::L4Protocol::Shim6:        return "SHIM6";
            case pipeline::L4Protocol::Wesp:         return "WESP";
            case pipeline::L4Protocol::Rohc:         return "ROHC";
            case pipeline::L4Protocol::Ethernet:     return "ETHERNET";
            case pipeline::L4Protocol::Unassigned:   return "UNASSIGNED";
            case pipeline::L4Protocol::Experimental: return "EXPERIMENTAL";
            case pipeline::L4Protocol::Experimental2:return "EXPERIMENTAL2";
            default:                                 return "OTHER";
        }
    }
};

} // namespace sniff::analyzers