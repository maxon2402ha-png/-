using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using КР_Ханников.Core;

namespace КР_Ханников.Data
{
    /// <summary>
    /// Фабрика для создания контекста во время разработки (design-time).
    /// Используется командами 'dotnet ef' для создания миграций.
    /// </summary>
    public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();

            // ИЗМЕНЕНИЕ: Получаем строку подключения к PostgreSQL
            var connectionString = Constants.Database.GetConnectionString();

            // ИЗМЕНЕНИЕ: Используем провайдер Npgsql вместо SQLite
            optionsBuilder.UseNpgsql(connectionString);

            return new AppDbContext(optionsBuilder.Options);
        }
    }
}