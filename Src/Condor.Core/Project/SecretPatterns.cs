namespace Condor.Core.Project;

public static class SecretPatterns
{
    public static bool IsSecret(string fileName)
    {
        if (fileName.StartsWith(".env", StringComparison.OrdinalIgnoreCase)) return true;
        if (fileName.StartsWith("secrets.", StringComparison.OrdinalIgnoreCase)) return true;
        if (fileName.StartsWith("credentials", StringComparison.OrdinalIgnoreCase)) return true;
        if (fileName.StartsWith("id_rsa", StringComparison.OrdinalIgnoreCase)) return true;
        if (fileName.EndsWith(".pem", StringComparison.OrdinalIgnoreCase)) return true;
        if (fileName.EndsWith(".key", StringComparison.OrdinalIgnoreCase)) return true;
        if (fileName.EndsWith(".pfx", StringComparison.OrdinalIgnoreCase)) return true;
        if (fileName.EndsWith(".p12", StringComparison.OrdinalIgnoreCase)) return true;
        if (fileName.EndsWith(".npmrc", StringComparison.OrdinalIgnoreCase)) return true;
        if (fileName.EndsWith(".jks", StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }
}