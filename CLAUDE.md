# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

**CodeFlow 3D** is a WPF desktop application (.NET Framework 4.7.2) that performs static call-graph analysis on codebases (C#, TypeScript, Python, Java, C++) and renders interactive 3D sequence diagrams. Users select a source and target function, and the app visualizes all execution paths between them as animated 3D diagrams with glass pillars and glowing arrows.

The full product specification is in [CodeFlowVisualizer_PRD.md](CodeFlowVisualizer_PRD.md).

## Build and Run

```bash
# Restore and build
msbuild CodeFlow3D.sln /t:Restore
msbuild CodeFlow3D.sln /p:Configuration=Release

# Run tests
msbuild src/CodeFlow3D.Tests/CodeFlow3D.Tests.csproj /t:Build
vstest.console src/CodeFlow3D.Tests/bin/Release/CodeFlow3D.Tests.dll

# Run a single test
vstest.console src/CodeFlow3D.Tests/bin/Release/CodeFlow3D.Tests.dll /Tests:TestMethodName
```

## Technology Stack

| Layer | Technology |
|---|---|
| UI | WPF (.NET Framework 4.7.2) |
| C# Analysis | Microsoft.CodeAnalysis (Roslyn) via MSBuildWorkspace |
| TS/JS Analysis | Tree-sitter via Node.js subprocess |
| Python Analysis | Python AST via subprocess |
| 3D Rendering | HelixToolkit.Wpf |
| Code Editor | AvalonEdit (read-only, syntax highlighted) |
| MVVM | CommunityToolkit.Mvvm (source-generated commands/properties) |
| Graph | QuikGraph (all graph traversal — don't reimplement graph algorithms) |
| DI | Microsoft.Extensions.DependencyInjection |

## Architecture

The solution lives under `src/` with two projects: `CodeFlow3D` (main app) and `CodeFlow3D.Tests`.

### Key architectural boundaries

- **Analysis/** — Language-specific static analyzers (Roslyn, Tree-sitter subprocess, Python AST subprocess, regex fallback). `AnalyzerFactory` selects analyzer by file extension.
- **Graph/** — Call graph construction (`CallGraphBuilder`) and path finding (`PathFinder` using DFS/BFS). Uses QuikGraph.
- **Rendering/** — 3D scene construction with HelixToolkit. Glass pillars for participants, animated arrows for calls, billboard labels. Isolated from business logic.
- **ViewModels/** — MVVM layer using CommunityToolkit.Mvvm. `DiagramViewModel` bridges `FlowPath` results into renderable objects.
- **Services/** — Interface definitions (`IProjectAnalyzer`, `ICallGraphBuilder`, `IPathFinder`, `ILayoutEngine`, `I3DRenderer`).

### Critical implementation details

- `MSBuildLocator.RegisterDefaults()` must be called once in `App.xaml.cs` before any Roslyn workspace usage.
- All async analysis runs on background threads; UI updates must go through `Application.Current.Dispatcher.InvokeAsync()`.
- All async operations must use `CancellationToken` and report `IProgress<T>`.
- 3D coordinate system: X = participant columns, Y = call depth (top=first call), Z = nesting level.
- Prefer `record` types for immutable models (available via LangVersion setting).

### Data flow

1. User opens project folder → file scanner finds known file types
2. `AnalyzerFactory` selects correct analyzer per file extension → builds `CallGraph` (nodes=functions, edges=calls)
3. User picks source/target functions via `SymbolPickerDialog` (fuzzy search)
4. `PathFinder` runs DFS from source to target → produces `FlowPath` (ordered call sequence)
5. `DiagramViewModel` translates `FlowPath` into 3D layout → `Scene3DBuilder` renders HelixToolkit scene
6. `AnimationSequencer` plays arrows step-by-step with timeline scrubber

## Implementation Phases

The PRD defines 5 phases. Build in order — Phase 1 (project loading + file tree) must work before Phase 2 (call graph + path finding), which must work before Phase 3 (3D rendering), etc.
