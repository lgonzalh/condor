using System.Text;
using Condor.Core.Project;

namespace Condor.Core.Tests;

public class ManifestParserTests
{
    [Fact]
    public void PackageJson_LeeNombreVersionYDependenciasOrdenadas()
    {
        var content = PackageJsonParser.Parse("""
        {
          "name": "mi-app",
          "version": "1.2.3",
          "type": "module",
          "dependencies": { "z": "1", "a": "2", "react": "18" },
          "devDependencies": { "a": "1", "typescript": "5" },
          "scripts": { "start": "node index.js" }
        }
        """);

        Assert.False(content.ParseError);
        Assert.Equal("mi-app", content.Name);
        Assert.Equal("1.2.3", content.Version);
        Assert.Equal(new[] { "a", "react", "typescript", "z" }, content.Dependencies);
    }

    [Fact]
    public void PackageJson_JsonInvalido_MarcaErrorDeParseo()
    {
        var content = PackageJsonParser.Parse("{ no es json valido");

        Assert.True(content.ParseError);
    }

    [Fact]
    public void PackageJson_RaizNoObjeto_MarcaErrorDeParseo()
    {
        var content = PackageJsonParser.Parse("[1, 2]");

        Assert.True(content.ParseError);
    }

    [Fact]
    public void PackageJson_SinCampos_NoMarcaError()
    {
        var content = PackageJsonParser.Parse("{}");

        Assert.False(content.ParseError);
        Assert.Null(content.Name);
        Assert.Null(content.Version);
        Assert.Empty(content.Dependencies);
    }

    [Fact]
    public void PackageJson_DependenciasSuperanElLimite_SeTruncan()
    {
        var builder = new StringBuilder();
        builder.Append("{\"dependencies\": {");
        for (var i = 0; i < 120; i++)
        {
            builder.Append("\"paquete").Append(i).Append("\": \"1.0\",");
        }
        builder.Length -= 1;
        builder.Append("}}");

        var content = PackageJsonParser.Parse(builder.ToString());

        Assert.False(content.ParseError);
        Assert.Equal(100, content.Dependencies.Count);
        Assert.True(content.DependenciesTruncated);
    }

    [Fact]
    public void Csproj_LeeSdkDependencias_yMarcadoresDeFramework()
    {
        var content = CsprojParser.Parse("""
        <Project Sdk="Microsoft.NET.Sdk.Web">
          <PropertyGroup>
            <TargetFramework>net10.0</TargetFramework>
            <UseWPF>true</UseWPF>
          </PropertyGroup>
          <ItemGroup>
            <PackageReference Include="Microsoft.AspNetCore.App" />
            <PackageReference Include="Newtonsoft.Json" Version="13.0.1" />
          </ItemGroup>
        </Project>
        """);

        Assert.False(content.ParseError);
        Assert.Equal("Microsoft.NET.Sdk.Web", content.Sdk);
        Assert.True(content.UseWpf);
        Assert.False(content.UseWindowsForms);
        Assert.Equal(new[] { "Microsoft.AspNetCore.App", "Newtonsoft.Json" }, content.Dependencies);
    }

    [Fact]
    public void Csproj_XmlInvalido_MarcaErrorDeParseo()
    {
        Assert.True(CsprojParser.Parse("<Project").ParseError);
    }

    [Fact]
    public void Pom_LeeProyectoYDependencias()
    {
        var content = PomXmlParser.Parse("""
        <project>
          <modelVersion>4.0.0</modelVersion>
          <groupId>com.example</groupId>
          <artifactId>demo-app</artifactId>
          <version>0.0.1</version>
          <properties>
            <java.version>17</java.version>
          </properties>
          <dependencies>
            <dependency>
              <groupId>org.springframework.boot</groupId>
              <artifactId>spring-boot-starter-web</artifactId>
            </dependency>
            <dependency>
              <artifactId>unidad-sola</artifactId>
            </dependency>
          </dependencies>
        </project>
        """);

        Assert.False(content.ParseError);
        Assert.Equal("demo-app", content.Name);
        Assert.Equal("0.0.1", content.Version);
        Assert.Equal(new[] { "spring-boot-starter-web", "unidad-sola" }, content.Dependencies);
    }

