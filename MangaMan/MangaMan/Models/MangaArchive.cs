using System;

namespace MangaMan.Models;

public class MangaArchive
{
    public Guid Id { get; set; }
    public Guid SyncFolderId { get; set; }
    public required string Name { get; set; }
    public required string Path { get; set; }
    public required DateTime CreatedAt { get; set; }
    public DateTime? LastOpenedAt { get; set; }
    public bool WasRead { get; set; }

    public required SyncFolder SyncFolder { get; set; }
}