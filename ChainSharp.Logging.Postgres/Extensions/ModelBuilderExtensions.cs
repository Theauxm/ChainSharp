using System.Reflection;
using ChainSharp.Logging.Enums;
using ChainSharp.Logging.Models;
using ChainSharp.Logging.Postgres.Utils;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ChainSharp.Logging.Postgres.Extensions;

public static class ModelBuilderExtensions
{
    public static ModelBuilder AddPostgresEnums(this ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresEnum<WorkflowState>();

        return modelBuilder;
    }

    public static NpgsqlDataSource BuildDataSource(string connectionString)
    {
        var npgsqlDataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);

        npgsqlDataSourceBuilder.MapEnum<WorkflowState>();

        return npgsqlDataSourceBuilder.Build();
    }

    public static ModelBuilder ApplyEntityOnModelCreating(this ModelBuilder modelBuilder)
    {
        var entityTypes = typeof(AssemblyMarker)
            .Assembly.GetTypes()
            .Where(
                t => typeof(IModel).IsAssignableFrom(t) && t is { IsAbstract: false, IsClass: true }
            )
            .ToList();

        foreach (var entityType in entityTypes)
        {
            var method =
                entityType.GetMethod(
                    "OnModelCreating",
                    BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static
                ) ?? throw new Exception($"Could not find OnModelCreating on {entityType.Name}");

            var parameters = method.GetParameters();
            if (parameters.Length != 1 || parameters[0].ParameterType != typeof(ModelBuilder))
                throw new Exception($"{entityType.Name}.OnModelCreating is the wrong signature");

            method.Invoke(null, [modelBuilder]);
        }

        return modelBuilder;
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
