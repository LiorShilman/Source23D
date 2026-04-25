using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using CodeFlow3D.Models;
using CodeFlow3D.Services;
using SK = CodeFlow3D.Models.SymbolKind;

namespace CodeFlow3D.Analysis
{
    public class CSharpAnalyzer : IProjectAnalyzer
    {
        public string Language => "csharp";
        public string[] SupportedExtensions => new[] { ".cs" };

        public async Task<CallGraph> AnalyzeAsync(string projectPath, IProgress<AnalysisProgress> progress, CancellationToken ct = default)
        {
            var graph = new CallGraph();
            var csFiles = Directory.GetFiles(projectPath, "*.cs", SearchOption.AllDirectories)
                .Where(f => !f.Contains("\\obj\\") && !f.Contains("\\bin\\") && !f.Contains("/obj/") && !f.Contains("/bin/"))
                .ToList();

            int total = csFiles.Count;
            int processed = 0;

            // Parse all files
            var syntaxTrees = new List<SyntaxTree>();
            foreach (var file in csFiles)
            {
                ct.ThrowIfCancellationRequested();
                var code = await Task.Run(() => File.ReadAllText(file), ct);
                var tree = CSharpSyntaxTree.ParseText(code, path: file, cancellationToken: ct);
                syntaxTrees.Add(tree);
            }

            // Try to build a compilation for semantic resolution
            var references = GetFrameworkReferences();
            var compilation = CSharpCompilation.Create("Analysis", syntaxTrees, references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            // PASS 1: Discover all methods and constructors (build the node index)
            // Key = method name (simple), Value = list of full IDs
            var methodIndex = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            // Key = "TypeName.MethodName", Value = full ID
            var qualifiedIndex = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var tree in syntaxTrees)
            {
                ct.ThrowIfCancellationRequested();
                var filePath = tree.FilePath;
                var semanticModel = compilation.GetSemanticModel(tree);
                var root = await tree.GetRootAsync(ct);

                var fileNode = new SymbolNode
                {
                    Id = filePath,
                    Name = Path.GetFileName(filePath),
                    Kind = SK.File,
                    FilePath = filePath,
                    Language = "csharp"
                };
                graph.AddNode(fileNode);

                var types = root.DescendantNodes().OfType<TypeDeclarationSyntax>();
                foreach (var type in types)
                {
                    var typeSymbol = semanticModel.GetDeclaredSymbol(type);
                    var typeName = typeSymbol?.ToDisplayString() ?? type.Identifier.Text;
                    var shortTypeName = typeSymbol?.Name ?? type.Identifier.Text;

                    // Methods
                    foreach (var method in type.Members.OfType<MethodDeclarationSyntax>())
                    {
                        var methodSymbol = semanticModel.GetDeclaredSymbol(method);
                        var methodId = methodSymbol?.ToDisplayString() ?? $"{typeName}.{method.Identifier.Text}";

                        var methodNode = new SymbolNode
                        {
                            Id = methodId,
                            Name = method.Identifier.Text,
                            Kind = SK.Method,
                            FilePath = filePath,
                            LineStart = method.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                            LineEnd = method.GetLocation().GetLineSpan().EndLinePosition.Line + 1,
                            Language = "csharp",
                            ContainingType = shortTypeName,
                            Signature = BuildSignature(method),
                            IsAsync = method.Modifiers.Any(SyntaxKind.AsyncKeyword)
                        };
                        graph.AddNode(methodNode);
                        fileNode.Children.Add(methodNode);

                        // Index by simple name
                        if (!methodIndex.TryGetValue(method.Identifier.Text, out var list))
                        {
                            list = new List<string>();
                            methodIndex[method.Identifier.Text] = list;
                        }
                        list.Add(methodId);

                        // Index by qualified name
                        qualifiedIndex[$"{shortTypeName}.{method.Identifier.Text}"] = methodId;
                    }

                    // Constructors
                    foreach (var ctor in type.Members.OfType<ConstructorDeclarationSyntax>())
                    {
                        var ctorSymbol = semanticModel.GetDeclaredSymbol(ctor);
                        var ctorId = ctorSymbol?.ToDisplayString() ?? $"{typeName}..ctor";

                        var ctorNode = new SymbolNode
                        {
                            Id = ctorId,
                            Name = ".ctor",
                            Kind = SK.Constructor,
                            FilePath = filePath,
                            LineStart = ctor.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                            LineEnd = ctor.GetLocation().GetLineSpan().EndLinePosition.Line + 1,
                            Language = "csharp",
                            ContainingType = shortTypeName,
                            Signature = ctorSymbol?.ToDisplayString() ?? $"{shortTypeName}()"
                        };
                        graph.AddNode(ctorNode);

                        if (!methodIndex.TryGetValue(shortTypeName, out var ctorList))
                        {
                            ctorList = new List<string>();
                            methodIndex[shortTypeName] = ctorList;
                        }
                        ctorList.Add(ctorId);
                    }
                }

                processed++;
                progress?.Report(new AnalysisProgress
                {
                    Phase = "Discovering symbols",
                    CurrentFile = Path.GetFileName(filePath),
                    FilesProcessed = processed,
                    TotalFiles = total
                });
            }

            // PASS 2: Find call edges (semantic first, then syntax fallback)
            processed = 0;
            foreach (var tree in syntaxTrees)
            {
                ct.ThrowIfCancellationRequested();
                var filePath = tree.FilePath;
                var semanticModel = compilation.GetSemanticModel(tree);
                var root = await tree.GetRootAsync(ct);

                var types = root.DescendantNodes().OfType<TypeDeclarationSyntax>();
                foreach (var type in types)
                {
                    var typeSymbol = semanticModel.GetDeclaredSymbol(type);
                    var typeName = typeSymbol?.ToDisplayString() ?? type.Identifier.Text;
                    var shortTypeName = typeSymbol?.Name ?? type.Identifier.Text;

                    foreach (var method in type.Members.OfType<MethodDeclarationSyntax>())
                    {
                        var methodSymbol = semanticModel.GetDeclaredSymbol(method);
                        var callerId = methodSymbol?.ToDisplayString() ?? $"{typeName}.{method.Identifier.Text}";

                        // Find all invocations inside this method
                        foreach (var invocation in method.DescendantNodes().OfType<InvocationExpressionSyntax>())
                        {
                            var calleeId = ResolveInvocation(invocation, semanticModel, methodIndex, qualifiedIndex, shortTypeName, ct);
                            if (calleeId != null && calleeId != callerId)
                            {
                                var lineSpan = invocation.GetLocation().GetLineSpan();
                                var exprText = invocation.Expression.ToString();

                                graph.AddEdge(new CallEdge
                                {
                                    CallerId = callerId,
                                    CalleeId = calleeId,
                                    CallSiteFile = filePath,
                                    CallSiteLine = lineSpan.StartLinePosition.Line + 1,
                                    IsAsync = exprText.Contains("await") || method.Modifiers.Any(SyntaxKind.AsyncKeyword),
                                    Confidence = 1.0
                                });
                            }
                        }

                        // Also catch object creation (new Foo())
                        foreach (var creation in method.DescendantNodes().OfType<ObjectCreationExpressionSyntax>())
                        {
                            var createdTypeName = creation.Type.ToString();
                            // Strip generic args
                            var idx = createdTypeName.IndexOf('<');
                            if (idx > 0) createdTypeName = createdTypeName.Substring(0, idx);

                            if (methodIndex.TryGetValue(createdTypeName, out var ctorIds))
                            {
                                var lineSpan = creation.GetLocation().GetLineSpan();
                                graph.AddEdge(new CallEdge
                                {
                                    CallerId = callerId,
                                    CalleeId = ctorIds[0],
                                    CallSiteFile = filePath,
                                    CallSiteLine = lineSpan.StartLinePosition.Line + 1,
                                    Confidence = 0.8
                                });
                            }
                        }
                    }
                }

                processed++;
                progress?.Report(new AnalysisProgress
                {
                    Phase = "Building call graph",
                    CurrentFile = Path.GetFileName(filePath),
                    FilesProcessed = processed,
                    TotalFiles = total
                });
            }

            // PASS 3: Resolve dangling edges (interface/abstract calls -> concrete implementations)
            ResolveInterfaceEdges(graph, methodIndex);

            return graph;
        }