    [Fact]
    public void Cargo_LeePaqueteDependencias_yExcluyeDev()
    {
        var content = CargoTomlParser.Parse("""
        [package]
        name = "mi-crate"
        version = "0.1.0"
        edition = "2021"

        [dependencies]
        serde = "1.0"
        tokio = { version = "1", features = ["full"] }

        [dev-dependencies]
        criterion = "0.5"

        [dependencies.custom]
        path = "../custom"
        """);

        Assert.False(content.ParseError);
        Assert.Equal("mi-crate", content.Name);
        Assert.Equal("0.1.0", content.Version);
        Assert.Equal(new[] { "custom", "serde", "tokio" }, content.Dependencies);
    }

    [Fact]
    public void Cargo_SoloDependenciasDeDesarrollo_SinDependencias()
    {
        var content = CargoTomlParser.Parse("""
        [package]
        name = "vacio"
        version = "0.1.0"

        [dev-dependencies]
        cosa = "1"
        """);

        Assert.Equal("vacio", content.Name);
        Assert.Empty(content.Dependencies);
    }

    [Fact]
    public void Pyproject_LeeProyectoYDependenciasMultilinea()
    {
        var content = PyprojectTomlParser.Parse("""
        [project]
        name = "mi-paquete"
        version = "0.1.0"
        dependencies = [
          "Django>=4.2",
          "flask==3.0",
          "requests",
        ]

        [project.optional-dependencies]
        dev = ["pytest"]
        """);

        Assert.False(content.ParseError);
        Assert.Equal("mi-paquete", content.Name);
        Assert.Equal("0.1.0", content.Version);
        Assert.Equal(new[] { "Django", "flask", "requests" }, content.Dependencies);
    }

    [Fact]
    public void Pyproject_DependenciasEnUnaLinea()
    {
        var content = PyprojectTomlParser.Parse("""
        [project]
        name = "simple"
        version = "0.0.1"
        dependencies = ["aiohttp>=3.8", "pydantic"]
        """);

        Assert.Equal(new[] { "aiohttp", "pydantic" }, content.Dependencies);
    }

    [Fact]
    public void Requirements_IgnoraComentariosOpciones_yVersiones()
    {
        var content = RequirementsTxtParser.Parse("""
        # comentario de cabecera
        flask==3.0.1
        requests>=2.31
        -e git+https://github.com/x/y.git
        Django
        --index-url https://pypi.org/simple
        black<=23.1
        """);

        Assert.False(content.ParseError);
        Assert.Equal(new[] { "Django", "black", "flask", "requests" }, content.Dependencies);
    }

    [Fact]
    public void Requirements_LineasSobreElLimite_SeTruncan()
    {
        var builder = new StringBuilder();
        for (var i = 0; i < 120; i++)
        {
            builder.Append("paquete").Append(i).Append('\n');
        }

        var content = RequirementsTxtParser.Parse(builder.ToString());

        Assert.Equal(100, content.Dependencies.Count);
        Assert.True(content.DependenciesTruncated);
    }

    [Fact]
    public void GoMod_LeeSoloLaLineaModule()
    {
        var content = GoModParser.Parse("""
        module github.com/example/app

        go 1.22

        require (
            github.com/user/lib v1.0.0
        )

        toolchain go1.23.1
        """);

        Assert.False(content.ParseError);
        Assert.Equal("github.com/example/app", content.Name);
        Assert.Null(content.Version);
        Assert.Empty(content.Dependencies);
    }

    [Fact]
    public void TsConfig_LeeTarget()
    {
        var content = TsConfigJsonParser.Parse("""
        {
          "compilerOptions": { "target": "ES2022", "strict": true },
          "extends": "./base.json"
        }
        """);

        Assert.False(content.ParseError);
        Assert.Equal("ES2022", content.TsTarget);
    }

    [Fact]
    public void Ruteador_PresenciaSola_NoUsaParser()
    {
        Assert.False(ManifestParsers.IsParsedKind("Makefile"));
        Assert.True(ManifestParsers.IsParsedKind("csproj"));
        Assert.True(ManifestParsers.IsParsedKind("package.json"));
    }
}