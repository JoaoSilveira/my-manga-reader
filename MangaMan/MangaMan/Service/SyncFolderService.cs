using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;
using MangaMan.Models;

namespace MangaMan.Service;

public static class SyncFolderService
{
    public record Archive(string Path, string Name);

    public record Folder(string Path, string Name, List<Archive> Archives, List<Folder> Children);

    public static Folder ReadFolder(string path)
    {
        List<Archive> archives = [];
        List<Folder> children = [];

        var dirInfo = new DirectoryInfo(path);
        foreach (var item in dirInfo.GetFileSystemInfos())
        {
            switch (item)
            {
                case FileInfo file when ArchiveService.IsArchiveFile(file.FullName):
                    archives.Add(new Archive(file.FullName, Path.GetFileNameWithoutExtension(file.Name)));
                    break;
                case DirectoryInfo child:
                    var hasImages = child.GetFiles()
                        .Any(ArchiveService.IsImageFile);

                    if (hasImages)
                        archives.Add(new Archive(child.FullName, child.Name));

                    children.Add(ReadFolder(child.FullName));
                    break;
            }
        }

        return PruneFolder(new Folder(dirInfo.FullName, dirInfo.Name, archives, children));
    }

    public static async Task PersistFolderAsync(MangaManDbContext context, Folder folder,
        CancellationToken ct = default)
    {
        var currentDate = DateTime.Now;
        await PersistFolderAsync(context, folder, null, currentDate, ct);
    }

    private static async Task PersistFolderAsync(MangaManDbContext context, Folder folder, SyncFolder? parent,
        DateTime currentDate, CancellationToken ct = default)
    {
        var syncFolder = new SyncFolder()
        {
            Name = folder.Name,
            Path = folder.Path,
            CreatedAt = currentDate,
            LastSyncAt = currentDate,
            Parent = parent,
        };

        var archives = folder.Archives
            .Select(a => new MangaArchive()
            {
                Name = a.Name,
                Path = a.Path,
                CreatedAt = currentDate,
                SyncFolder = syncFolder,
                WasRead = false,
            })
            .ToList();

        await context.AddAsync(syncFolder, ct);
        await context.AddRangeAsync(archives, ct);

        foreach (var child in folder.Children)
        {
            await PersistFolderAsync(context, child, syncFolder, currentDate, ct);
        }
    }

    private static Folder PruneFolder(Folder folder)
    {
        return folder with
        {
            Children = folder.Children
                .Where(DescendentHasArchive)
                .Select(PruneFolder)
                .ToList(),
        };
    }

    private static bool DescendentHasArchive(Folder folder)
    {
        return folder.Archives.Count > 0 || folder.Children.Any(DescendentHasArchive);
    }
}