        private static void ResolveInterfaceEdges(CallGraph graph, Dictionary<string, List<string>> methodIndex)
        {
            var edgesToAdd = new List<CallEdge>();
            var edgesToRemove = new List<CallEdge>();

            foreach (var edge in graph.Edges)
            {
                // Resolve if callee is missing OR is an interface/abstract method
                var calleeTypeName = ExtractTypeName(edge.CalleeId);
                bool isInterfaceCall = calleeTypeName != null &&
                    calleeTypeName.StartsWith("I") && calleeTypeName.Length > 1 && char.IsUpper(calleeTypeName[1]);
                bool calleeMissing = !graph.Nodes.ContainsKey(edge.CalleeId);
                bool calleeHasNoEdges = !calleeMissing && !graph.Edges.Any(e => e.CallerId == edge.CalleeId);

                if (calleeMissing || (isInterfaceCall && calleeHasNoEdges))
                {
                    // Extract the method name from the callee ID
                    // e.g. "CodeFlow3D.Services.ICallGraphBuilder.BuildAsync(...)" -> "BuildAsync"
                    var calleeId = edge.CalleeId;
                    var methodName = ExtractMethodName(calleeId);

                    if (methodName != null && methodIndex.TryGetValue(methodName, out var candidates))
                    {
                        // Find the best match — prefer concrete implementation
                        string bestMatch = null;

                        // Try to match by interface name -> implementation name pattern
                        // IFoo.Method -> Foo.Method, FooImpl.Method, etc.
                        var typeName = ExtractTypeName(calleeId);
                        if (typeName != null && typeName.StartsWith("I") && typeName.Length > 1 && char.IsUpper(typeName[1]))
                        {
                            var implName = typeName.Substring(1); // ICallGraphBuilder -> CallGraphBuilder
                            bestMatch = candidates.FirstOrDefault(c =>
                                c.IndexOf($".{implName}.", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                c.IndexOf($".{implName}(", StringComparison.OrdinalIgnoreCase) >= 0);
                        }

                        if (bestMatch == null)
                            bestMatch = candidates.FirstOrDefault(c => graph.Nodes.ContainsKey(c));

                        if (bestMatch != null)
                        {
                            edgesToRemove.Add(edge);
                            edgesToAdd.Add(new CallEdge
                            {
                                CallerId = edge.CallerId,
                                CalleeId = bestMatch,
                                CallSiteFile = edge.CallSiteFile,
                                CallSiteLine = edge.CallSiteLine,
                                IsAsync = edge.IsAsync,
                                IsVirtual = true,
                                Confidence = edge.Confidence * 0.9
                            });
                        }
                    }
                }
            }

            foreach (var e in edgesToRemove)
                graph.Edges.Remove(e);
            foreach (var e in edgesToAdd)
                graph.AddEdge(e);
        }

        private static string ExtractMethodName(string fullId)
        {
            // "Namespace.Type.Method(params)" -> "Method"
            var parenIdx = fullId.IndexOf('(');
            var namepart = parenIdx > 0 ? fullId.Substring(0, parenIdx) : fullId;
            var dotIdx = namepart.LastIndexOf('.');
            return dotIdx >= 0 ? namepart.Substring(dotIdx + 1) : namepart;
        }

        private static string ExtractTypeName(string fullId)
        {
            // "Namespace.Type.Method(params)" -> "Type"
            var parenIdx = fullId.IndexOf('(');
            var namepart = parenIdx > 0 ? fullId.Substring(0, parenIdx) : fullId;
            var parts = namepart.Split('.');
            return parts.Length >= 2 ? parts[parts.Length - 2] : null;
        }

        private string ResolveInvocation(
            InvocationExpressionSyntax invocation,
            SemanticModel semanticModel,
            Dictionary<string, List<string>> methodIndex,
            Dictionary<string, string> qualifiedIndex,
            string currentTypeName,
            CancellationToken ct)
        {
            // Strategy 1: Semantic resolution (works when compilation has all references)
            var symbolInfo = semanticModel.GetSymbolInfo(invocation, ct);
            var calledSymbol = symbolInfo.Symbol ?? symbolInfo.CandidateSymbols.FirstOrDefault();
            if (calledSymbol is IMethodSymbol calledMethod)
            {
                var semanticId = calledMethod.ToDisplayString();
                // Only use if it's a method we know about (in our project)
                if (methodIndex.Values.Any(list => list.Contains(semanticId)))
                    return semanticId;
            }

            // Strategy 2: Syntax-based name matching
            var expr = invocation.Expression;
            string calledName = null;
            string qualifierName = null;

            if (expr is MemberAccessExpressionSyntax memberAccess)
            {
                calledName = memberAccess.Name.Identifier.Text;

                // Try to figure out the type from the expression
                // e.g. _pathFinder.FindAllPaths -> qualifier = "_pathFinder"
                var qualifier = memberAccess.Expression.ToString();

                // Try semantic type info on the qualifier
                var typeInfo = semanticModel.GetTypeInfo(memberAccess.Expression, ct);
                if (typeInfo.Type != null)
                {
                    qualifierName = typeInfo.Type.Name;
                }
                else
                {
                    // Heuristic: strip _ prefix and capitalize for field names
                    qualifier = qualifier.TrimStart('_');
                    if (qualifier.Length > 0)
                        qualifierName = char.ToUpper(qualifier[0]) + qualifier.Substring(1);
                }
            }
            else if (expr is IdentifierNameSyntax identifier)
            {
                calledName = identifier.Identifier.Text;
                // Calling a method in the same class
                qualifierName = currentTypeName;
            }
            else if (expr is GenericNameSyntax generic)
            {
                calledName = generic.Identifier.Text;
            }

            if (calledName == null)
                return null;

            // Try qualified match first: "TypeName.MethodName"
            if (qualifierName != null && qualifiedIndex.TryGetValue($"{qualifierName}.{calledName}", out var qualifiedId))
                return qualifiedId;

            // Try same-class match
            if (qualifiedIndex.TryGetValue($"{currentTypeName}.{calledName}", out var sameClassId))
                return sameClassId;

            // Try by method name alone (if unique or just pick first)
            if (methodIndex.TryGetValue(calledName, out var candidates) && candidates.Count > 0)
            {
                // Prefer match in same type
                var sameType = candidates.FirstOrDefault(c => c.Contains($"{currentTypeName}."));
                if (sameType != null) return sameType;

                // If qualifier name hint matches
                if (qualifierName != null)
                {
                    var hinted = candidates.FirstOrDefault(c =>
                        c.IndexOf(qualifierName, StringComparison.OrdinalIgnoreCase) >= 0);
                    if (hinted != null) return hinted;
                }

                // Single candidate = safe bet
                if (candidates.Count == 1)
                    return candidates[0];

                // Multiple candidates, return first (lower confidence, but still useful)
                return candidates[0];
            }

            return null;
        }

        private static string BuildSignature(MethodDeclarationSyntax method)
        {
            var parameters = method.ParameterList?.Parameters
                .Select(p => $"{p.Type} {p.Identifier.Text}") ?? Enumerable.Empty<string>();
            var returnType = method.ReturnType?.ToString() ?? "void";
            return $"{returnType} {method.Identifier.Text}({string.Join(", ", parameters)})";
        }

        private static MetadataReference[] GetFrameworkReferences()
        {
            var refs = new List<MetadataReference>();

            // Add all currently loaded assemblies that have a location
            var assemblies = AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
                .Select(a => a.Location)
                .Distinct()
                .ToList();

            foreach (var loc in assemblies)
            {
                try
                {
                    refs.Add(MetadataReference.CreateFromFile(loc));
                }
                catch
                {
                    // Skip assemblies we can't load
                }
            }

            // Ensure core types are included
            var coreTypes = new[]
            {
                typeof(object),
                typeof(Enumerable),
                typeof(Task),
                typeof(System.Collections.Generic.List<>),
                typeof(System.ComponentModel.INotifyPropertyChanged),
            };

            foreach (var type in coreTypes)
            {
                try
                {
                    var loc = type.Assembly.Location;
                    if (!string.IsNullOrEmpty(loc) && !assemblies.Contains(loc))
                        refs.Add(MetadataReference.CreateFromFile(loc));
                }
                catch { }
            }

            return refs.ToArray();
        }

        public async Task<CallGraph> AnalyzeFileAsync(string filePath, CancellationToken ct = default)
        {
            var dir = Path.GetDirectoryName(filePath);
            return await AnalyzeAsync(dir, null, ct);
        }
    }
}
