create type chain_sharp.log_level as ENUM (
    'trace',
    'debug',
    'information',
    'warning',
    'error',
    'critical',
    'none'
);

create table chain_sharp.log
(
    id integer generated always as identity
        constraint log_pkey
            primary key,
    metadata_id        integer,
    event_id integer not null,
    level chain_sharp.log_level not null,
    message varchar not null,
    category varchar not null,
    exception varchar,
    stack_trace varchar
);
