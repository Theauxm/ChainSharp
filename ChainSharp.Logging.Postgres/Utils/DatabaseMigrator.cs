using DbUp;
using DbUp.Engine;

namespace ChainSharp.Logging.Postgres.Utils;

public class DatabaseMigrator
{
    public static UpgradeEngine CreateEngineWithEmbeddedScripts(string connectionString) =>
        DeployChanges
            .To.PostgresqlDatabase(connectionString)
            .JournalToPostgresqlTable("chain_sharp", "migrations")
            .WithScriptsEmbeddedInAssembly(typeof(AssemblyMarker).Assembly)
            .LogToConsole()
            .Build();
}
