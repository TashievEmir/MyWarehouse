using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Infrastructure.Persistence.Data;

public class DataContextFactory 
    : IDesignTimeDbContextFactory<DataContext>
{
    public DataContext CreateDbContext(string[] args)
    {
        var dbPath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "app.db");
        
        var options = new DbContextOptionsBuilder<DataContext>()
            .UseSqlite($"Data Source={dbPath}")
            .Options;

        return new DataContext(options);
    }
}