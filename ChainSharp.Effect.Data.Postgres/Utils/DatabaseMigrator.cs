using DbUp;
using DbUp.Engine;
using LanguageExt;
using Npgsql;

namespace ChainSharp.Effect.Data.Postgres.Utils;

public class DatabaseMigrator
{
    public static UpgradeEngine CreateEngineWithEmbeddedScripts(string connectionString) =>
        DeployChanges
            .To.PostgresqlDatabase(connectionString)
            .JournalToPostgresqlTable("chain_sharp", "migrations")
            .WithScriptsEmbeddedInAssembly(typeof(AssemblyMarker).Assembly)
            .LogToConsole()
            .Build();

    public static async Task Migrate(string connectionString)
    {
        try
        {
            var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();
            await connection.ReloadTypesAsync();

            var command = new NpgsqlCommand("create schema if not exists chain_sharp;", connection);
            await command.ExecuteNonQueryAsync();

            var result = CreateEngineWithEmbeddedScripts(connectionString).PerformUpgrade();

            if (result.Successful == false)
                result.Error.Rethrow();
        }
        catch (Exception e)
        {
            Console.WriteLine(
                $"Caught Exception ({e.GetType()}) while attempting to migrate ChainSharp database: {e}"
            );
            throw;
        }
    }
}
