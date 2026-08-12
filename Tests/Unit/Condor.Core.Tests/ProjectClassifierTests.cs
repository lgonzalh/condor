using Condor.Core.Models;
using Condor.Core.Project;

namespace Condor.Core.Tests;

public class ProjectClassifierTests
{
    [Fact]
    public void CsProj_EsEvidenciaPrimariaDeCSharp()
    {
        var scan = Scan("src/app.csproj", "a.cs", "b.cs", "c.cs");
        var manifests = new List<ManifestRecord>
        {
            Manifest("csproj", "src/app.csproj")
        };

        var result = new ProjectClassifier().Classify(scan, manifests);

        var csharp = Assert.Single(result.Languages);
        Assert.Equal("C#", csharp.Name);
        Assert.True(csharp.Primary);
        Assert.Equal(EvidenceKind.Manifest, csharp.Signals[0].Kind);
        Assert.Equal("csproj", csharp.Signals[0].Value);
        Assert.Equal(new[] { "csproj", ".cs" }, csharp.Signals.Select(s => s.Value));
        Assert.Equal(3, csharp.Signals[1].Count);
    }

    [Fact]
    public void SoloExtensiones_EsEvidenciaSecundaria()
    {
        var scan = Scan("a.py", "b.py", "c.py");

        var result = new ProjectClassifier().Classify(scan, new List<ManifestRecord>());

        var python = Assert.Single(result.Languages);
        Assert.Equal("Python", python.Name);
        Assert.False(python.Primary);
        Assert.Equal(".py", Assert.Single(python.Signals).Value);
        Assert.Equal(3, python.Signals[0].Count);
    }

    [Fact]
    public void MenosDeTresArchivos_SinLenguaje()
    {
        var scan = Scan("a.js", "b.js");

        var result = new ProjectClassifier().Classify(scan, new List<ManifestRecord>());

        Assert.Empty(result.Languages);
    }

    [Fact]
    public void IndexHtml_EnLaRaiz_DeclaraSitioWeb()
    {
        var scan = Scan("index.html", "style.css", "app.js");

        var result = new ProjectClassifier().Classify(scan, new List<ManifestRecord>());

        var html = Assert.Single(result.Languages);
        Assert.Equal("HTML/CSS", html.Name);
        Assert.True(html.Primary);
        Assert.Equal("index.html", html.Signals[0].Value);
    }

    [Fact]
    public void IndexHtml_EnSrc_TambienDeclaraSitioWeb()
    {
        var scan = Scan("src/index.html");

        var result = new ProjectClassifier().Classify(scan, new List<ManifestRecord>());

        Assert.Contains(result.Languages, l => l.Name == "HTML/CSS" && l.Primary);
    }

    [Fact]
    public void Lenguajes_SeOrdenanPorNombre()
    {
        var scan = Scan("a.py", "b.py", "c.py", "d.go", "e.go", "f.go");

        var result = new ProjectClassifier().Classify(scan, new List<ManifestRecord>());

        Assert.Equal(new[] { "Go", "Python" }, result.Languages.Select(l => l.Name));
    }

    [Fact]
    public void ReactVueAngularExpress_DesdePackageJson()
    {
        var scan = Scan("package.json");
        var manifests = new List<ManifestRecord>
        {
            Manifest("package.json", "package.json", "@angular/core", "express", "react", "vue")
        };

        var result = new ProjectClassifier().Classify(scan, manifests);

        Assert.Equal(new[] { "Angular", "Express", "React", "Vue" }, result.Frameworks.Select(f => f.Name));
        Assert.All(result.Frameworks, f => Assert.Equal("package.json", f.Manifest));
        Assert.Equal("dependencia react", result.Frameworks.Single(f => f.Name == "React").Signal);
    }

    [Fact]
    public void DjangoFlask_DesdeRequirements()
    {
        var scan = Scan("requirements.txt");
        var manifests = new List<ManifestRecord>
        {
            Manifest("requirements.txt", "requirements.txt", "Django", "Flask")
        };

        var result = new ProjectClassifier().Classify(scan, manifests);

        Assert.Equal(new[] { "Django", "Flask" }, result.Frameworks.Select(f => f.Name));
    }

    [Fact]
    public void SpringBoot_DesdePom()
    {
        var scan = Scan("pom.xml");
        var manifests = new List<ManifestRecord>
        {
            Manifest("pom.xml", "pom.xml", "spring-boot-starter-web")
        };

        var result = new ProjectClassifier().Classify(scan, manifests);

        var spring = Assert.Single(result.Frameworks);
        Assert.Equal("Spring Boot", spring.Name);
    }

