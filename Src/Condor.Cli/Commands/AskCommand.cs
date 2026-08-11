using Condor.Cli.Presentation;
using Condor.Core.Contracts;
using Condor.Core.Evaluation;
using Condor.Core.Models;

namespace Condor.Cli.Commands;

public static class AskCommand
{
    public static async Task<int> ExecuteAsync(
        ILlmClient llmClient,
        IStateStore stateStore,
        string[] args,
        CancellationToken cancellationToken = default)
    {
        var list = args.ToList();

        if (list.Any(argument => argument.Equals("--model", StringComparison.OrdinalIgnoreCase)))
        {
            Terminal.WriteError("El argumento '--model' ya no se usa. Usa '--modelo <modelo>'.");
            return 1;
        }

        var modelIndex = list.FindIndex(argument =>
            argument.Equals("--modelo", StringComparison.OrdinalIgnoreCase));

        string? explicitModel = null;
        if (modelIndex >= 0 && modelIndex + 1 < list.Count)
        {
            explicitModel = list[modelIndex + 1];
            list.RemoveAt(modelIndex + 1);
            list.RemoveAt(modelIndex);
        }

        var prompt = string.Join(" ", list).Trim();
        if (string.IsNullOrWhiteSpace(prompt))
        {
            Terminal.WriteError("Uso: condor consultar \"<mensaje>\" [--modelo <modelo>]");
            return 1;
        }

        var assessment = await stateStore.LoadAssessmentAsync(cancellationToken);
        var model = LlmModelSelector.Select(assessment, explicitModel);

        if (string.IsNullOrWhiteSpace(model))
        {
            Terminal.WriteError("No hay un modelo disponible.");
            Terminal.WriteDim("Ejecuta 'condor analizar' para detectar los modelos o especifica uno con --modelo.");
            return 1;
        }

        if (string.IsNullOrWhiteSpace(explicitModel))
        {
            Terminal.WriteDim("Modelo seleccionado (primer disponible): " + model);
        }

        Terminal.WriteInfo("Condor consulta al modelo local " + model + "...");

        var response = await llmClient.CompleteAsync(
            new LlmRequest { Model = model, Prompt = prompt },
            cancellationToken);

        if (!response.Success)
        {
            Terminal.WriteError(response.Error ?? "La inferencia fallo sin detalle");
            return 1;
        }

        Terminal.WriteLine();
        Terminal.WriteSuccess("Respuesta de " + (response.Model ?? model) + ":");
        Terminal.WriteLine();
        Terminal.WriteLine(response.Content ?? "");
        Terminal.WriteLine();

        return 0;
    }
}
