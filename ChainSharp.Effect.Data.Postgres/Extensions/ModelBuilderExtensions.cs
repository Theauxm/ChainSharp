using System.Reflection;
using ChainSharp.Effect.Data.Postgres.Utils;
using ChainSharp.Effect.Enums;
using ChainSharp.Effect.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace ChainSharp.Effect.Data.Postgres.Extensions;

public static class ModelBuilderExtensions
{
    public static ModelBuilder AddPostgresEnums(this ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresEnum<WorkflowState>();
        modelBuilder.HasPostgresEnum<LogLevel>();

        return modelBuilder;
    }

    public static NpgsqlDataSource BuildDataSource(string connectionString)
    {
        var npgsqlDataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);

        npgsqlDataSourceBuilder.MapEnum<WorkflowState>();
        npgsqlDataSourceBuilder.MapEnum<LogLevel>();

        return npgsqlDataSourceBuilder.Build();
    }

    public static void ApplyUtcDateTimeConverter(this ModelBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        foreach (var entityType in builder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                if (property.ClrType == typeof(DateTime) || property.ClrType == typeof(DateTime?))
                    property.SetValueConverter(typeof(UtcValueConverter));
            }
        }
    }
}
