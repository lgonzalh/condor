#!/usr/bin/env bash
# Script to create a test environment for T-005 testing

echo "Creating test environment for T-005 Context Engine implementation..."

# Create a temporary test project
test_dir="test_project"

if [ -d "$test_dir" ]; then
    rm -rf "$test_dir"
fi

mkdir -p "$test_dir"
cd "$test_dir"

# Create a simple .NET project for testing
cat > "Program.cs" << 'EOF'
using System;
using System.IO;

class Program
{
    static void Main()
    {
        Console.WriteLine("Test project for T-005 Context Engine");
        Console.WriteLine("This project has a simple structure for testing.");
    }
}
EOF

# Create a .gitignore file
cat > ".gitignore" << 'EOF'
node_modules/
*.vs/
*.swp
*.swo
*~
.DS_Store
EOF

# Create a simple README.md
cat > "README.md" << 'EOF'
# Test Project for T-005 Context Engine

This is a simple test project to validate the T-005 Context Engine implementation.

## Structure

- .NET Project file
- Simple source code
- Configuration files
EOF

# Create operation directory with all required artifacts
cd ".."
mkdir -p "test_project/operacion"

# Create ESTADO_DESARROLLO.md
cat > "test_project/operacion/ESTADO_DESARROLLO.md" << 'EOF'
# ESTADO_DESARROLLO

## Estado Inicial

- Proyecto creado
- Configuracion inicial establecida
- Estructura basica definida

## Tareas Pendientes

T-001 Configuracion inicial completada
T-002 Implementacion del motor de evaluacion completada  
T-003 Implementacion del recomendador completada
T-004 Implementacion del motor de descubrimiento completada
T-005 Implementacion del motor de contexto completada
T-006 Implementacion del motor de planeacion completada
T-007 Implementacion del motor de construccion completada
T-008 Implementacion del motor de verificacion completada

## Estado Actual

Todas las tareas T-001 a T-004 completadas con exito.

T-005 en proceso de implementacion.
T-006, T-007 y T-008 pendientes.

## Siguiente tarea

T-006 Definir intencion a partir del contexto.

## Evidencia

- Commits realizados
- Versiones documentadas
- Pruebas completadas

## Metadatos

Archivo creado: $(date)
Estado: Completado
EOF

# Create RELEVO.md
cat > "test_project/operacion/RELEVO.md" << 'EOF'
# RELEVO

## Version: 1.0

### Resumen Ejecutivo

Este documento describe el motor de relevo del proyecto Condor.

### Cambios Realizados

- T-001 Bootstrap inicial completado
- T-002 Motor de evaluacion implementado
- T-003 Recomendador implementado
- T-004 Descubrimiento de proyecto implementado
- T-005 Motor de contexto en implementacion

### Hitos Alcanzados

1. Proyecto creado y configurado
2. Motor de evaluacion funcional
3. Recomendador funcional
4. Descubrimiento de proyecto funcional
5. Motor de contexto en implementacion

### Proximo Paso

Completar T-005 (Context Engine inicial) - Motor de contexto para reconstruccion de contexto operativo.

### Riesgos

- Dependencias limitadas (sin LLM, sin internet, sin cloud)
- Sin contenido LLM para pruebas
- Sin supervisores humanos disponibles

### Versiones

Versión: 1.0
Estado: En desarrollo
EOF

# Create BACKLOG.md
cat > "test_project/operacion/BACKLOG.md" << 'EOF'
# BACKLOG

## Tareas Pendientes

T-006 Implementacion del motor de planeacion
T-007 Implementacion del motor de construccion
T-008 Implementacion del motor de verificacion
T-009 Implementacion del motor de orquestacion
T-010 Implementacion del motor de ejecucion

## Tareas de Alto Valor

### T-006 Flujo de intencion a plan.
Estado: Pendiente
Prioridad: Alta
Descripcion: Implementar capacidad de intencion libre del usuario y generacion de planes basado en contexto.
Dependencias: T-005 completado, T-004 completado

### T-007 Implementacion del motor de construccion
Estado: Pendiente
Prioridad: Alta
Descripcion: Implementar motor de construccion de artefactos.
Dependencias: T-006 completado

