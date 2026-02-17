using System.Reflection;
using ChainSharp.Effect.Data.Postgres.Utils;
using ChainSharp.Effect.Enums;
using ChainSharp.Effect.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace ChainSharp.Effect.Data.Postgres.Extensions;

/// <summary>
/// Provides extension methods for configuring Entity Framework Core model builders for PostgreSQL in the ChainSharp.Effect.Data.Postgres system.
/// </summary>
/// <remarks>
/// The ModelBuilderExtensions class contains utility methods that simplify the configuration
/// of Entity Framework Core models for PostgreSQL in the ChainSharp.Effect.Data.Postgres system.
///
/// These extensions enable:
/// 1. Mapping .NET enums to PostgreSQL enum types
/// 2. Building properly configured data sources
/// 3. Ensuring consistent UTC date/time handling
///
/// By using these extensions, the system can leverage PostgreSQL-specific features
/// while maintaining a clean separation of concerns.
/// </remarks>
public static class ModelBuilderExtensions
{
    /// <summary>
    /// Adds PostgreSQL enum mappings to the model builder.
    /// </summary>
    /// <param name="modelBuilder">The model builder to configure</param>
    /// <returns>The configured model builder for method chaining</returns>
    /// <remarks>
    /// This method configures the model builder to map .NET enums to PostgreSQL enum types.
    /// It specifically maps the WorkflowState and LogLevel enums, which are used throughout
    /// the ChainSharp.Effect system.
    ///
    /// PostgreSQL enum types provide type safety at the database level and can improve
    /// performance compared to storing enum values as strings or integers.
    ///
    /// This method is typically called from the OnModelCreating method of the PostgresContext.
    /// </remarks>
    public static ModelBuilder AddPostgresEnums(this ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresEnum<WorkflowState>(schema: "chain_sharp");
        modelBuilder.HasPostgresEnum<LogLevel>(schema: "chain_sharp");
        modelBuilder.HasPostgresEnum<ScheduleType>(schema: "chain_sharp");
        modelBuilder.HasPostgresEnum<DeadLetterStatus>(schema: "chain_sharp");
        modelBuilder.HasPostgresEnum<WorkQueueStatus>(schema: "chain_sharp");

        return modelBuilder;
    }

    /// <summary>
    /// Builds a PostgreSQL data source with enum mappings.
    /// </summary>
    /// <param name="connectionString">The connection string to use</param>
    /// <returns>A configured NpgsqlDataSource</returns>
    /// <remarks>
    /// This method creates a new NpgsqlDataSource with the specified connection string
    /// and configures it with the necessary enum mappings for the ChainSharp.Effect system.
    ///
    /// The data source is configured to map the WorkflowState and LogLevel enums,
    /// ensuring that they are properly handled when reading from and writing to the database.
    ///
    /// This method is typically called when setting up the DbContextFactory in the
    /// AddPostgresEffect extension method.
    /// </remarks>
    public static NpgsqlDataSource BuildDataSource(string connectionString)
    {
        var npgsqlDataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);

        npgsqlDataSourceBuilder.MapEnum<WorkflowState>("chain_sharp.workflow_state");
        npgsqlDataSourceBuilder.MapEnum<LogLevel>("chain_sharp.log_level");
        npgsqlDataSourceBuilder.MapEnum<ScheduleType>("chain_sharp.schedule_type");
        npgsqlDataSourceBuilder.MapEnum<DeadLetterStatus>("chain_sharp.dead_letter_status");
        npgsqlDataSourceBuilder.MapEnum<WorkQueueStatus>("chain_sharp.work_queue_status");

        return npgsqlDataSourceBuilder.Build();
    }

    /// <summary>
    /// Applies a UTC date/time converter to all DateTime properties in the model.
    /// </summary>
    /// <param name="builder">The model builder to configure</param>
    /// <remarks>
    /// This method configures the model builder to use the UtcValueConverter for all
    /// DateTime and nullable DateTime properties in the model.
    ///
    /// The UtcValueConverter ensures that all DateTime values are stored and retrieved
    /// with the UTC kind, preventing timezone-related issues when working with dates and times.
    ///
    /// This is particularly important for distributed systems and applications that
    /// need to handle dates and times consistently across different timezones.
    ///
    /// This method is typically called from the OnModelCreating method of the PostgresContext.
    /// </remarks>
    public static void ApplyUtcDateTimeConverter(this ModelBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        foreach (var entityType in builder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                if (property.ClrType == typeof(DateTime) || property.ClrType == typeof(DateTime?))
                    property.SetValueConverter(new UtcValueConverter());
            }
        }
    }
}
