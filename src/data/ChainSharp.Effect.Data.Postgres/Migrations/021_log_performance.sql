-- Restore primary key (dropped by migration 004)
ALTER TABLE chain_sharp.log ADD CONSTRAINT log_pkey PRIMARY KEY (id);

-- Index for dashboard MetadataDetailPage and DeleteExpiredMetadataStep queries
CREATE INDEX ix_log_metadata_id ON chain_sharp.log (metadata_id);
