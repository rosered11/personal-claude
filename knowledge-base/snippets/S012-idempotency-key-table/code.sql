-- Idempotency key table
CREATE TABLE idempotency_keys (
    key         VARCHAR(255) PRIMARY KEY,
    response    JSONB NOT NULL,
    status_code INT NOT NULL,
    created_at  TIMESTAMP DEFAULT NOW()
);

-- Index for TTL cleanup job
CREATE INDEX idx_idempotency_created ON idempotency_keys (created_at);

-- Application logic (pseudocode):
-- 1. Check: SELECT response, status_code FROM idempotency_keys WHERE key = $1
-- 2. If found: return cached response (skip processing)
-- 3. Process request
-- 4. Insert: INSERT INTO idempotency_keys (key, response, status_code) VALUES ($1, $2, $3)
--    ON CONFLICT (key) DO NOTHING  -- handles concurrent retry race condition

-- Cleanup job (run daily via pg_cron or Airflow):
DELETE FROM idempotency_keys WHERE created_at < NOW() - INTERVAL '24 hours';
