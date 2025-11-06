-- Asegura extensión (id por defecto)
CREATE EXTENSION IF NOT EXISTS pgcrypto;

CREATE TABLE IF NOT EXISTS public.outbox (
  id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  aggregate_type  TEXT NOT NULL DEFAULT 'transaction',
  aggregate_id    UUID NOT NULL,
  event_type      TEXT NOT NULL,
  payload         JSONB NOT NULL,
  status          TEXT  NOT NULL DEFAULT 'pending', -- pending | sent | failed
  retry_count     INT   NOT NULL DEFAULT 0,
  next_attempt_at TIMESTAMPTZ,
  created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  sent_at         TIMESTAMPTZ
);

-- Prioriza pendientes por fecha de intento y creación
CREATE INDEX IF NOT EXISTS idx_outbox_pending
  ON public.outbox (
    status,
    COALESCE(next_attempt_at, NOW()),
    created_at
  );
