using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;
using Microsoft.EntityFrameworkCore.Metadata;

namespace MangaMan.Service;

public static class ArchiveService
{
    private static readonly string[] ArchiveExtensions = [".zip", ".cbz"];
    private static readonly string[] ImageExtensions = [".jpg", ".jpeg", ".png", ".webp"];

    public static IArchiveReader OpenArchive(string path)
    {
        return IsArchiveFile(path)
            ? ZipArchiveReader.Create(path)
            : FolderArchiveReader.Create(path);
    }

    internal static bool IsArchiveFile(string path) =>
        ArchiveExtensions.Contains(Path.GetExtension(path).ToLowerInvariant());

    internal static bool IsArchiveFile(FileInfo file) => ArchiveExtensions.Contains(file.Extension.ToLowerInvariant());

    internal static bool IsImageFile(string path) =>
        ImageExtensions.Contains(Path.GetExtension(path).ToLowerInvariant());

    internal static bool IsImageFile(FileInfo file) =>
        ImageExtensions.Contains(file.Extension.ToLowerInvariant());
}

public class ArchiveFolder
{
    public required string Name { get; init; }
    public HashSet<ArchiveFile> Files { get; set; } = [];
    public HashSet<ArchiveFolder> Folders { get; set; } = [];

    public override bool Equals(object? obj)
    {
        return Name.Equals((obj as ArchiveFolder)?.Name);
    }

    public override int GetHashCode()
    {
        return Name.GetHashCode();
    }
}

public record ArchiveFile(string Name, string Path)
{
    public bool IsImage { get; } = ArchiveService.IsImageFile(Name);
}

public interface IArchiveReader
{
    public IReadOnlyCollection<string> Images { get; }

    public Task<byte[]?> ReadAllBytesAsync(string path, CancellationToken cancellationToken = default);

    public ArchiveFolder ReadFolderTree();
}

public class ZipArchiveReader : IArchiveReader, IDisposable, IAsyncDisposable
{
    private readonly ZipArchive _archive;
    private readonly List<string> _images;

    private ZipArchiveReader(ZipArchive archive, List<string> images)
    {
        _archive = archive;
        _images = images;
    }

    public static ZipArchiveReader Create(string path)
    {
        var archive = new ZipArchive(File.OpenRead(path), ZipArchiveMode.Read);
        var images = archive.Entries
            .Where(entry => ArchiveService.IsImageFile(entry.Name))
            .Select(entry => entry.FullName)
            .Order()
            .ToList();

        return new ZipArchiveReader(archive, images);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _archive.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
        await _archive.DisposeAsync();
    }

    IReadOnlyCollection<string> IArchiveReader.Images => _images;

    public async Task<byte[]?> ReadAllBytesAsync(string path, CancellationToken cancellationToken = default)
    {
        if (!_images.Contains(path))
            return null;

        var entry = _archive.GetEntry(path);
        if (entry is null)
            return null;

        // zip does not handle async very well
        var bytes = new byte[entry.Length];
        // ReSharper disable once MethodHasAsyncOverloadWithCancellation
        await using var stream = entry.Open();
        // ReSharper disable once MethodHasAsyncOverloadWithCancellation
        stream.ReadExactly(bytes, 0, bytes.Length);

        return await Task.FromResult(bytes);
    }

    public ArchiveFolder ReadFolderTree()
    {
        var root = new ArchiveFolder() { Name = "" };
        foreach (var entry in _archive.Entries)
        {
            var folder = root;
            foreach (var part in entry.FullName.SplitAny(['/', '\\']))
            {
                // last part is a folder (entry ending with '/')
                if (part.Start.Value == part.End.Value)
                    continue;

                // last part and does not ends with '/' (is not a folder)
                if (part.End.Value == entry.FullName.Length)
                {
                    folder.Files.Add(new ArchiveFile(entry.FullName[part], entry.FullName));
                    continue;
                }

                folder = folder.Folders.FirstOrDefault(f => f.Name == entry.FullName[part])
                         ?? new ArchiveFolder() { Name = entry.FullName[part] };
            }
        }

        root.Files = root.Files.OrderBy(f => f.Name).ToHashSet();
        root.Files = root.Files.OrderBy(f => f.Name).ToHashSet();

        return root;
    }
}

public class FolderArchiveReader : IArchiveReader
{
    private readonly DirectoryInfo _folder;
    private readonly List<string> _images;

    private FolderArchiveReader(DirectoryInfo folder, List<string> images)
    {
        _folder = folder;
        _images = images;
    }

    public static FolderArchiveReader Create(string path)
    {
        var folder = new DirectoryInfo(path);
        var images = folder.EnumerateFiles()
            .Where(ArchiveService.IsImageFile)
            .Select(f => f.Name)
            .Order()
            .ToList();

        return new FolderArchiveReader(folder, images);
    }

    IReadOnlyCollection<string> IArchiveReader.Images => _images;

    public async Task<byte[]?> ReadAllBytesAsync(string path, CancellationToken cancellationToken = default)
    {
        if (!_images.Contains(path))
            return null;

        var fileInfo = new FileInfo(Path.Combine(_folder.FullName, path));
        var bytes = new byte[fileInfo.Length];

        await using var stream = fileInfo.OpenRead();
        await stream.ReadExactlyAsync(bytes, 0, bytes.Length, cancellationToken);

        return bytes;
    }

    public ArchiveFolder ReadFolderTree() => ReadFolderTree(_folder);

    private ArchiveFolder ReadFolderTree(DirectoryInfo folder)
    {
        return new ArchiveFolder()
        {
            Name = folder.Name,
            Files = folder.EnumerateFiles()
                .Select(f => new ArchiveFile(f.Name, f.FullName[(_folder.FullName.Length + 1)..]))
                .OrderBy(f => f.Name)
                .ToHashSet(),
            Folders = folder.EnumerateDirectories()
                .Select(ReadFolderTree)
                .OrderBy(f => f.Name)
                .ToHashSet(),
        };
    }
}