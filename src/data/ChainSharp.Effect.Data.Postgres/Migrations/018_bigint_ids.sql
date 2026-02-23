-- Migration 018: Widen all entity IDs and FKs from integer to bigint
--
-- WARNING: ALTER COLUMN TYPE from integer to bigint requires a table rewrite,
-- which acquires an ACCESS EXCLUSIVE lock. For tables with millions of rows,
-- this may take seconds to minutes. Plan for a maintenance window.
-- Run VACUUM ANALYZE on affected tables after migration.

-- ── metadata (identity column) ──────────────────────────────────────
ALTER TABLE chain_sharp.metadata ALTER COLUMN id TYPE bigint;
ALTER TABLE chain_sharp.metadata ALTER COLUMN parent_id TYPE bigint;
ALTER TABLE chain_sharp.metadata ALTER COLUMN manifest_id TYPE bigint;

-- ── log (identity column) ───────────────────────────────────────────
ALTER TABLE chain_sharp.log ALTER COLUMN id TYPE bigint;
ALTER TABLE chain_sharp.log ALTER COLUMN metadata_id TYPE bigint;

-- ── manifest (identity column) ──────────────────────────────────────
ALTER TABLE chain_sharp.manifest ALTER COLUMN id TYPE bigint;
ALTER TABLE chain_sharp.manifest ALTER COLUMN manifest_group_id TYPE bigint;
ALTER TABLE chain_sharp.manifest ALTER COLUMN depends_on_manifest_id TYPE bigint;

-- ── manifest_group (serial → also alter sequence) ───────────────────
ALTER TABLE chain_sharp.manifest_group ALTER COLUMN id TYPE bigint;
ALTER SEQUENCE chain_sharp.manifest_group_id_seq AS bigint;

-- ── work_queue (serial → also alter sequence) ───────────────────────
ALTER TABLE chain_sharp.work_queue ALTER COLUMN id TYPE bigint;
ALTER SEQUENCE chain_sharp.work_queue_id_seq AS bigint;
ALTER TABLE chain_sharp.work_queue ALTER COLUMN manifest_id TYPE bigint;
ALTER TABLE chain_sharp.work_queue ALTER COLUMN metadata_id TYPE bigint;

-- ── dead_letter (identity column) ───────────────────────────────────
ALTER TABLE chain_sharp.dead_letter ALTER COLUMN id TYPE bigint;
ALTER TABLE chain_sharp.dead_letter ALTER COLUMN manifest_id TYPE bigint;
ALTER TABLE chain_sharp.dead_letter ALTER COLUMN retry_metadata_id TYPE bigint;

-- ── background_job (id is already bigserial, only FK needs change) ──
ALTER TABLE chain_sharp.background_job ALTER COLUMN metadata_id TYPE bigint;
