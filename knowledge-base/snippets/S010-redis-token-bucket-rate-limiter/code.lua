-- Redis Token Bucket Rate Limiter
-- Execute via: EVAL <script> 1 <bucket_key> <capacity> <refill_rate> <current_timestamp_ms>
-- Returns: 1 = allowed, 0 = rejected

local key         = KEYS[1]
local capacity    = tonumber(ARGV[1])
local refill_rate = tonumber(ARGV[2])
local now         = tonumber(ARGV[3])

local bucket    = redis.call('HMGET', key, 'tokens', 'last_refill')
local tokens    = tonumber(bucket[1]) or capacity
local last_refill = tonumber(bucket[2]) or now

-- Refill tokens based on elapsed time
local elapsed   = now - last_refill
local new_tokens = math.min(capacity, tokens + elapsed * refill_rate)

if new_tokens >= 1 then
    redis.call('HMSET', key, 'tokens', new_tokens - 1, 'last_refill', now)
    redis.call('EXPIRE', key, math.ceil(capacity / refill_rate) + 1)
    return 1  -- allowed
else
    return 0  -- rejected
end
