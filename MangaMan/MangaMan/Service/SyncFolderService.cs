using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;

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
                case FileInfo file when ArchiveService.IsFileArchive(file.FullName):
                    archives.Add(new Archive(file.FullName, file.Name));
                    break;
                case DirectoryInfo child:
                    children.Add(ReadFolder(child.FullName));
                    break;
            }
        }

        return PruneFolder(new Folder(dirInfo.FullName, dirInfo.Name, archives, children));
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