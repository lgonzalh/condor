using System;
using System.IO;

namespace Condor.Infrastructure.Vision;

public sealed class ImageFileReader
{
    public ImageReadResult Read(string path, long maxBytes)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return ImageReadResult.Fail("No se indico una ruta de imagen.");
        }

        if (!File.Exists(path))
        {
            return ImageReadResult.Fail("El archivo de imagen no existe: " + path);
        }

        if (Directory.Exists(path))
        {
            return ImageReadResult.Fail("La ruta indicada es un directorio, no una imagen.");
        }

        FileInfo info;

        try
        {
            info = new FileInfo(path);
        }
        catch
        {
            return ImageReadResult.Fail("No fue posible leer los datos de la imagen.");
        }

        if (info.Length > maxBytes)
        {
            return ImageReadResult.Fail("La imagen supera el limite de " + maxBytes + " bytes.");
        }

        byte[] bytes;

        try
        {
            bytes = File.ReadAllBytes(path);
        }
        catch
        {
            return ImageReadResult.Fail("La imagen no puede leerse.");
        }

        return ImageReadResult.Ok(path, bytes.Length, bytes);
    }
}

public readonly record struct ImageReadResult(
    bool Success,
    string Path,
    long SizeBytes,
    byte[]? Bytes,
    string? Reason)
{
    public static ImageReadResult Ok(string path, long size, byte[] bytes) =>
        new(true, path, size, bytes, null);

    public static ImageReadResult Fail(string reason) =>
        new(false, string.Empty, 0, null, reason);
}
