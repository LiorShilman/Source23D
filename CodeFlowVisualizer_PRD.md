# CodeFlow 3D — Visual Code Intelligence Platform
### Claude Code Project Specification | WPF C# 4.7.2

---

## Vision

**CodeFlow 3D** is a professional WPF desktop application that performs **static call-graph analysis** on any project (C#, TypeScript, Python, Java, etc.) and renders an interactive **3D Sequence Diagram** — a living, explorable map of execution flow from any source function to any target function.

The user selects a project folder, picks a source function and a target function through a polished file/symbol browser, and the app renders a photorealistic 3D sequence diagram showing every file, class, and function involved in the path between them — with depth, layers, animations, and drill-down capability.

---

## Target Stack

| Layer | Technology |
|---|---|
| UI Framework | WPF (.NET Framework 4.7.2) |
| Language Analysis — C# | Microsoft.CodeAnalysis (Roslyn) |
| Language Analysis — TypeScript/JS | Tree-sitter via native interop or subprocess |
| Language Analysis — Python | Python.NET + AST module (subprocess fallback) |
| 3D Rendering | HelixToolkit.Wpf 3D viewport |
| 2D Overlays | WPF Canvas + DrawingContext |
| Code Editor | AvalonEdit (syntax highlighting, read-only) |
| MVVM | CommunityToolkit.Mvvm |
| Animations | WPF Storyboard + DoubleAnimation |
| Graph Layout | QuikGraph library (call graph traversal) |
| DI Container | Microsoft.Extensions.DependencyInjection |

---

## Core Concepts

### Call Graph
A directed graph where:
- **Nodes** = functions/methods
- **Edges** = "A calls B" relationships
- **Path** = all routes from source → target, traversed depth-first

### 3D Sequence Diagram Rendering
The sequence diagram is rendered in 3D space:
- **X axis** = participant columns (files / classes)
- **Y axis** = time / call depth (top = call start, bottom = deepest call)
- **Z axis** = nesting level (each nested call "pushes forward" into the screen)

Each "message" (function call) is a glowing arrow floating in 3D space, with the participant panels as frosted glass pillars.

---

## Project Structure

```
CodeFlow3D/
├── CodeFlow3D.sln
├── src/
│   ├── CodeFlow3D/                          # Main WPF application
│   │   ├── App.xaml
│   │   ├── App.xaml.cs
│   │   ├── MainWindow.xaml
│   │   ├── MainWindow.xaml.cs
│   │   ├── Views/
│   │   │   ├── ProjectExplorerPanel.xaml    # Left panel: file tree + function picker
│   │   │   ├── DiagramPanel.xaml            # Center: 3D viewport
│   │   │   ├── CodePreviewPanel.xaml        # Right: code viewer (AvalonEdit)
│   │   │   ├── SymbolPickerDialog.xaml      # Modal: source/target function selector
│   │   │   └── SettingsPanel.xaml
│   │   ├── ViewModels/
│   │   │   ├── MainViewModel.cs
│   │   │   ├── ProjectExplorerViewModel.cs
│   │   │   ├── DiagramViewModel.cs
│   │   │   ├── CodePreviewViewModel.cs
│   │   │   └── SymbolPickerViewModel.cs
│   │   ├── Models/
│   │   │   ├── ProjectModel.cs              # Root project representation
│   │   │   ├── SymbolNode.cs               # File / Class / Function node
│   │   │   ├── CallEdge.cs                 # Directed call relationship
│   │   │   ├── CallGraph.cs                # Full project call graph
│   │   │   ├── FlowPath.cs                 # Source→Target traversal result
│   │   │   └── DiagramLayout.cs            # 3D positioning of nodes
│   │   ├── Services/
│   │   │   ├── IProjectAnalyzer.cs
│   │   │   ├── ICallGraphBuilder.cs
│   │   │   ├── IPathFinder.cs
│   │   │   ├── ILayoutEngine.cs
│   │   │   └── I3DRenderer.cs
│   │   ├── Rendering/
│   │   │   ├── Scene3DBuilder.cs           # HelixToolkit scene construction
│   │   │   ├── ParticipantPillar.cs        # 3D "lifeline" glass pillars
│   │   │   ├── CallArrow3D.cs              # 3D animated arrows
│   │   │   ├── LabelBillboard.cs           # Always-facing-camera labels
│   │   │   ├── SelectionHighlight.cs       # Hover / selection effects
│   │   │   └── AnimationSequencer.cs       # Step-by-step playback
│   │   ├── Analysis/
│   │   │   ├── CSharpAnalyzer.cs           # Roslyn-based analyzer
│   │   │   ├── TypeScriptAnalyzer.cs       # Tree-sitter subprocess analyzer
│   │   │   ├── PythonAnalyzer.cs           # AST subprocess analyzer
│   │   │   ├── GenericAnalyzer.cs          # Regex fallback for unknown types
│   │   │   └── AnalyzerFactory.cs          # Picks correct analyzer by extension
│   │   ├── Graph/
│   │   │   ├── CallGraphBuilder.cs         # Assembles full project graph
│   │   │   ├── PathFinder.cs              # DFS / BFS from source → target
│   │   │   └── GraphExporter.cs           # Export to JSON / DOT / PlantUML
│   │   ├── Controls/
│   │   │   ├── HelixViewport3DEx.cs        # Extended HelixToolkit viewport
│   │   │   ├── AnimatedArrow3D.cs          # Custom 3D arrow with animation
│   │   │   ├── GlassPillar3D.cs            # Semi-transparent participant panel
│   │   │   └── TimelineRuler.cs            # Y-axis call depth ruler
│   │   ├── Converters/
│   │   │   ├── LanguageToIconConverter.cs
│   │   │   ├── DepthToColorConverter.cs
│   │   │   └── BoolToVisibilityConverter.cs
│   │   ├── Styles/
│   │   │   ├── Colors.xaml                 # Dark theme color palette
│   │   │   ├── Typography.xaml
│   │   │   ├── Controls.xaml               # Custom button/panel styles
│   │   │   └── Animations.xaml
│   │   └── Resources/
│   │       ├── Icons/                      # SVG / PNG icons per language
│   │       └── Shaders/                    # HLSL effect shaders (optional)
│   └── CodeFlow3D.Tests/
│       ├── Analysis/
│       │   ├── CSharpAnalyzerTests.cs
│       │   └── PathFinderTests.cs
│       └── Graph/
│           └── CallGraphBuilderTests.cs
```

---

## UI Layout Specification

### Layout: Three-Panel Dark IDE

```
┌──────────────────────────────────────────────────────────────────────────┐
│  [≡ CodeFlow 3D]    📂 Open Project    ⚙ Settings      ▶ Play  ⏸ Pause │  ← Toolbar (48px)
├───────────────┬───────────────────────────────────┬────────────────────────┤
│               │                                   │                        │
│  📁 Project   │         3D DIAGRAM VIEWPORT        │   📄 Code Preview      │
│  Explorer     │                                   │                        │
│               │   [HelixToolkit 3D Scene]         │   [AvalonEdit]         │
│  File Tree    │                                   │                        │
│  ──────────   │   Glass pillars + glowing arrows  │   Syntax highlighted   │
│  🔍 Search    │   floating in dark space          │   source of selected   │
│               │                                   │   function             │
│  Selected:    │                                   │                        │
│  [Source ▾]   │                                   │   ─────────────────    │
│  [Target ▾]   │                                   │   📊 Call Stats        │
│               │                                   │   Depth: 4             │
│  [ANALYZE]    │                                   │   Nodes: 12            │
│               │                                   │   Files: 3             │
│               ├───────────────────────────────────┤                        │
│               │  Timeline ━━━━●━━━━━━━━━━━━━━━━━  │                        │
└───────────────┴───────────────────────────────────┴────────────────────────┘
```

### Color Palette (Dark Theme)

```xaml
<!-- Primary Background -->
<Color x:Key="BgPrimary">#0D0E14</Color>      <!-- Near-black blue -->
<Color x:Key="BgSurface">#13151F</Color>      <!-- Panel backgrounds -->
<Color x:Key="BgCard">#1A1D2E</Color>         <!-- Cards, dropdowns -->
<Color x:Key="BgHover">#22263A</Color>        <!-- Hover states -->

<!-- Accent Colors (for 3D entities) -->
<Color x:Key="AccentCyan">#00D4FF</Color>     <!-- Source function -->
<Color x:Key="AccentGold">#FFB800</Color>     <!-- Target function -->
<Color x:Key="AccentPurple">#9B59FF</Color>   <!-- Intermediate calls -->
<Color x:Key="AccentGreen">#00FF9C</Color>    <!-- Return flows -->
<Color x:Key="AccentRed">#FF4C6A</Color>      <!-- Error / exception paths -->

<!-- Text Colors -->
<Color x:Key="TextPrimary">#E8EAFF</Color>
<Color x:Key="TextSecondary">#8892AA</Color>
<Color x:Key="TextMuted">#4A5066</Color>

<!-- Glass / Glow Effects -->
<Color x:Key="GlassBase">#1E2235</Color>      <!-- Pillar fill base -->
<Color x:Key="GlassBorder">#2A3050</Color>    <!-- Pillar border -->
```

---

## Feature Specifications

### Feature 1: Project Loader

**Behavior:**
1. User clicks "Open Project" → FolderBrowserDialog
2. App recursively scans folder for known file types
3. Progress bar shown during scan
4. File tree populated with file count per language
5. Project stats shown: X files, Y functions discovered

**Supported file types:**
- `.cs` → C# (Roslyn)
- `.ts`, `.tsx`, `.js`, `.jsx` → TypeScript/JavaScript (Tree-sitter subprocess)
- `.py` → Python (AST subprocess)
- `.java` → Java (regex fallback)
- `.cpp`, `.h` → C++ (regex fallback)

**SymbolNode model:**
```csharp
public class SymbolNode
{
    public string Id { get; set; }               // Unique: "File::Class::Method"
    public string Name { get; set; }             // Display name
    public SymbolKind Kind { get; set; }         // File, Namespace, Class, Method, Property
    public string FilePath { get; set; }         // Absolute path
    public int LineStart { get; set; }
    public int LineEnd { get; set; }
    public string Language { get; set; }         // "csharp", "typescript", etc.
    public List<SymbolNode> Children { get; set; }
    public List<string> CalledSymbolIds { get; set; }  // Outgoing call edges
}
```

---

### Feature 2: Symbol Picker (Source & Target)

**Two-phase selection UI — the heart of the UX:**

#### Symbol Picker Dialog
```
┌─────────────────────────────────────────────────────┐
│  🎯 Select Function                            [✕]  │
├─────────────────────────────────────────────────────┤
│  🔍  [Search: "ProcessOrder"               ]        │
├──────────────────────┬──────────────────────────────┤
│  📁 Files            │  ƒ Functions                  │
│                      │                               │
│  ▶ Services/         │  ● ProcessOrder(OrderDto)     │
│  ▶ Controllers/      │  ● ProcessOrderAsync(...)     │
│  ▶ Repositories/     │  ● ValidateOrder(Order)       │
│                      │                               │
│  ● OrderService.cs ◀─┤  ← filtered by selected file │
│    PaymentSvc.cs     │                               │
│    UserService.cs    │                               │
└──────────────────────┴──────────────────────────────┘
│  Selected: OrderService.cs → ProcessOrder(OrderDto) │
│                                          [CONFIRM]  │
└─────────────────────────────────────────────────────┘
```

**Behavior:**
- Fuzzy search across all symbols instantly (debounced 150ms)
- Clicking a file filters the right panel to its functions only
- Hover shows function signature + line number tooltip
- Keyboard navigation: arrows, enter to confirm, escape to cancel
- Recently used symbols remembered per project

---

### Feature 3: Static Call Graph Analysis

#### C# Analysis (Roslyn — Primary)
```csharp
public class CSharpAnalyzer : IProjectAnalyzer
{
    public async Task<CallGraph> AnalyzeAsync(string projectPath, IProgress<AnalysisProgress> progress)
    {
        // 1. Load .sln or .csproj with MSBuildWorkspace
        // 2. For each document → get SemanticModel
        // 3. Walk SyntaxTree with CSharpSyntaxWalker
        // 4. On each InvocationExpressionSyntax:
        //    - Resolve symbol via SemanticModel.GetSymbolInfo()
        //    - Add directed edge: currentMethod → calledMethod
        // 5. Build full CallGraph with all edges
    }
}
```

**Key Roslyn APIs to use:**
- `MSBuildWorkspace.Create()` — load full solution
- `SemanticModel.GetSymbolInfo(invocation)` — resolve call target
- `MethodDeclarationSyntax` — enumerate all methods
- `InvocationExpressionSyntax` — find all call sites
- Handle: virtual calls, interface calls, extension methods, lambdas

#### TypeScript Analysis (Tree-sitter subprocess)
```csharp
public class TypeScriptAnalyzer : IProjectAnalyzer
{
    // Launch Node.js subprocess running tree-sitter-analyzer.js
    // Stdin: JSON config (project path, files to analyze)
    // Stdout: JSON call graph (nodes + edges)
    // Parse and merge into CallGraph model
}
```

Bundled `tree-sitter-analyzer.js`:
- Uses `tree-sitter` + `tree-sitter-typescript`
- Walks AST for `CallExpression`, `NewExpression`
- Resolves imports to find cross-file calls
- Outputs: `{ nodes: [...], edges: [...] }`

#### Python Analysis (subprocess)
```csharp
public class PythonAnalyzer : IProjectAnalyzer
{
    // Launch python3 subprocess running ast_analyzer.py
    // Walks ast.Call nodes
    // Resolves function definitions by name + import
    // Outputs JSON call graph
}
```

#### Fallback (Regex-based for unknown languages)
```csharp
public class GenericAnalyzer : IProjectAnalyzer
{
    // Detect function definitions: regex patterns per extension
    // Detect function calls: naive name-match
    // Lower confidence, shown with ⚠️ indicator on arrows
}
```

---

### Feature 4: Path Finding

```csharp
public class PathFinder : IPathFinder
{
    // DFS from source, collect ALL paths to target
    // Configurable max depth (default: 20)
    // Configurable max paths (default: 5 — show top 5 shortest)
    // Returns: List<FlowPath>, each with ordered list of SymbolNodes

    public FlowPath FindShortestPath(CallGraph graph, string sourceId, string targetId);
    public List<FlowPath> FindAllPaths(CallGraph graph, string sourceId, string targetId, int maxPaths = 5);
}

public class FlowPath
{
    public List<SymbolNode> Steps { get; set; }    // Ordered call sequence
    public int Depth => Steps.Count;
    public List<string> FilesInvolved { get; set; }
    public List<string> ClassesInvolved { get; set; }
    public bool IsAsync { get; set; }
    public bool HasCycles { get; set; }
}
```

**Path display modes:**
- **Shortest path** (default) — minimal call chain
- **All paths** — tabbed view showing up to 5 routes
- **Call tree** — full subtree from source (not filtered to target)

---

### Feature 5: 3D Diagram Rendering (HelixToolkit)

#### Scene Architecture

```
3D World Space:
  Y-axis (up) = time / call order (reversed: top = first call)
  X-axis = participants (files/classes spread horizontally)
  Z-axis = nesting depth (each recursive level pushes further "into screen")

Camera: Perspective, initial position (0, -15, 40), LookAt (0, -10, 0)
Lighting: 2x directional lights + ambient
```

#### Participant Pillars (`GlassPillar3D.cs`)
```csharp
// Each unique file/class in the call path gets a vertical "lifeline" pillar
// Visual: semi-transparent frosted glass box, 0.4f wide, full height of diagram
// Colors: gradient from AccentCyan (source) → AccentGold (target) based on role
// Label: always-facing-camera billboard at top of pillar (filename + class)
// On hover: pillar brightens, shows full file path tooltip
// On click: selects all arrows on this lifeline, opens file in code preview
```

**Pillar construction:**
```csharp
public class ParticipantPillar
{
    public string ParticipantId { get; set; }
    public string Label { get; set; }           // "OrderService\nProcessOrder"
    public double XPosition { get; set; }       // Spread evenly across X
    public Color PillarColor { get; set; }      // Based on role
    public GeometryModel3D Build();             // Returns HelixToolkit mesh
}
```

#### Call Arrows (`CallArrow3D.cs`)
```csharp
// Each function call = animated 3D arrow from caller lifeline → callee lifeline
// Arrow properties:
//   - Thickness: proportional to call frequency (if multiple calls)
//   - Color: caller's pillar color at source, callee's color at target (gradient)
//   - Animation: arrow "draws itself" left-to-right when sequenced
//   - Glow: emissive material creates neon glow effect
//   - Label: function name + "()" billboard above the arrow midpoint
//   - Return arrow: dashed, same path reversed, AccentGreen color

public class CallArrow3D
{
    public SymbolNode Caller { get; set; }
    public SymbolNode Callee { get; set; }
    public int SequenceIndex { get; set; }     // Y position (call order)
    public bool IsReturn { get; set; }
    public bool IsAsync { get; set; }          // Async arrows: dashed style
    public double ZDepth { get; set; }         // Nesting depth offset
    public void AnimateForward(Duration duration);
    public void Highlight(bool selected);
}
```

#### Label Billboards (`LabelBillboard.cs`)
```csharp
// Always-facing-camera labels (WPF ScreenSpaceLines3D or custom billboard shader)
// Font: Consolas 13px, white text on semi-transparent dark bg
// Two layers: function name (bright) + file:line (muted)
// Fade out when camera too far, fade in on hover
```

#### Animation Sequencer (`AnimationSequencer.cs`)
```csharp
public class AnimationSequencer
{
    // Plays the sequence diagram step by step
    // Each step: one arrow animates in (call), then optionally its subtree
    // Controls: Play, Pause, Step Forward, Step Back, Speed (0.5x - 4x)
    // Timeline scrubber: shows all steps, current position, clickable
    
    public void PlayAll();
    public void Pause();
    public void StepForward();
    public void StepBack();
    public void JumpToStep(int step);
    public void SetSpeed(double multiplier);    // 0.5 = slow, 4.0 = fast
}
```

---

### Feature 6: Code Preview Panel

**Behavior:**
- AvalonEdit control, read-only, syntax highlighted
- Click any arrow → opens its source file, scrolls to + highlights the call site
- Click any pillar → opens the file, shows the class/function
- Shows function signature, line number, surrounding context (±10 lines)
- Language auto-detected from file extension

**Stats sidebar (below code preview):**
```
📊 Flow Statistics
─────────────────
Source:    OrderService.ProcessOrder
Target:    Database.ExecuteQuery
Depth:     4 levels
Steps:     7 calls
Files:     3 files involved
Classes:   4 classes
Async:     Yes (2 awaits)
Est. Time: ~12ms (measured)
```

---

### Feature 7: Interaction & Navigation

#### 3D Viewport Controls
- **Left drag**: rotate scene (HelixToolkit default orbit)
- **Scroll wheel**: zoom in/out
- **Right drag**: pan
- **Double-click node**: jump to code preview
- **Ctrl+scroll**: change Z spread (flatten/deepen the nesting axis)
- **F key**: fit all in view (HelixToolkit FitView)
- **Home key**: reset to default camera

#### Filtering Controls (toolbar above diagram)
- **Filter by file**: show only arrows involving selected file
- **Filter by depth**: slider 1–20, hides calls deeper than N
- **Toggle returns**: show/hide return arrows
- **Toggle async**: highlight/hide async calls
- **Collapse subtree**: right-click a node to collapse its sub-calls

#### Export Options
- **PNG/SVG**: screenshot of current 3D view
- **PlantUML**: export as `.puml` sequence diagram text
- **JSON**: full call graph as JSON
- **DOT**: Graphviz format for further processing

---

### Feature 8: Multiple Path Support

When multiple paths exist source → target:

```
[Path 1: Direct]  [Path 2: Via Cache]  [Path 3: Via Queue]
     ●                   ○                     ○
 3 steps              5 steps              7 steps
```

- Tab bar above diagram shows all found paths
- Switching tab replaces the 3D scene (animated transition)
- Stats update per-path
- "Overlay mode": all paths shown simultaneously, color-coded

---

## Implementation Phases

### Phase 1 — Foundation (Week 1)
**Goal: Shell + project loading working**

1. Create WPF solution with proper folder structure
2. Implement dark theme (Colors.xaml, styles)
3. Main window layout (3 panels, toolbar)
4. Project folder loading + file tree (TreeView with icons)
5. Basic file scanner (find .cs, .ts, .py etc.)
6. `SymbolNode` model + in-memory store
7. Roslyn C# analyzer (method enumeration only, no call graph yet)
8. File tree populated with real data

**Deliverable:** App loads a C# project and shows its files and functions in the tree.

---

### Phase 2 — Call Graph (Week 2)
**Goal: Full call graph for C# projects**

1. Roslyn InvocationExpression walker → builds `CallGraph`
2. `PathFinder` with DFS → `FlowPath` results
3. `SymbolPickerDialog` with fuzzy search
4. Source + Target selection working
5. Analyze button → runs analysis, shows progress
6. Results stored in `DiagramViewModel`
7. Basic 2D debug view (list of steps, no 3D yet)
8. Unit tests for analyzer + path finder

**Deliverable:** Select source+target in a C# project → see the call path listed.

---

### Phase 3 — 3D Rendering (Week 3)
**Goal: Full 3D sequence diagram**

1. HelixToolkit viewport integrated in center panel
2. `ParticipantPillar` → glass pillars rendered for each file/class
3. `CallArrow3D` → arrows between pillars at correct Y positions
4. `LabelBillboard` → function name labels on arrows
5. Camera controls working (orbit, zoom, pan)
6. Basic color scheme (source=cyan, target=gold, others=purple)
7. Pillar click → selects lifeline
8. Arrow click → highlights + shows in code preview

**Deliverable:** Full 3D sequence diagram rendered for any C# flow.

---

### Phase 4 — Animation & Polish (Week 4)
**Goal: Professional animation + UX polish**

1. `AnimationSequencer` → step-by-step arrow draw animation
2. Timeline scrubber control
3. Play/Pause/Step/Speed controls
4. Arrow hover glow effects
5. Camera auto-position based on diagram size
6. Multiple path tabs + switching
7. TypeScript analyzer (Tree-sitter subprocess)
8. Python analyzer (AST subprocess)
9. Export: PNG, PlantUML, JSON

**Deliverable:** Fully animated, polished app working with C#, TS, and Python.

---

### Phase 5 — Advanced Features (Week 5)
**Goal: Power user features**

1. Filtering controls (by file, by depth, toggle returns/async)
2. Subtree collapse/expand
3. "Overlay all paths" mode
4. Z-depth spread control
5. Search within diagram
6. Recent projects list
7. Settings panel (theme, animation speed, max depth)
8. Keyboard shortcuts

---

## Critical Implementation Notes

### HelixToolkit Setup
```xml
<!-- NuGet packages -->
<PackageReference Include="HelixToolkit.Wpf" Version="2.25.0" />
<PackageReference Include="Microsoft.CodeAnalysis.CSharp.Workspaces" Version="4.8.0" />
<PackageReference Include="Microsoft.CodeAnalysis.Workspaces.MSBuild" Version="4.8.0" />
<PackageReference Include="AvalonEdit" Version="6.3.0" />
<PackageReference Include="CommunityToolkit.Mvvm" Version="8.2.2" />
<PackageReference Include="QuikGraph" Version="2.5.0" />
<PackageReference Include="Microsoft.Extensions.DependencyInjection" Version="8.0.0" />
```

### MSBuild Workspace Note
```csharp
// REQUIRED: MSBuildLocator must be called ONCE before any Roslyn workspace use
// Add to App.xaml.cs before anything else:
Microsoft.Build.Locator.MSBuildLocator.RegisterDefaults();
```

### Glass Pillar Material (HelixToolkit)
```csharp
var glassMaterial = new MaterialGroup();
glassMaterial.Children.Add(new DiffuseMaterial(
    new SolidColorBrush(Color.FromArgb(40, 30, 50, 80))));    // Semi-transparent blue
glassMaterial.Children.Add(new SpecularMaterial(
    new SolidColorBrush(Colors.White), 80));                    // Shiny highlight
glassMaterial.Children.Add(new EmissiveMaterial(
    new SolidColorBrush(Color.FromArgb(20, 0, 200, 255))));    // Cyan glow
```

### Arrow Animation Pattern
```csharp
// Animate arrow "drawing" by animating Points collection or using DashArray trick:
// 1. Set StrokeDashArray to [0, totalLength]
// 2. Animate DashOffset from totalLength to 0
// This creates a "drawing" effect
var dashAnimation = new DoubleAnimation
{
    From = totalLength,
    To = 0,
    Duration = TimeSpan.FromMilliseconds(400),
    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
};
```

### Billboard Label Approach
```csharp
// WPF ScreenSpaceLines3D doesn't handle text well.
// Best approach: Use HelixToolkit's BillboardTextVisual3D
var label = new BillboardTextVisual3D
{
    Text = "ProcessOrder()",
    Position = new Point3D(midX, midY, midZ),
    FontSize = 13,
    Foreground = Brushes.White,
    Background = new SolidColorBrush(Color.FromArgb(180, 10, 15, 30)),
    BorderBrush = new SolidColorBrush(Color.FromArgb(80, 0, 212, 255)),
    BorderThickness = new Thickness(1),
    Padding = new Thickness(6, 3, 6, 3)
};
```

### Thread Safety
```csharp
// Analysis runs on background thread — always dispatch UI updates:
await Task.Run(() => analyzer.AnalyzeAsync(path, progress));
// Then on UI thread:
Application.Current.Dispatcher.InvokeAsync(() => 
{
    DiagramViewModel.UpdateFromFlowPath(flowPath);
});
```

---

## Performance Requirements

| Metric | Target |
|---|---|
| Project scan (1000 files) | < 5 seconds |
| C# Roslyn analysis (500 classes) | < 15 seconds |
| Path finding (20 depth limit) | < 500ms |
| 3D scene build (50 nodes) | < 1 second |
| Animation frame rate | 60 FPS |
| Memory usage | < 500MB for large projects |

**Optimization strategies:**
- Cache Roslyn SemanticModel per file
- Lazy-load subtrees (only analyze files on the path, not the whole project)
- LOD (Level of Detail) for 3D: at camera distances > 50 units, simplify geometry
- Cancel previous analysis when new one starts (CancellationToken)
- Virtual TreeView for large file lists

---

## Error Handling

```csharp
// Analysis errors: shown as ⚠️ nodes in diagram with tooltip
// Unresolved calls: shown with dashed arrows + "?" label
// Parse errors: logged, skipped, shown in error panel
// Out-of-scope calls (to .NET framework): shown as gray terminal nodes
// Cycles: detected and marked, max 1 cycle expansion per path
```

---

## Accessibility & UX

- All interactive elements have tooltips
- Keyboard-navigable (Tab through panels, arrow keys in tree/lists)
- Status bar always shows current operation + progress
- Undo/redo for source/target selection
- Error state shows actionable message ("No path found. Try a higher depth limit.")
- Loading states with animated indicators for all async operations

---

## Success Criteria

The app is complete when:

1. ✅ User can open any C# solution and browse its symbols
2. ✅ Symbol picker allows fast fuzzy search and selection
3. ✅ Roslyn correctly builds call graph for real-world C# projects
4. ✅ Path finder correctly identifies all routes source → target
5. ✅ 3D scene renders glass pillars, glowing arrows, and labels correctly
6. ✅ Animation plays the sequence step by step smoothly
7. ✅ Clicking arrows/pillars opens correct code location
8. ✅ TypeScript projects are also analyzable (subprocess)
9. ✅ Diagram is exportable as PNG and PlantUML
10. ✅ App looks and feels like a premium professional tool

---

## Development Notes for Claude Code

- Start with `Phase 1` tasks — establish the foundation before any rendering
- Use `CommunityToolkit.Mvvm` for all ViewModels (source-generated commands/properties)
- All `async` operations must use `CancellationToken` and report `IProgress<T>`
- The `AnalyzerFactory` pattern allows easy addition of new languages later
- `QuikGraph` handles all graph traversal — don't implement graph algorithms manually
- Write unit tests alongside each analyzer (`CodeFlow3D.Tests` project)
- Keep 3D logic in `Rendering/` isolated from business logic in `Analysis/` and `Graph/`
- The `DiagramViewModel` is the bridge — it translates `FlowPath` into renderable objects
- Prefer `record` types for immutable models (C# 9+ — available in .NET 4.7.2 via LangVersion)

---

*CodeFlow 3D — Built to make code visible.*
