using Condor.Core.Models;

namespace Condor.Core.Evaluation;

/// <summary>Casos versionados de defectos conocidos y un caso limpio para calibrar al revisor.</summary>
public static class ReviewerBenchmarkSuite
{
    // La versión representa los casos y las reglas de puntuación. Los cambios
    // de instrucciones se distinguen mediante PromptHash y se comparan contra
    // la línea base para medir si mejoran el revisor.
    public const string Version = "reviewer-v2";

    public static IReadOnlyList<ReviewerBenchmarkCase> Cases { get; } = new[]
    {
        new ReviewerBenchmarkCase(
            "private-document-access",
            "src/Sample/AccessControl.cs",
            "public static bool CanRead(User user, Document document)\n{\n    return document.OwnerId == user.Id || user.IsAuthenticated;\n}",
            new[]
            {
                new ReviewerBenchmarkExpectation("src/Sample/AccessControl.cs", 3, "high", new[]
                {
                    new[] { "owner", "propietario", "dueño" },
                    new[] { "authenticated", "autenticado", "autenticada" }
                })
            }),
        new ReviewerBenchmarkCase(
            "path-traversal",
            "src/Sample/ArchiveReader.cs",
            "public static string Read(string root, string name)\n{\n    var path = Path.Combine(root, name);\n    return File.ReadAllText(path);\n}",
            new[]
            {
                new ReviewerBenchmarkExpectation("src/Sample/ArchiveReader.cs", 3, "high", new[]
                {
                    new[] { "traversal", "atravies", "../", "fuera del directorio", "outside the directory", "parent directory", "directorio padre", "escapar", "escape" }
                })
            }),
        new ReviewerBenchmarkCase(
            "negative-order-quantity",
            "src/Sample/Cart.cs",
            "public static int AddToStock(int requestedQuantity, int stock)\n{\n    if (requestedQuantity <= stock)\n    {\n        stock -= requestedQuantity;\n        return stock;\n    }\n    return stock;\n}",
            new[]
            {
                new ReviewerBenchmarkExpectation("src/Sample/Cart.cs", 3, "medium", new[]
                {
                    new[] { "negativa", "negative" },
                    new[] { "aumenta", "incrementa", "increase", "increases", "increment" }
                })
            }),
        new ReviewerBenchmarkCase(
            "clean-clamp",
            "src/Sample/Clamp.cs",
            "public static int Clamp(int value, int min, int max)\n{\n    if (min > max) throw new ArgumentException();\n    return Math.Min(max, Math.Max(min, value));\n}",
            Array.Empty<ReviewerBenchmarkExpectation>())
    };
}
