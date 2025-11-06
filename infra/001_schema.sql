-- Habilita función gen_random_uuid()
CREATE EXTENSION IF NOT EXISTS pgcrypto;

CREATE TABLE IF NOT EXISTS public.transactions (
  id                SERIAL PRIMARY KEY,
  external_id       UUID        NOT NULL UNIQUE,
  source_account_id UUID,
  target_account_id UUID,
  transfer_type_id  INT         NOT NULL,
  value             NUMERIC(14,2) NOT NULL,
  status            VARCHAR(16) NOT NULL DEFAULT 'pending',
  created_at        TIMESTAMP    NOT NULL DEFAULT NOW(),
  updated_at        TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
  idempotency_key   TEXT
);

-- Índices útiles
CREATE UNIQUE INDEX IF NOT EXISTS uq_transactions_idem
  ON public.transactions(idempotency_key)
  WHERE idempotency_key IS NOT NULL;

CREATE INDEX IF NOT EXISTS ix_tx_created_at
  ON public.transactions(created_at);

CREATE INDEX IF NOT EXISTS ix_tx_source_date
  ON public.transactions(source_account_id, created_at);
