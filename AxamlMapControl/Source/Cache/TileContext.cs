using System.IO;
using Microsoft.EntityFrameworkCore;

namespace AxamlMapControl.Source.Cache
{
    /// <summary>
    /// Database tile context.
    /// </summary>
    public class TileContext : DbContext
    {
        public static readonly string DefaultDataSource = Path.Combine(Directory.GetCurrentDirectory(), "sqlite_cache.db");
        public string DataSource { get; }

        public DbSet<Tile> Tiles { get; set; } = default!;

        public TileContext(string dataSource)
        {
            DataSource = dataSource;
            Database.EnsureCreated();
        }

        public TileContext() : this(DefaultDataSource)
        {

        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlite($"Data Source={DataSource}");
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Tile>().HasKey(p => new { p.Zoom, p.X, p.Y });
        }
    }
}