    [Fact]
    public void AspNetCore_DesdeSdkDeWeb()
    {
        var scan = Scan("web.csproj");
        var manifests = new List<ManifestRecord>
        {
            new ManifestRecord { Kind = "csproj", Path = "web.csproj", Sdk = "Microsoft.NET.Sdk.Web" }
        };

        var result = new ProjectClassifier().Classify(scan, manifests);

        var aspNet = Assert.Single(result.Frameworks);
        Assert.Equal("ASP.NET Core", aspNet.Name);
        Assert.Equal("Sdk Microsoft.NET.Sdk.Web", aspNet.Signal);
    }

    [Fact]
    public void AspNetCore_PorDependenciaCuandoNoHaySdkDeWeb()
    {
        var scan = Scan("app.csproj");
        var manifests = new List<ManifestRecord>
        {
            Manifest("csproj", "app.csproj", "Microsoft.AspNetCore.Hosting")
        };

        var result = new ProjectClassifier().Classify(scan, manifests);

        var aspNet = Assert.Single(result.Frameworks);
        Assert.Equal("dependencia Microsoft.AspNetCore.Hosting", aspNet.Signal);
    }

    [Fact]
    public void WpfYWinForms_DesdeMarcadores()
    {
        var scan = Scan("escritorio.csproj");
        var manifests = new List<ManifestRecord>
        {
            new ManifestRecord { Kind = "csproj", Path = "escritorio.csproj", UseWpf = true, UseWindowsForms = true }
        };

        var result = new ProjectClassifier().Classify(scan, manifests);

        Assert.Equal(new[] { "WPF", "WinForms" }, result.Frameworks.Select(f => f.Name));
        Assert.Equal("UseWPF", result.Frameworks.Single(f => f.Name == "WPF").Signal);
    }

    [Fact]
    public void SinSenales_SinLenguajesNiFrameworks()
    {
        var scan = Scan("datos.txt");

        var result = new ProjectClassifier().Classify(scan, new List<ManifestRecord>());

        Assert.Empty(result.Languages);
        Assert.Empty(result.Frameworks);
        Assert.Equal(new[] { ".txt" }, result.ExtensionCounts.Select(e => e.Name));
        Assert.Equal(1, result.ExtensionCounts[0].Count);
    }

    [Fact]
    public void ConteosDeExtension_SeOrdenanPorNombre()
    {
        var scan = Scan("a.ts", "b.cs", "c.cs");

        var result = new ProjectClassifier().Classify(scan, new List<ManifestRecord>());

        Assert.Equal(new[] { ".cs", ".ts" }, result.ExtensionCounts.Select(e => e.Name));
        Assert.Equal(2, result.ExtensionCounts[0].Count);
    }

    [Fact]
    public void Documentacion_LeePresenciaEnRaizYDocs()
    {
        var scan = Scan("README.md", "LICENSE", "CHANGELOG.md", "docs/README.md", "sub/README.md");
        scan.Directories.Add(new ScannedDirectory("docs", false));

        var result = new ProjectClassifier().Classify(scan, new List<ManifestRecord>());

        Assert.Equal(
            new[] { "CHANGELOG.md", "LICENSE", "README.md", "docs", "docs/README.md" },
            result.Documentation.Select(d => d.Path));
        Assert.Equal("README", result.Documentation.Single(d => d.Path == "README.md").Kind);
        Assert.Equal(0, result.Documentation.Single(d => d.Path == "docs").SizeBytes);
        Assert.DoesNotContain(result.Documentation, d => d.Path == "sub/README.md");
    }

    private static ProjectScan Scan(params string[] relativePaths)
    {
        var scan = new ProjectScan();
        foreach (var relativePath in relativePaths)
        {
            var file = new ScannedFile(relativePath, 10);
            scan.Files.Add(file);
            var extension = SignalCatalog.ExtensionKey(NameOf(relativePath));
            if (extension.Length > 0)
            {
                scan.ExtensionCounts[extension] = scan.ExtensionCounts.TryGetValue(extension, out var count)
                    ? count + 1
                    : 1;
            }
        }

        return scan;
    }

    private static ManifestRecord Manifest(string kind, string path, params string[] dependencies)
    {
        var record = new ManifestRecord
        {
            Kind = kind,
            Path = path,
            Name = kind == "package.json" ? "app" : null
        };
        record.Dependencies.AddRange(dependencies);
        return record;
    }

    private static string NameOf(string relativePath)
    {
        var index = relativePath.LastIndexOf('/');
        return index >= 0 ? relativePath.Substring(index + 1) : relativePath;
    }
}