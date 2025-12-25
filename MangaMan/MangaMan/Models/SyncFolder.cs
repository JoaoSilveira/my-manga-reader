using System;
using System.Collections.Generic;

namespace MangaMan.Models;

public class SyncFolder
{
    public Guid Id { get; set; }
    public Guid? ParentId { get; set; }
    public required string Name { get; set; }
    public required string Path { get; set; }
    public required DateTime CreatedAt { get; set; }
    public required DateTime LastSyncAt { get; set; }

    public SyncFolder? Parent { get; set; }
    public ICollection<SyncFolder> Children { get; set; } = [];
    public ICollection<MangaArchive> Entries { get; set; } = [];
}