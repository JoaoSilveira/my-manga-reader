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
    private static readonly string[] ImageExtensions = [".jpg", ".jpeg", ".png",".webp"];

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

public interface IArchiveReader
{
    public IReadOnlyCollection<string> Images { get; }

    public Task<byte[]?> ReadAllBytesAsync(string path, CancellationToken cancellationToken = default);
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

        var bytes = new byte[entry.Length];
        await using var stream = await entry.OpenAsync(cancellationToken);
        await stream.ReadExactlyAsync(bytes, 0, bytes.Length, cancellationToken);

        return bytes;
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
            .Select(f => f.FullName)
            .Order()
            .ToList();

        return new FolderArchiveReader(folder, images);
    }

    IReadOnlyCollection<string> IArchiveReader.Images => _images;

    public async Task<byte[]?> ReadAllBytesAsync(string path, CancellationToken cancellationToken = default)
    {
        if (!_images.Contains(path))
            return null;

        var fileInfo = new FileInfo(path);
        var bytes = new byte[fileInfo.Length];

        await using var stream = fileInfo.OpenRead();
        await stream.ReadExactlyAsync(bytes, 0, bytes.Length, cancellationToken);

        return bytes;
    }
}