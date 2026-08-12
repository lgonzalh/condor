using System.Text;
using Condor.Core.Project;

namespace Condor.Infrastructure.Project;

public sealed class ManifestFileReader
{
    private readonly DiscoveryLimits limits;

    public ManifestFileReader(DiscoveryLimits? limits = null)
    {
        this.limits = limits ?? DiscoveryLimits.Default;
    }

    public ManifestRecord? Read(string rootPath, ScannedFile file)
    {
        var fileName = NameOf(file.RelativePath);
        var kind = SignalCatalog.ManifestKindOf(fileName);
        if (kind is null || SecretPatterns.IsSecret(fileName) || SignalCatalog.IsBinaryExtension(fileName))
        {
            return null;
        }

        var fullPath = Path.Combine(rootPath, file.RelativePath.Replace('/', Path.DirectorySeparatorChar));

        long size;
        try
        {
            size = new FileInfo(fullPath).Length;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }

        var record = new ManifestRecord
        {
            Kind = kind,
            Path = file.RelativePath,
            SizeBytes = size
        };

        if (!ManifestParsers.IsParsedKind(kind))
        {
            return record;
        }

        if (size > limits.MaxManifestBytes)
        {
            record.ParseError = true;
            record.LimitManifestSize = true;
            return record;
        }

        byte[] content;
        try
        {
            content = ReadUpTo(fullPath, limits.MaxManifestBytes + 1);
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }

        if (content.Length > limits.MaxManifestBytes)
        {
            record.ParseError = true;
            record.LimitManifestSize = true;
            return record;
        }

        var parsed = ManifestParsers.Parse(kind, DecodeUtf8(content));
        record.Name = parsed.Name;
        record.Version = parsed.Version;
        record.Dependencies.AddRange(parsed.Dependencies);
        record.ParseError = parsed.ParseError;
        record.DependenciesTruncated = parsed.DependenciesTruncated;
        record.Sdk = parsed.Sdk;
        record.UseWpf = parsed.UseWpf;
        record.UseWindowsForms = parsed.UseWindowsForms;
        return record;
    }

    private static byte[] ReadUpTo(string fullPath, int maxBytes)
    {
        var buffer = new byte[maxBytes];
        var total = 0;
        using (var stream = File.OpenRead(fullPath))
        {
            while (total < buffer.Length)
            {
                var read = stream.Read(buffer, total, buffer.Length - total);
                if (read == 0)
                {
                    break;
                }

                total += read;
            }
        }

        if (total == buffer.Length)
        {
            return buffer;
        }

        var trimmed = new byte[total];
        Array.Copy(buffer, trimmed, total);
        return trimmed;
    }

    private static string DecodeUtf8(byte[] content)
    {
        var offset = content.Length >= 3 && content[0] == 0xEF && content[1] == 0xBB && content[2] == 0xBF ? 3 : 0;
        return Encoding.UTF8.GetString(content, offset, content.Length - offset);
    }

    private static string NameOf(string relativePath)
    {
        var index = relativePath.LastIndexOf('/');
        return index >= 0 ? relativePath.Substring(index + 1) : relativePath;
    }
}