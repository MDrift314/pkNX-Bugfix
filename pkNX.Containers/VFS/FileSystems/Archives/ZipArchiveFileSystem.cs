using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.IO.Compression;

namespace pkNX.Containers.VFS;

public class ZipArchiveFileSystem : IFileSystem
{
    public ZipArchive ZipArchive { get; }

    public bool IsReadOnly => false;

    public static ZipArchiveFileSystem Open(Stream s)
    {
        return new ZipArchiveFileSystem(new ZipArchive(s, ZipArchiveMode.Update, true));
    }

    public static ZipArchiveFileSystem Create(Stream s)
    {
        return new ZipArchiveFileSystem(new ZipArchive(s, ZipArchiveMode.Create, true));
    }

    private ZipArchiveFileSystem(ZipArchive archive)
    {
        ZipArchive = archive;
    }
    public void Dispose()
    {
        ZipArchive.Dispose();
    }

    protected IEnumerable<ZipArchiveEntry> GetZipEntries()
    {
        return ZipArchive.Entries;
    }
    protected FileSystemPath ToPath(ZipArchiveEntry entry)
    {
        return FileSystemPath.Parse(FileSystemPath.DirectorySeparator + entry.FullName);
    }
    protected string ToEntryPath(FileSystemPath path)
    {
        // Remove heading '/' from path.
        return path.Path.TrimStart(FileSystemPath.DirectorySeparator);
    }

    protected ZipArchiveEntry? ToEntry(FileSystemPath path)
    {
        return ZipArchive.GetEntry(ToEntryPath(path));
    }

    public IEnumerable<FileSystemPath> GetEntitiesInDirectory(FileSystemPath directory, Func<FileSystemPath, bool>? filter = null)
    {
        var entries = GetZipEntries().Select(ToPath).Where(directory.IsParentOf);

        if (filter != null)
            entries = entries.Where(filter);

        return entries.Select(entryPath => entryPath.ParentPath == directory
                ? entryPath
                : directory.AppendDirectory(entryPath.MakeRelativeTo(directory).GetDirectorySegments().First()))
            .Distinct();
    }

    public IEnumerable<FileSystemPath> GetDirectoriesInDirectory(FileSystemPath directory, Func<FileSystemPath, bool>? filter = null)
    {
        if (!directory.IsDirectory)
            throw new ArgumentException("This FileSystemPath is not a directory.", nameof(directory));

        var entries = GetZipEntries()
            .Select(ToPath)
            .Where(p => directory.IsParentOf(p) && p.IsDirectory);

        if (filter != null)
            entries = entries.Where(filter);

        return entries.Select(entryPath =>
                entryPath.ParentPath == directory ? entryPath :
                directory.AppendDirectory(entryPath.MakeRelativeTo(directory).GetDirectorySegments().First()))
            .Distinct();
    }

    public IEnumerable<FileSystemPath> GetFilesInDirectory(FileSystemPath directory, Func<FileSystemPath, bool>? filter = null)
    {
        if (!directory.IsDirectory)
            throw new ArgumentException("The specified path is not a directory.", nameof(directory));

        var entries = GetZipEntries()
            .Select(ToPath)
            .Where(p => directory.IsParentOf(p) && p.IsFile);

        if (filter != null)
            entries = entries.Where(filter);

        return entries.Select(entryPath => entryPath.ParentPath == directory
                ? entryPath
                : directory.AppendDirectory(entryPath.MakeRelativeTo(directory).GetDirectorySegments().First()))
            .Distinct();
    }

    public bool Exists(FileSystemPath path)
    {
        if (path.IsFile)
            return ToEntry(path) != null;

        return GetZipEntries()
            .Select(ToPath)
            .Any(entryPath => entryPath.IsChildOf(path) || entryPath.Equals(path));
    }

    public Stream CreateFile(FileSystemPath path)
    {
        ZipArchiveEntry entry = ZipArchive.CreateEntry(ToEntryPath(path));
        return entry.Open();
    }

    public Stream OpenFile(FileSystemPath path, FileMode mode, FileAccess access)
    {
        ZipArchiveEntry? entry = ZipArchive.GetEntry(ToEntryPath(path));
        return entry?.Open() ?? Stream.Null;
    }

    public void CreateDirectory(FileSystemPath path)
    {
        ZipArchive.CreateEntry(ToEntryPath(path));
    }

    public void Delete(FileSystemPath path, DeleteMode mode = DeleteMode.TopMostLayer)
    {
        ZipArchiveEntry? entry = ZipArchive.GetEntry(ToEntryPath(path));
        entry?.Delete();
    }
}