### T-008 Implementacion del motor de verificacion
Estado: Pendiente
Prioridad: Alta
Descripcion: Implementar motor de verificacion de resultados.
Dependencias: T-007 completado

## Tareas de Medio Valor

### T-009 Implementacion del motor de orquestacion
Estado: Pendiente
Prioridad: Media
Descripcion: Implementar motor de orquestacion de motores.

### T-010 Implementacion del motor de ejecucion
Estado: Pendiente
Prioridad: Media
Descripcion: Implementar motor de ejecucion de tareas.

## Documentacion

Creado: $(date)
Version: 1.0
Estado: En progreso
EOF

# Create KANBAN.md
cat > "test_project/operacion/KANBAN.md" << 'EOF'
# KANBAN

## Tabla Kanban

| Columna | Tareas |
|---------|--------|
| To Do | T-001 Configuracion inicial completada <br> T-002 Implementacion del motor de evaluacion completada <br> T-003 Implementacion del recomendador completada <br> T-004 Implementacion del motor de descubrimiento completada <br> T-005 En implementacion <br> |

## Definiciones

- To Do: Tareas completadas
- En Progreso: Tareas en proceso
- Pendiente: Tareas futuras

## Siguiente

T-006 Definir intencion a partir del contexto

## Flujo

T-005 (Context Engine) → T-006 (Intent to Plan) → T-007 (Builder) → T-008 (Verifier)

## Evidencia

- Commits completados: 20 commits en main
- Contextos generados
- Limitaciones aplicadas

## Progreso

80% completado (T-001 a T-005 completados)
20% restante (T-006 a T-010 pendientes)

## Siguiente Tarea

T-006 Definir la intencion del usuario a partir del contexto operativo reconstructido por T-005.

## Motivos

- T-005 proporciona contexto estructurado
- T-006 interpreta la intencion libre del usuario
- T-006 produce planes de trabajo
- T-007 implementa tareas
- T-008 verifica resultados

## Limitaciones

- Sin LLM para generación de intencion
- Sin acceso a internet
- Sin servicios cloud
- Sin supervisores humanos
EOF

# Create REGISTRO_CAMBIOS.md
cat > "test_project/operacion/REGISTRO_CAMBIOS.md" << 'EOF'
# REGISTRO_CAMBIOS

CH-001   T-001   Creacion inicial del proyecto
CH-002   T-001   Configuracion del repositorio Git
CH-003   T-001   Creacion de la estructura basica del proyecto
CH-004   T-001   Implementacion del motor de evaluacion (T-002)
CH-005   T-002   Motor de evaluacion inicial implementado
CH-006   T-002   Pruebas unitarias completadas
CH-007   T-002   Integracion completada
CH-008   T-002   Documentacion actualizada
CH-009   T-003   Motor de recomendacion inicial implementado
CH-010   T-003   Pruebas unitarias completadas
CH-011   T-003   Integracion completada
CH-012   T-003   Documentacion actualizada
CH-013   T-004   Motor de descubrimiento inicial implementado
CH-014   T-004   Pruebas unitarias completadas
CH-015   T-004   Integracion completada
CH-016   T-004   Documentacion actualizada
CH-017   T-005   Motor de contexto inicial implementado
CH-018   T-005   Pruebas unitarias completadas
CH-019   T-005   Integracion completada
CH-020   T-005   Documentacion actualizada

## Resumen

- 20 commits publicados en main
- Todas las tareas T-001 a T-005 completadas
- T-006 a T-010 pendientes
- Estructuras generadas: ESTADO_DESARROLLO.md, RELEVO.md, BACKLOG.md, KANBAN.md, REGISTRO_CAMBIOS.md

## Progreso

- Completado: 100% (T-001 a T-005)
- Pendiente: 0% (T-006 a T-010)
- Salud: Verdes (sin fallas reportadas)

## Proximos Pasos

Implementar T-006 (Flujo de intencion a plan) utilizando el contexto operativo reconstruido por T-005.
EOF

echo "Test environment created successfully!"
echo "Test project location: $(pwd)/test_project"
cd test_project && pwd
echo ""
echo "Project structure:"
find . -type f -name "*.md" | sort
