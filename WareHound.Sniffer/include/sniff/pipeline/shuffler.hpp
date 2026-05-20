// libsniff/pipeline/shuffler.hpp
// M x R matrix of SPSC queues sitting between mappers and reducers.
//
//   queues_[m * num_reducers + r] is the queue FROM mapper m TO reducer r.
//
// Each cell is a TRUE single-producer single-consumer queue:
//   - producer: exactly one mapper thread (id = m)
//   - consumer: exactly one reducer thread (id = r)
// So we can use the cheaper SPSC primitive (no CAS, no fences beyond
// acquire/release). This is the layout described in the user's design.
#pragma once

#include "sniff/core/spsc_queue.hpp"
#include "sniff/flow/flow_key.hpp"
#include "sniff/pipeline/stage.hpp"

#include <atomic>
#include <cstddef>
#include <memory>
#include <vector>

namespace sniff::pipeline {

class Shuffler {
public:
    Shuffler(std::size_t num_mappers,
             std::size_t num_reducers,
             std::size_t per_queue_capacity)
        : num_mappers_(num_mappers)
        , num_reducers_(num_reducers) {
        queues_.reserve(num_mappers_ * num_reducers_);
        for (std::size_t i = 0; i < num_mappers_ * num_reducers_; ++i) {
            queues_.emplace_back(
                std::make_unique<core::SpscQueue<PacketView>>(per_queue_capacity));
        }
    }

    std::size_t num_mappers() const noexcept { return num_mappers_; }
    std::size_t num_reducers() const noexcept { return num_reducers_; }

    // Producer side — called from mapper thread `mapper_id`.
    bool offer(std::size_t mapper_id, PacketView pv) noexcept {
        const std::size_t r =
            static_cast<std::size_t>(flow::hash_flow(pv.flow) % num_reducers_);
        auto& q = *queues_[mapper_id * num_reducers_ + r];
        const bool ok = q.try_push(pv);
        if (!ok) drops_.fetch_add(1, std::memory_order_relaxed);
        return ok;
    }

    // Consumer side — called from reducer thread `reducer_id`.
    // Scans mapper queues for this reducer in round-robin order; returns first
    // packet found. Updates poll_cursor_[reducer_id] so the next call starts
    // where the previous one left off (fair to all mappers).
    bool poll(std::size_t reducer_id, PacketView& out) noexcept {
        const std::size_t start = poll_cursor_[reducer_id];
        for (std::size_t i = 0; i < num_mappers_; ++i) {
            const std::size_t m = (start + i) % num_mappers_;
            auto& q = *queues_[m * num_reducers_ + reducer_id];
            if (q.try_pop(out)) {
                poll_cursor_[reducer_id] = (m + 1) % num_mappers_;
                return true;
            }
        }
        return false;
    }

    // Initialize per-reducer poll cursors. Must be called after construction
    // before any reducer threads start.
    void init_poll_cursors() {
        poll_cursor_.assign(num_reducers_, 0);
    }

    uint64_t drops() const noexcept {
        return drops_.load(std::memory_order_relaxed);
    }

private:
    const std::size_t num_mappers_;
    const std::size_t num_reducers_;
    std::vector<std::unique_ptr<core::SpscQueue<PacketView>>> queues_;
    std::vector<std::size_t> poll_cursor_; // reducer-private; only read/written
                                           // by reducer thread `r` at index r
    std::atomic<uint64_t> drops_{0};
};

} // namespace sniff::pipeline
