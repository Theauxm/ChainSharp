-- Change manifest external_id from char(32) to varchar
-- The external ID is no longer always a GUID and may vary in length
alter table chain_sharp.manifest
    alter column external_id type varchar;
