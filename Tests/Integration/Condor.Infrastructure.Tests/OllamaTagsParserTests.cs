using Condor.Core.Models;
using Condor.Infrastructure.Detection;

namespace Condor.Infrastructure.Tests;

public class OllamaTagsParserTests
{
    private const string SampleTagsJson = """
    {
      "models": [
        {
          "name": "hhao/qwen2.5-coder-tools:7b",
          "model": "hhao/qwen2.5-coder-tools:7b",
          "modified_at": "2025-06-03T20:51:14.1333242-03:00",
          "size": 4687090790,
          "digest": "abcd",
          "details": {
            "parent_model": "",
            "format": "gguf",
            "family": "qwen2",
            "families": ["qwen2"],
            "parameter_size": "7.6B",
            "quantization_level": "Q4_K_M",
            "context_length": 32768
          },
          "capabilities": ["completion", "tools", "insert"]
        },
        {
          "name": "deepseek-coder:6.7b",
          "model": "deepseek-coder:6.7b",
          "modified_at": "2025-05-01T00:00:00Z",
          "size": 3826283298,
          "digest": "efgh",
          "details": {
            "format": "gguf",
            "family": "llama",
            "parameter_size": "6.7B",
            "quantization_level": "Q4_K_M"
          },
          "capabilities": ["completion"]
        }
      ]
    }
    """;

    [Fact]
    public void Parse_ConFormatoReal_CompletaTodosLosDetalles()
    {
        var models = OllamaTagsParser.Parse(SampleTagsJson);

        Assert.Equal(2, models.Count);

        var first = models[0];
        Assert.Equal("hhao/qwen2.5-coder-tools:7b", first.Name);
        Assert.Equal(4_687_090_790, first.SizeBytes);
        Assert.Equal("qwen2", first.Family);
        Assert.Equal("7.6B", first.ParameterSize);
        Assert.Equal("Q4_K_M", first.Quantization);
        Assert.Equal(32768, first.ContextLength);
        Assert.Equal(new[] { "completion", "tools", "insert" }, first.Capabilities);
    }

    [Fact]
    public void Parse_DetallesParciales_NoCrasheaYConservaLoDisponible()
    {
        var json = """
        {
          "models": [
            { "name": "sin-detalles:latest", "size": 1234 },
            {
              "name": "parcial:latest",
              "size": 5678,
              "details": { "family": "qwen2" },
              "capabilities": []
            }
          ]
        }
        """;

        var models = OllamaTagsParser.Parse(json);

        Assert.Equal(2, models.Count);
        Assert.Equal("sin-detalles:latest", models[0].Name);
        Assert.Equal(1234, models[0].SizeBytes);
        Assert.Null(models[0].Family);
        Assert.Null(models[0].ContextLength);
        Assert.Equal("qwen2", models[1].Family);
        Assert.Empty(models[1].Capabilities);
    }

    [Fact]
    public void Parse_JsonInvalido_DevuelveListaVacia()
    {
        Assert.Empty(OllamaTagsParser.Parse("json corrupto {{{"));
    }

    [Fact]
    public void Parse_ArregloAlEstiloCli_CompletaModelos()
    {
        var json = """
        [
          {
            "name": "qwen-tools:7b",
            "size": 4687090790,
            "details": { "family": "qwen2", "parameter_size": "7.6B" },
            "capabilities": ["completion", "tools"]
          }
        ]
        """;

        var models = OllamaTagsParser.Parse(json);

        Assert.Single(models);
        Assert.Equal("qwen-tools:7b", models[0].Name);
        Assert.Equal("qwen2", models[0].Family);
        Assert.Contains("tools", models[0].Capabilities);
    }
}

public class OllamaDetectorDetailTests
{
    [Fact]
    public async Task DetectAsync_ConServidorReal_CompletaDetallesDeModelos()
    {
        var detector = new OllamaDetector();

        var result = await detector.DetectAsync();

        if (result.ServerRunning && result.Models.Count > 0)
        {
            var first = result.Models[0];
            Assert.False(string.IsNullOrWhiteSpace(first.Name));
            Assert.True(first.SizeBytes > 0);
            Assert.False(string.IsNullOrWhiteSpace(first.ParameterSize));
            Assert.False(string.IsNullOrWhiteSpace(first.Quantization));
            Assert.True(first.ContextLength > 0);
            Assert.NotEmpty(first.Capabilities);
        }
    }
}
