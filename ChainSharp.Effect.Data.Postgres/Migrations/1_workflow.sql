create schema if not exists chain_sharp;

create type chain_sharp.workflow_state as ENUM (
    'pending',
    'completed',
    'failed',
    'in_progress'
);

create table chain_sharp.metadata
(
    id integer generated always as identity
        constraint workflow_pkey
            primary key,
    parent_id        integer
        CONSTRAINT workflow_workflow_id_fkey
            REFERENCES chain_sharp.metadata (id),
    external_id      char(32) not null,
    hangfire_job_id  varchar,
    name             varchar not null,
    executor         varchar,
    workflow_state            chain_sharp.workflow_state default 'pending'::chain_sharp.workflow_state not null,
    database_changes integer default 0 not null,
    failure_step     varchar,
    failure_reason   varchar,
    failure_exception   varchar,
    stack_trace      varchar,
    start_time timestamp with time zone not null,
    end_time timestamp with time zone
);

create unique index workflow_external_id_uindex
    on chain_sharp.metadata (external_id);

