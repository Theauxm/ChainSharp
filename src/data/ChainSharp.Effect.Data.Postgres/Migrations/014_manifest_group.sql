-- 1. Create manifest_group table
CREATE TABLE chain_sharp.manifest_group (
    id serial PRIMARY KEY,
    name varchar NOT NULL,
    max_active_jobs int,
    priority smallint NOT NULL DEFAULT 0,
    is_enabled boolean NOT NULL DEFAULT true,
    created_at timestamp NOT NULL DEFAULT (now() AT TIME ZONE 'utc'),
    updated_at timestamp NOT NULL DEFAULT (now() AT TIME ZONE 'utc'),
    CONSTRAINT uq_manifest_group_name UNIQUE (name)
);

-- 2. Seed from existing group_id values (non-null)
INSERT INTO chain_sharp.manifest_group (name)
SELECT DISTINCT group_id FROM chain_sharp.manifest WHERE group_id IS NOT NULL;

-- 3. Seed auto-generated groups for ungrouped manifests (using external_id)
INSERT INTO chain_sharp.manifest_group (name)
SELECT DISTINCT external_id FROM chain_sharp.manifest WHERE group_id IS NULL;

-- 4. Set group priority from max manifest priority
UPDATE chain_sharp.manifest_group mg SET priority = sub.max_p
FROM (
    SELECT COALESCE(m.group_id, m.external_id) AS gname, MAX(m.priority) AS max_p
    FROM chain_sharp.manifest m GROUP BY 1
) sub WHERE mg.name = sub.gname AND sub.max_p > 0;

-- 5. Add FK column, populate, make NOT NULL
ALTER TABLE chain_sharp.manifest ADD COLUMN manifest_group_id int;

UPDATE chain_sharp.manifest m SET manifest_group_id = mg.id
FROM chain_sharp.manifest_group mg WHERE mg.name = COALESCE(m.group_id, m.external_id);

ALTER TABLE chain_sharp.manifest ALTER COLUMN manifest_group_id SET NOT NULL;
ALTER TABLE chain_sharp.manifest
    ADD CONSTRAINT fk_manifest_manifest_group
    FOREIGN KEY (manifest_group_id) REFERENCES chain_sharp.manifest_group(id);
CREATE INDEX ix_manifest_manifest_group_id ON chain_sharp.manifest (manifest_group_id);

-- 6. Drop old group_id column
DROP INDEX IF EXISTS chain_sharp.ix_manifest_group_id;
ALTER TABLE chain_sharp.manifest DROP COLUMN group_id;
