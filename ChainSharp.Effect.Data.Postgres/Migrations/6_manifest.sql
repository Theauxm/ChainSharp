-- Create the manifest table to store workflow manifest information
-- This table stores configuration and property information for workflows
create table if not exists chain_sharp.manifest
(
    -- Primary key with auto-incrementing ID
    id integer generated always as identity
        constraint manifest_pkey
            primary key,
    
    -- External identifier for the manifest (used for external lookups)
    external_id char(32) not null,
    
    -- Name of the type this manifest represents (typically the full type name)
    name varchar not null,
    
    -- Full type name of the property serializer type
    property_type varchar,
    
    -- JSON properties stored as JSONB for flexible configuration
    properties jsonb
);

-- Create index on external_id for faster lookups
create index if not exists manifest_external_id_idx 
    on chain_sharp.manifest (external_id);

-- Create index on name for faster lookups by type
create index if not exists manifest_name_idx 
    on chain_sharp.manifest (name);
