using System.Text;
using Condor.Infrastructure.Project;
using Condor.Core.Project;

namespace Condor.Infrastructure.Tests;

public class ManifestFileReaderTests : IDisposable
{
    private readonly List<string> directoriosTemporales = new();

    [Fact]
    public void LeeCsproj_Valido()
    {
        var root = NuevoDirectorio();
        Escribir(root, "App.csproj", """
        <Project Sdk="Microsoft.NET.Sdk">
          <ItemGroup>
            <PackageReference Include="Newtonsoft.Json" Version="13.0.1" />
          </ItemGroup>
        </Project>
        """);

        var record = new ManifestFileReader().Read(root, new ScannedFile("App.csproj", 0));

        Assert.NotNull(record);
        Assert.Equal("csproj", record.Kind);
        Assert.False(record.ParseError);
        Assert.Equal("Microsoft.NET.Sdk", record.Sdk);
        Assert.Equal(new[] { "Newtonsoft.Json" }, record.Dependencies);
        Assert.True(record.SizeBytes > 0);
    }

    [Fact]
    public void ManifiestoMayorDe64Kb_NoSeParseA()
    {
        var root = NuevoDirectorio();
        Escribir(root, "Grande.csproj", new string('a', 70_000));

        var record = new ManifestFileReader().Read(root, new ScannedFile("Grande.csproj", 0));

        Assert.NotNull(record);
        Assert.True(record.ParseError);
        Assert.True(record.LimitManifestSize);
        Assert.Equal(70_000, record.SizeBytes);
    }

    [Fact]
    public void Secreto_NoSeAbre()
    {
        var root = NuevoDirectorio();
        Escribir(root, ".env", "SECRETO=123");

        var record = new ManifestFileReader().Read(root, new ScannedFile(".env", 0));

        Assert.Null(record);
    }

    [Fact]
    public void NoManifiesto_DevuelveNull()
    {
        var root = NuevoDirectorio();
        Escribir(root, "datos.txt", "contenido");

        var record = new ManifestFileReader().Read(root, new ScannedFile("datos.txt", 0));

        Assert.Null(record);
    }

    [Fact]
    public void PresenciaSola_NoSeParseA()
    {
        var root = NuevoDirectorio();
        Escribir(root, "Makefile", "!!!! contenido binario !!!!");

        var record = new ManifestFileReader().Read(root, new ScannedFile("Makefile", 0));

        Assert.NotNull(record);
        Assert.Equal("Makefile", record.Kind);
        Assert.False(record.ParseError);
        Assert.Empty(record.Dependencies);
    }

    [Fact]
    public void ManifiestoConBomUtf8_SeParseACorrectamente()
    {
        var root = NuevoDirectorio();
        var texto = "{\"name\":\"app-bom\",\"version\":\"2.0.0\",\"dependencies\":{\"express\":\"^4.0.0\"}}";
        File.WriteAllBytes(Path.Combine(root, "package.json"), Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(texto)).ToArray());

        var record = new ManifestFileReader().Read(root, new ScannedFile("package.json", 0));

        Assert.NotNull(record);
        Assert.Equal("package.json", record.Kind);
        Assert.False(record.ParseError);
        Assert.False(record.LimitManifestSize);
        Assert.Equal("app-bom", record.Name);
        Assert.Equal("2.0.0", record.Version);
        Assert.Equal(new[] { "express" }, record.Dependencies);
    }
    [Fact]
    public void JsonInvalido_MarcaErrorDeParseo()
    {
        var root = NuevoDirectorio();
        Escribir(root, "package.json", "{ no es json");

        var record = new ManifestFileReader().Read(root, new ScannedFile("package.json", 0));

        Assert.NotNull(record);
        Assert.Equal("package.json", record.Kind);
        Assert.True(record.ParseError);
        Assert.False(record.LimitManifestSize);
    }

    private string NuevoDirectorio()
    {
        var directorio = Path.Combine(Path.GetTempPath(), "condor-t004-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directorio);
        directoriosTemporales.Add(directorio);
        return directorio;
    }

    private static void Escribir(string directorio, string nombre, string contenido)
    {
        File.WriteAllText(Path.Combine(directorio, nombre), contenido);
    }

    public void Dispose()
    {
        foreach (var directorio in directoriosTemporales)
        {
            try
            {
                Directory.Delete(directorio, true);
            }
            catch
            {
                // Directorio en uso o ya eliminado.
            }
        }
    }
}