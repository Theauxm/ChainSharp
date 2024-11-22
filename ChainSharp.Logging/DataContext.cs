using ChainSharp.Logging.Models;
using Microsoft.EntityFrameworkCore;

namespace ChainSharp.Logging;

public class DataContext : DbContext
{
    public DbSet<WorkflowMetadata> WorkflowMetadata { get; set; }
    
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        base.OnConfiguring(optionsBuilder);
        
        if (!optionsBuilder.IsConfigured)
            throw new InvalidOperationException("Database provider must be configured.");
    }
}