using Microsoft.EntityFrameworkCore;

namespace MangaMan.Models;

public class MangaManDbContext : DbContext
{
    public DbSet<SyncFolder> SyncFolders { get; set; }
    public DbSet<MangaArchive> MangaArchives { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseSqlite("Data Source=MangaMan.db;Foreign Keys=True");
    }
}