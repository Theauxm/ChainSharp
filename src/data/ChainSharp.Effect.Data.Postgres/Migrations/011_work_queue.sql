CREATE TYPE chain_sharp.work_queue_status AS ENUM ('queued', 'dispatched', 'cancelled');

CREATE TABLE chain_sharp.work_queue (
    id              serial PRIMARY KEY,
    external_id     varchar NOT NULL,
    workflow_name   varchar NOT NULL,
    input           jsonb,
    input_type_name varchar,
    status          chain_sharp.work_queue_status NOT NULL DEFAULT 'queued',
    manifest_id     int REFERENCES chain_sharp.manifest(id) ON DELETE RESTRICT,
    metadata_id     int REFERENCES chain_sharp.metadata(id) ON DELETE RESTRICT,
    created_at      timestamp NOT NULL DEFAULT (now() AT TIME ZONE 'utc'),
    dispatched_at   timestamp
);

CREATE UNIQUE INDEX ix_work_queue_external_id ON chain_sharp.work_queue (external_id);
CREATE INDEX ix_work_queue_status ON chain_sharp.work_queue (status) WHERE status = 'queued';
CREATE INDEX ix_work_queue_manifest_id ON chain_sharp.work_queue (manifest_id);
