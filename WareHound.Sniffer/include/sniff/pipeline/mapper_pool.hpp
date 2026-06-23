#pragma once
#include "sniff/analyzers/eth_ip.hpp"
#include "sniff/core/mempool.hpp"
#include "sniff/core/ring_buffer.hpp"
#include "sniff/pipeline/shuffler.hpp"
#include "sniff/pipeline/stage.hpp"
#include "sniff/platform/affinity.hpp"

#include <atomic>
#include <memory>
#include <thread>
#include <vector>

namespace sniff::pipeline {

class MapperPool {
public:
    MapperPool(std::size_t num_workers,
               std::shared_ptr<core::SpmcRingBuffer<PacketView>> input,
               Shuffler& shuffler,
               core::BufferPool& pool)
        : num_workers_(num_workers)
        , input_(std::move(input))
        , shuffler_(shuffler)
        , pool_(pool) {}

    ~MapperPool() { stop(); }

    void start(const std::vector<int>& core_ids = {}) {
        running_.store(true, std::memory_order_release);
        workers_.reserve(num_workers_);
        for (std::size_t i = 0; i < num_workers_; ++i) {
            int core = (i < core_ids.size()) ? core_ids[i] : -1;
            workers_.emplace_back([this, i, core] {
                platform::set_current_thread_name(
                    "snf-map-" + std::to_string(i));
                if (core >= 0) platform::pin_current_thread_to_core(core);
                loop(i);
            });
        }
    }

    void stop() {
        if (!running_.exchange(false, std::memory_order_acq_rel)) return;
        for (auto& t : workers_) if (t.joinable()) t.join();
        workers_.clear();
    }

    uint64_t parsed() const noexcept { return parsed_.load(std::memory_order_relaxed); }
    uint64_t dropped_unparseable() const noexcept {
        return unparseable_.load(std::memory_order_relaxed);
    }

private:
    void loop(std::size_t worker_id) {
        PacketView pv;
        while (running_.load(std::memory_order_acquire)) {
            if (!input_->try_claim(worker_id, pv)) {
                platform::cpu_pause();
                continue;
            }
            if (analyzers::parse_eth_ipv4(pv)) {
                parsed_.fetch_add(1, std::memory_order_relaxed);
                if (!shuffler_.offer(worker_id, pv)) {
                    // Target SPSC was full — release buffer ourselves.
                    pool_.release(pv.data);
                }
            } else {
                unparseable_.fetch_add(1, std::memory_order_relaxed);
                pool_.release(pv.data);
            }
        }
    }

    std::size_t num_workers_;
    std::shared_ptr<core::SpmcRingBuffer<PacketView>> input_;
    Shuffler& shuffler_;
    core::BufferPool& pool_;
    std::vector<std::thread> workers_;
    std::atomic<bool> running_{false};

    std::atomic<uint64_t> parsed_{0};
    std::atomic<uint64_t> unparseable_{0};
};

} 
