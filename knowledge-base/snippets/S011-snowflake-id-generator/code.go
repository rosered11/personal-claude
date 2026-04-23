package snowflake

import (
	"sync"
	"time"
)

const (
	epoch        int64 = 1700000000000 // custom epoch (ms) — adjust per deployment
	machineBits        = 10
	sequenceBits       = 12
)

type Snowflake struct {
	mu        sync.Mutex
	lastMs    int64
	machineID int64
	sequence  int64
}

func New(machineID int64) *Snowflake {
	return &Snowflake{machineID: machineID & 0x3FF} // 10-bit mask
}

func (s *Snowflake) NextID() int64 {
	s.mu.Lock()
	defer s.mu.Unlock()

	now := time.Now().UnixMilli()
	if now == s.lastMs {
		s.sequence = (s.sequence + 1) & 0xFFF // 12-bit mask
		if s.sequence == 0 {
			for now <= s.lastMs {
				now = time.Now().UnixMilli()
			}
		}
	} else {
		s.sequence = 0
	}
	s.lastMs = now
	return (now-epoch)<<22 | s.machineID<<12 | s.sequence
}
