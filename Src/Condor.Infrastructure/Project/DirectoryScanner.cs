using Condor.Core.Project;

namespace Condor.Infrastructure.Project;

public sealed class DirectoryScanner
{
    private readonly DiscoveryLimits limits;

    public DirectoryScanner(DiscoveryLimits? limits = null)
    {
        this.limits = limits ?? DiscoveryLimits.Default;
    }

    public ProjectScan Scan(string rootPath, CancellationToken cancellationToken = default)
    {
        var scan = new ProjectScan();
        var rootFull = Path.GetFullPath(rootPath);
        var queue = new Queue<(string Path, int Depth)>();
        queue.Enqueue((rootFull, 0));

        try
        {
            while (queue.Count > 0 && !scan.Stopped)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var (directory, depth) = queue.Dequeue();

                if (depth >= limits.MaxDepth)
                {
                    continue;
                }

                IEnumerable<string> entries;
                try
                {
                    entries = Directory.EnumerateFileSystemEntries(directory);
                }
                catch (UnauthorizedAccessException)
                {
                    scan.Degradations.Add("acceso denegado al directorio '" + Relativize(directory, rootFull) + "'");
                    continue;
                }
                catch (IOException)
                {
                    scan.Degradations.Add("no se pudo enumerar el directorio '" + Relativize(directory, rootFull) + "'");
                    continue;
                }

                foreach (var entry in entries)
                {
                    if (scan.Stopped)
                    {
                        break;
                    }

                    var isDirectory = false;
                    var isReparsePoint = false;
                    try
                    {
                        var attributes = File.GetAttributes(entry);
                        isDirectory = attributes.HasFlag(FileAttributes.Directory);
                        isReparsePoint = attributes.HasFlag(FileAttributes.ReparsePoint);
                    }
                    catch (UnauthorizedAccessException)
                    {
                        scan.Degradations.Add("acceso denegado al elemento '" + Relativize(entry, rootFull) + "'");
                        continue;
                    }
                    catch (IOException)
                    {
                        isDirectory = false;
                        isReparsePoint = false;
                    }

                    var relativePath = Relativize(entry, rootFull);

                    if (isDirectory)
                    {
                        var name = Path.GetFileName(entry.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                        if (SignalCatalog.IsExcludedDirectory(name))
                        {
                            continue;
                        }

                        if (scan.Directories.Count >= limits.MaxDirectories)
                        {
                            scan.LimitsApplied.Add(DiscoveryLimits.LimitDirectories);
                            scan.Degradations.Add("limite de directorios alcanzado");
                            scan.Stopped = true;
                            break;
                        }

                        scan.Directories.Add(new ScannedDirectory(relativePath, isReparsePoint));
                        if (!isReparsePoint && depth + 1 < limits.MaxDepth)
                        {
                            queue.Enqueue((entry, depth + 1));
                        }
                    }
                    else
                    {
                        if (scan.Files.Count >= limits.MaxFiles)
                        {
                            scan.LimitsApplied.Add(DiscoveryLimits.LimitFiles);
                            scan.Degradations.Add("limite de archivos alcanzado");
                            scan.Stopped = true;
                            break;
                        }

                        var size = 0L;
                        if (!isReparsePoint)
                        {
                            try
                            {
                                size = new FileInfo(entry).Length;
                            }
                            catch (UnauthorizedAccessException)
                            {
                                scan.Degradations.Add("acceso denegado a '" + relativePath + "'");
                            }
                            catch (IOException)
                            {
                                scan.Degradations.Add("no se pudo medir el archivo '" + relativePath + "'");
                            }
                        }

                        scan.Files.Add(new ScannedFile(relativePath, size));

                        var extension = SignalCatalog.ExtensionKey(Path.GetFileName(entry));
                        if (extension.Length > 0)
                        {
                            scan.ExtensionCounts[extension] = scan.ExtensionCounts.TryGetValue(extension, out var count)
                                ? count + 1
                                : 1;
                        }

                        if (!scan.TotalSizeExceeded)
                        {
                            if (scan.TotalSizeBytes + size > limits.MaxTotalSizeBytes)
                            {
                                scan.TotalSizeExceeded = true;
                                scan.LimitsApplied.Add(DiscoveryLimits.LimitTotalSize);
                                scan.Degradations.Add("tamano total maximo alcanzado");
                            }
                            else
                            {
                                scan.TotalSizeBytes += size;
                            }
                        }
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            scan.LimitsApplied.Add(DiscoveryLimits.LimitTimeout);
            scan.Degradations.Add("tiempo maximo del descubrimiento alcanzado");
        }

        return scan;
    }

    private static string Relativize(string entry, string rootFull)
    {
        var relativePath = entry.Substring(rootFull.Length).TrimStart('\\', '/');
        return relativePath.Replace('\\', '/');
    }
}