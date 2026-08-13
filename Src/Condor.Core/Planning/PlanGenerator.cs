using System;
using System.Collections.Generic;
using System.Linq;
using Condor.Core.Models;

namespace Condor.Core.Planning
{
    public static class PlanGenerator
    {
        private const string ReasonNoContext =
            "No hay contexto operativo disponible. Ejecuta 'condor contexto' o 'condor analizar' primero.";

        private const string ReasonNoRequest = "No hay solicitud del usuario.";

        private const string ReasonIndefiniteIntent =
            "No fue posible interpretar la intencion de la solicitud.";

        public static WorkPlan Generate(
            ProjectContext? context,
            string request,
            PlanLimits limits)
        {
            if (context is null)
            {
                return Degraded(new WorkPlan
                {
                    Status = DetectionStatus.NotDetected,
                    Reason = ReasonNoContext
                });
            }

            if (string.IsNullOrWhiteSpace(request))
            {
                return new WorkPlan
                {
                    SchemaVersion = "1.0.0",
                    Status = DetectionStatus.Limited,
                    Reason = ReasonNoRequest,
                    RootName = context.RootName,
                    WorkingDirectory = context.WorkingDirectory,
                    Intention = PlanIntent.Indefinida,
                    GeneratedAtUtc = DateTime.UtcNow
                };
            }

            var intention = PlanIntent.Classify(request);

            var limitsApplied = BuildLimitsApplied(intention, context);
            var evidence = BuildEvidence(context);
            var risksConsidered = BuildRisksConsidered(context);
            var objective = BuildObjective(request, context, limits, intention);
            var tasks = BuildTasks(intention, context, objective, limits);

            var status = intention == PlanIntent.Indefinida
                ? DetectionStatus.Limited
                : context.Status == DetectionStatus.Limited
                    ? DetectionStatus.Limited
                    : DetectionStatus.Detected;

            return new WorkPlan
            {
                SchemaVersion = "1.0.0",
                Status = status,
                Reason = intention == PlanIntent.Indefinida
                    ? ReasonIndefiniteIntent
                    : context.Reason,
                RootName = context.RootName,
                WorkingDirectory = context.WorkingDirectory,
                Intention = intention,
                Objective = objective,
                Tasks = tasks,
                Evidence = evidence,
                RisksConsidered = risksConsidered,
                LimitsApplied = limitsApplied,
                GeneratedAtUtc = DateTime.UtcNow
            };
        }

        private static WorkPlan Degraded(WorkPlan plan)
        {
            plan.GeneratedAtUtc = DateTime.UtcNow;
            return plan;
        }

        private static List<string> BuildLimitsApplied(string intention, ProjectContext context)
        {
            var applied = new List<string>();

            if (intention == PlanIntent.Indefinida)
            {
                applied.Add(PlanLimits.LimitTasks);
            }

            applied.AddRange(context.LimitsApplied);

            return applied
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToList();
        }

        private static List<string> BuildEvidence(ProjectContext context)
        {
            var values = new List<string>();

            foreach (var recommendation in context.Recommendations)
            {
                values.Add("Recomendacion: " + recommendation.Text);
            }

            return values
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToList();
        }

        private static List<string> BuildRisksConsidered(ProjectContext context)
        {
            var values = new List<string>();

            foreach (var risk in context.Risks)
            {
                values.Add(risk.Kind + " (" + risk.Severity + "): " + risk.Evidence);
            }

            foreach (var dependency in context.RelevantDependencies)
            {
                values.Add("Dependencia: " + dependency.Name + " [" + dependency.Source + "]");
            }

            return values
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToList();
        }

