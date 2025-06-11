using Microsoft.EntityFrameworkCore;

namespace ChainSharp.Effect.Data.Models.Log;

public class PersistentLog : Effect.Models.Log.Log
{
    internal static void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Effect.Models.Log.Log>(entity =>
        {
            entity.ToTable("log", "chain_sharp");
            entity.Property(e => e.Id).ValueGeneratedOnAdd();

            // entity
                // .HasOne(x => x.Metadata)
                // .WithMany(x => x.Logs)
                // .HasForeignKey(x => x.MetadataId)
                // .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
