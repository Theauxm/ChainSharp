-- Define the log level enum type
-- This enum represents the standard logging levels used in .NET
-- Using a PostgreSQL enum type provides type safety at the database level
create type chain_sharp.log_level as ENUM (
    'trace',       -- Most detailed information, typically only valuable when debugging
    'debug',       -- Detailed information on application flow, typically only valuable during development
    'information', -- General information about application progress
    'warning',     -- Potentially harmful situations that might lead to an error
    'error',       -- Error events that might still allow the application to continue running
    'critical',    -- Very severe error events that might cause the application to terminate
    'none'         -- Not used for actual logging, specifies that no events should be logged
);

-- Create the log table to store detailed logging information
-- This table stores log entries generated during workflow execution
create table chain_sharp.log
(
    -- Primary key with auto-incrementing ID
    id integer generated always as identity
        constraint log_pkey
            primary key,
    
    -- Reference to the workflow metadata this log entry belongs to
    -- This allows filtering logs by workflow execution
    metadata_id integer,
    
    -- Event ID for grouping related log entries
    event_id integer not null,
    
    -- Severity level of the log entry
    level chain_sharp.log_level not null,
    
    -- The actual log message
    message varchar not null,
    
    -- Category/source of the log (typically the class name)
    category varchar not null,
    
    -- Exception details if this log entry represents an error
    exception varchar,
    
    -- Stack trace for debugging error logs
    stack_trace varchar
);