        private static string BuildObjective(
            string request,
            ProjectContext context,
            PlanLimits limits,
            string intention)
        {
            var prefix = intention switch
            {
                PlanIntent.Nueva => "Crear",
                PlanIntent.Continuar => "Continuar",
                PlanIntent.Modificar => "Modificar",
                _ => "Avanzar sobre"
            };

            var requestText = request.Trim();

            var objective = context.RootName.Length > 0
                ? prefix + " " + requestText + " en " + context.RootName
                : prefix + " " + requestText;

            if (objective.Length > limits.MaxObjectiveLength)
            {
                objective = objective.Substring(0, limits.MaxObjectiveLength).TrimEnd();
            }

            return objective;
        }

        private static List<PlanTask> BuildTasks(
            string intention,
            ProjectContext context,
            string objective,
            PlanLimits limits)
        {
            var tasks = new List<PlanTask>();
            var idCounter = 0;

            AddBaseTasks(tasks, intention, context, objective, ref idCounter);

            foreach (var recommendation in context.Recommendations)
            {
                if (tasks.Count >= limits.MaxTasks)
                {
                    break;
                }

                var title = LimitText(recommendation.Text, limits.MaxTaskDetailLength);
                tasks.Add(NewTask(ref idCounter, title, null, Previous(tasks), "media", recommendation.Evidence));
            }

            AddRiskTasks(tasks, context, limits, ref idCounter);

            return tasks.Take(limits.MaxTasks).ToList();
        }

        private static void AddBaseTasks(
            List<PlanTask> tasks,
            string intention,
            ProjectContext context,
            string objective,
            ref int idCounter)
        {
            var baseTaskTitle = intention switch
            {
                PlanIntent.Nueva => "Preparar la base del proyecto",
                PlanIntent.Continuar => "Retomar el punto de continuacion",
                PlanIntent.Modificar => "Aplicar el cambio solicitado",
                _ => "Definir la intencion con el usuario"
            };

            tasks.Add(NewTask(ref idCounter, baseTaskTitle, objective, new List<string>(), "alta", intention));

            if (intention == PlanIntent.Continuar &&
                context.ContinuationPoint is { SuggestedNext: not null } continuation &&
                continuation.SuggestedNext.Length > 0)
            {
                var title = LimitText(continuation.SuggestedNext, PlanLimits.Default.MaxTaskDetailLength);
                tasks.Add(NewTask(ref idCounter, title, null, new List<string> { "T0" }, "alta", "siguiente tarea detectada"));
            }
        }

        private static void AddRiskTasks(
            List<PlanTask> tasks,
            ProjectContext context,
            PlanLimits limits,
            ref int idCounter)
        {
            var high = context.Risks.Where(risk => risk.Severity == "alta").ToList();
            var other = context.Risks.Where(risk => risk.Severity != "alta").ToList();
            var ordered = high.Concat(other).ToList();

            foreach (var risk in ordered)
            {
                if (tasks.Count >= limits.MaxTasks)
                {
                    break;
                }

                tasks.Add(NewTask(
                    ref idCounter,
                    "Atender riesgo: " + risk.Kind,
                    null,
                    Previous(tasks),
                    RiskPriority(risk.Severity),
                    risk.Evidence));
            }
        }

        private static List<string>? Previous(List<PlanTask> tasks)
        {
            return tasks.Count > 0
                ? new List<string> { tasks[tasks.Count - 1].Id }
                : new List<string>();
        }

        private static PlanTask NewTask(
            ref int idCounter,
            string title,
            string? detail,
            List<string>? dependsOn,
            string priority,
            string evidence)
        {
            var task = new PlanTask
            {
                Id = "T" + idCounter,
                Title = title,
                Detail = detail,
                DependsOn = dependsOn ?? new List<string>(),
                Priority = priority,
                Evidence = evidence
            };
            idCounter++;
            return task;
        }

        private static string RiskPriority(string severity)
        {
            return severity switch
            {
                "alta" => "alta",
                "baja" => "baja",
                _ => "media"
            };
        }

        private static string LimitText(string value, int maxLength)
        {
            if (value.Length <= maxLength)
            {
                return value;
            }

            return value.Substring(0, maxLength).TrimEnd();
        }
    }
}
