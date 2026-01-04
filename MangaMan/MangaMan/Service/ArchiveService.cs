using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
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

public static partial class ComparerHelper
{
    public static int CompareNumericFileNames(string? x, string? y)
    {
        if (ReferenceEquals(x, y)) return 0;
        if (y is null) return 1;
        if (x is null) return -1;

        return (ExtractDigits(x), ExtractDigits(y)) switch
        {
            ({ } a, { } b) => int.Parse(a) - int.Parse(b),
            _ => string.Compare(x, y, StringComparison.Ordinal),
        };
    }

    private static readonly Regex LastDigits = MyRegex();

    private static string? ExtractDigits(string name)
    {
        return LastDigits.IsMatch(name) ? LastDigits.Match(name).Value : null;
    }

    [GeneratedRegex(@"\d+(?=\.\w{3,}$)")]
    private static partial Regex MyRegex();
}

public partial record ArchiveFile(string Name, string Path) : IComparable<ArchiveFile>
{
    public bool IsImage { get; } = ArchiveService.IsImageFile(Name);

    public int CompareTo(ArchiveFile? other) => ComparerHelper.CompareNumericFileNames(Name, other?.Name);
}

public interface IArchiveReader
{
    public string Path { get; }
    public IReadOnlyCollection<string> Images { get; }

    public Task<byte[]?> ReadAllBytesAsync(string path, CancellationToken cancellationToken = default);

    public ArchiveFolder ReadFolderTree();
}

public class ZipArchiveReader : IArchiveReader, IDisposable, IAsyncDisposable
{
    private readonly ZipArchive _archive;
    private readonly List<string> _images;
    public string Path { get; }

    private ZipArchiveReader(ZipArchive archive, List<string> images, string path)
    {
        _archive = archive;
        _images = images;
        Path = path;
    }

    public static ZipArchiveReader Create(string path)
    {
        var archive = new ZipArchive(File.OpenRead(path), ZipArchiveMode.Read);
        var images = archive.Entries
            .Where(entry => ArchiveService.IsImageFile(entry.Name))
            .Select(entry => entry.FullName)
            .Order(Comparer<string>.Create(ComparerHelper.CompareNumericFileNames))
            .ToList();

        return new ZipArchiveReader(archive, images, path);
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
                folder.Folders.Add(folder);
            }
        }

        root.Files = root.Files.Order().ToHashSet();
        root.Folders = root.Folders.OrderBy(f => f.Name).ToHashSet();

        return root;
    }
}

public class FolderArchiveReader : IArchiveReader
{
    private readonly DirectoryInfo _folder;
    private readonly List<string> _images;
    public string Path { get; }

    private FolderArchiveReader(DirectoryInfo folder, List<string> images, string path)
    {
        _folder = folder;
        _images = images;
        Path = path;
    }

    public static FolderArchiveReader Create(string path)
    {
        var folder = new DirectoryInfo(path);
        var images = folder.EnumerateFiles()
            .Where(ArchiveService.IsImageFile)
            .Select(f => f.Name)
            .Order(Comparer<string>.Create(ComparerHelper.CompareNumericFileNames))
            .ToList();

        return new FolderArchiveReader(folder, images, path);
    }

    IReadOnlyCollection<string> IArchiveReader.Images => _images;

    public async Task<byte[]?> ReadAllBytesAsync(string path, CancellationToken cancellationToken = default)
    {
        if (!_images.Contains(path))
            return null;

        var fileInfo = new FileInfo(System.IO.Path.Combine(_folder.FullName, path));
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
                .Order()
                .ToHashSet(),
            Folders = folder.EnumerateDirectories()
                .Select(ReadFolderTree)
                .OrderBy(f => f.Name)
                .ToHashSet(),
        };
    }
}