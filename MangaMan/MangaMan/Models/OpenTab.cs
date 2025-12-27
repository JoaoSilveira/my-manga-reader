using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MangaMan.Models;

public class OpenTab
{
    [Key] public Guid MangaArchiveId { get; set; }
    public required string CurrentPage { get; set; }

    [ForeignKey(nameof(MangaArchiveId))] public MangaArchive MangaArchive { get; set; } = null!;
}