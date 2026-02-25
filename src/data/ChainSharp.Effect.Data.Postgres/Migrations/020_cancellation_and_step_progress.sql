-- Cancellation signal column
ALTER TABLE chain_sharp.metadata ADD COLUMN IF NOT EXISTS cancel_requested boolean NOT NULL DEFAULT false;

-- Step progress columns
ALTER TABLE chain_sharp.metadata ADD COLUMN IF NOT EXISTS step_started_at timestamptz;
ALTER TABLE chain_sharp.metadata ADD COLUMN IF NOT EXISTS currently_running_step text;
