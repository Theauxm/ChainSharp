-- Add 'dependent' value to schedule_type enum for manifests that trigger after a parent succeeds
ALTER TYPE chain_sharp.schedule_type ADD VALUE IF NOT EXISTS 'dependent';

-- Add self-referencing FK column for dependent manifest relationships
ALTER TABLE chain_sharp.manifest
    ADD COLUMN depends_on_manifest_id int REFERENCES chain_sharp.manifest(id) ON DELETE SET NULL;

-- Partial index for efficient lookup of dependent manifests
CREATE INDEX ix_manifest_depends_on
    ON chain_sharp.manifest (depends_on_manifest_id) WHERE depends_on_manifest_id IS NOT NULL;
