# Controles locales de calidad

## Complejidad y duplicación

GitHub Actions ejecuta `Compare-ChangedComplexity.ps1` y
`Compare-ChangedDuplication.ps1` para solicitudes de cambio y colas de merge,
comparando el SHA base del evento con el commit que CI valida. Ambos informes
son señales para revisión; no reemplazan el análisis del código ni bloquean por
un aumento numérico aislado.

Para ejecutarlos localmente, indica un commit base:

```powershell
$env:CONDOR_METRICS_BASE = "<sha-base>"
./operacion/scripts/Compare-ChangedComplexity.ps1
./operacion/scripts/Compare-ChangedDuplication.ps1
```

Para incluir los cambios rastreados y archivos C# nuevos del árbol local,
agrega `-UseWorkingTree` al comparador correspondiente. Por ejemplo:

```powershell
./operacion/scripts/Compare-ChangedComplexity.ps1 -BaseSha "<sha-base>" -UseWorkingTree
./operacion/scripts/Compare-ChangedDuplication.ps1 -BaseSha "<sha-base>" -UseWorkingTree
```

La complejidad se informa a partir de los diagnósticos CA1502; los valores por
debajo del umbral pueden aparecer como `sin alerta`. La comparación de
duplicación busca bloques de seis líneas exactas entre archivos C# de `Src` y
`Tests`; un bloque repetido requiere revisión porque la coincidencia textual no
demuestra que dos implementaciones sean equivalentes.

Para analizar los cambios locales aún no confirmados, agrega `-UseWorkingTree`;
el comparador incluye archivos C# nuevos. `-ShowExamples` muestra hasta dos
bloques repetidos por archivo modificado para ayudar a revisar coincidencias
incidentales. Las ventanas que contienen directivas `using` se excluyen para
reducir alertas causadas solo por imports comunes:

```powershell
./operacion/scripts/Compare-ChangedDuplication.ps1 `
  -BaseSha "<sha-base>" `
  -UseWorkingTree `
  -ShowExamples
```

## Escenario de rendimiento

Cuando un cambio pueda afectar el rendimiento, ejecuta el escenario pertinente
en la base y en HEAD. Pasa el ejecutable y cada argumento como elementos
separados, por ejemplo:

```powershell
./operacion/scripts/Measure-ScenarioPerformance.ps1 `
  -BaseSha "<sha-base>" `
  -Executable "dotnet" `
  -ArgumentList @("test", "Tests/Integration/Condor.Infrastructure.Tests/Condor.Infrastructure.Tests.csproj", "-c", "Release", "--filter", "FullyQualifiedName~EscenarioRelevante") `
  -Iterations 3
```

El runner alterna el orden, guarda cada medición y compara las medianas de
duración, CPU y memoria pico muestreada del árbol de procesos en Windows. El
informe JSON queda bajo `.condor/performance/`, que es estado local ignorado por
Git. La memoria suma working sets cada 100 ms y puede contar páginas
compartidas más de una vez; los procesos que aparecen y terminan entre muestras
pueden no quedar registrados.

Para comparar los cambios locales aún no confirmados, agrega
`-UseWorkingTree`. Si el escenario necesita preparar dependencias o compilar
antes de medirse, `-SetupExecutable` y `-SetupArgumentList` ejecutan esa
preparación una vez en cada árbol, fuera del intervalo medido. Por ejemplo:

```powershell
./operacion/scripts/Measure-ScenarioPerformance.ps1 `
  -BaseSha "<sha-base>" `
  -UseWorkingTree `
  -SetupExecutable "dotnet" `
  -SetupArgumentList @("restore", "Condor.slnx", "--verbosity", "quiet") `
  -Executable "dotnet" `
  -ArgumentList @("build", "Condor.slnx", "--configuration", "Release", "--no-restore", "--no-incremental", "--verbosity", "quiet") `
  -Iterations 2
```

El informe registra si midió `HEAD` o el árbol de trabajo y los comandos de
preparación y medición para que la comparación se pueda interpretar.
