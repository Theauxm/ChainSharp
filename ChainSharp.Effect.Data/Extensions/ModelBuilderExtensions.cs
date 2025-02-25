using System.Reflection;
using ChainSharp.Effect.Models;
using Microsoft.EntityFrameworkCore;

namespace ChainSharp.Effect.Data.Extensions;

public static class ModelBuilderExtensions
{
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
}
