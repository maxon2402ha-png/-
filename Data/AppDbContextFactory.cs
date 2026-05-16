using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using КР_Ханников.Core;

namespace КР_Ханников.Data
{
                    public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();

                        var connectionString = Constants.Database.GetConnectionString();

                        optionsBuilder.UseNpgsql(connectionString);

            return new AppDbContext(optionsBuilder.Options);
        }
    }
}