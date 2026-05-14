using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using CodeFlow3D.Models;

namespace CodeFlow3D.Analysis
{
    public class FunctionSimulatorService
    {
        public SimulationSession CreateSession(SymbolNode function, Dictionary<string, string> paramValues)
        {
            if (string.IsNullOrEmpty(function.FilePath) || !File.Exists(function.FilePath))
                return null;

            var sourceCode = File.ReadAllText(function.FilePath);
            var tree = CSharpSyntaxTree.ParseText(sourceCode);
            var root = (CompilationUnitSyntax)tree.GetRoot();

            var method = FindMethod(root, function.Name, function.LineStart);
            if (method == null)
                return null;

            var span = method.GetLocation().GetLineSpan();
            var startLine = span.StartLinePosition.Line + 1;
            var endLine   = span.EndLinePosition.Line + 1;

            var paramInfos = method.ParameterList.Parameters.Select(p => new ParameterInfo
            {
                Name         = p.Identifier.Text,
                TypeName     = p.Type?.ToString() ?? "var",
                DefaultValue = p.Default?.Value.ToString()
            }).ToList();

            // Merge user-supplied values with defaults
            var merged = new Dictionary<string, string>();
            foreach (var pi in paramInfos)
                merged[pi.Name] = paramValues.ContainsKey(pi.Name)
                    ? paramValues[pi.Name]
                    : (pi.DefaultValue ?? "?");

            var walker = new SimulationWalker(merged, paramInfos, tree);
            var body = (SyntaxNode)method.Body ?? method.ExpressionBody;
            if (body != null)
                walker.Visit(body);

            return new SimulationSession
            {
                Function        = function,
                Parameters      = paramInfos,
                ParameterValues = merged,
                Steps           = walker.Steps,
                SourceCode      = sourceCode,
                FunctionStartLine = startLine,
                FunctionEndLine   = endLine
            };
        }

        private static MethodDeclarationSyntax FindMethod(SyntaxNode root, string name, int lineHint)
        {
            var candidates = root.DescendantNodes()
                .OfType<MethodDeclarationSyntax>()
                .Where(m => m.Identifier.Text == name)
                .ToList();

            if (candidates.Count == 0) return null;

            if (lineHint > 0)
            {
                return candidates.OrderBy(m =>
                    Math.Abs(m.GetLocation().GetLineSpan().StartLinePosition.Line + 1 - lineHint)).First();
            }

            return candidates[0];
        }
    }

    internal class SimulationWalker : CSharpSyntaxWalker
    {
        public List<SimulationStep> Steps { get; } = new List<SimulationStep>();

        private readonly SyntaxTree _tree;
        private readonly Dictionary<string, string> _vars;
        private readonly Dictionary<string, string> _varTypes = new Dictionary<string, string>();
        private readonly HashSet<string> _paramNames;
        private int _stepIndex;

        public SimulationWalker(Dictionary<string, string> initialVars,
                                List<ParameterInfo> paramInfos,
                                SyntaxTree tree)
        {
            _vars       = new Dictionary<string, string>(initialVars);
            _paramNames = new HashSet<string>(initialVars.Keys);
            _tree       = tree;

            // Seed types for parameters
            foreach (var p in paramInfos)
                _varTypes[p.Name] = p.TypeName;
        }

        public override void VisitBlock(BlockSyntax node)
        {
            foreach (var stmt in node.Statements)
                Visit(stmt);
        }

        public override void VisitLocalDeclarationStatement(LocalDeclarationStatementSyntax node)
        {
            foreach (var decl in node.Declaration.Variables)
            {
                var name     = decl.Identifier.Text;
                var typeName = node.Declaration.Type.ToString();
                var initExpr = decl.Initializer?.Value;
                var initText = initExpr?.ToString() ?? "null";

                // Resolve type from expression when declared as 'var'
                if (typeName == "var")
                    typeName = InferType(initExpr) ?? InferTypeFromText(initText);

                _varTypes[name] = typeName;

                // Store a human-readable value (not raw source code)
                var value = ResolveValue(initExpr, initText);
                _vars[name] = value;

                AddStep(node, SimStepKind.VarDecl,
                    $"var {name} = {Shorten(initText)}",
                    newVar: name,
                    calledMethod: ExtractCallName(initExpr));
            }
        }

        public override void VisitExpressionStatement(ExpressionStatementSyntax node)
        {
            var expr = node.Expression;

            if (expr is AssignmentExpressionSyntax assign)
            {
                var target = assign.Left.ToString();
                var rhs    = assign.Right.ToString();
                var value  = ResolveValue(assign.Right, rhs);

                if (_vars.ContainsKey(target))
                {
                    _vars[target] = value;
                    AddStep(node, SimStepKind.Assignment,
                        $"{Shorten(target, 18)} = {Shorten(rhs)}",
                        changedVar: target);
                }
                else
                {
                    AddStep(node, SimStepKind.Assignment,
                        $"{Shorten(target, 18)} = {Shorten(rhs)}");
                }
                return;
            }

            if (expr is InvocationExpressionSyntax inv)
            {
                AddStep(node, SimStepKind.Call,
                    Truncate(expr.ToString(), 60),
                    calledMethod: ExtractCallName(inv));
                return;
            }

            if (expr is AwaitExpressionSyntax awaitExpr)
            {
                AddStep(node, SimStepKind.Await,
                    $"await {Truncate(awaitExpr.Expression.ToString(), 50)}",
                    calledMethod: ExtractCallName(awaitExpr.Expression));
                return;
            }

            AddStep(node, SimStepKind.Statement, Truncate(expr.ToString(), 60));
        }

        public override void VisitReturnStatement(ReturnStatementSyntax node)
        {
            var text = node.Expression != null
                ? $"return {Truncate(node.Expression.ToString(), 50)}"
                : "return";
            AddStep(node, SimStepKind.Return, text);
        }

        public override void VisitIfStatement(IfStatementSyntax node)
        {
            var cond = Truncate(node.Condition.ToString(), 50);
            AddStep(node, SimStepKind.Branch, $"if ({cond})",
                isBranch: true, branchCond: cond);
            Visit(node.Statement);
            if (node.Else != null)
                Visit(node.Else.Statement);
        }

        public override void VisitForEachStatement(ForEachStatementSyntax node)
        {
            AddStep(node, SimStepKind.LoopStart,
                $"foreach ({node.Type} {node.Identifier} in {Truncate(node.Expression.ToString(), 30)})");
            Visit(node.Statement);
        }

        public override void VisitForStatement(ForStatementSyntax node)
        {
            AddStep(node, SimStepKind.LoopStart,
                Truncate(node.ToString().Split('{')[0].Trim(), 60));
            Visit(node.Statement);
        }

        public override void VisitWhileStatement(WhileStatementSyntax node)
        {
            AddStep(node, SimStepKind.LoopStart,
                $"while ({Truncate(node.Condition.ToString(), 40)})");
            Visit(node.Statement);
        }

        public override void VisitTryStatement(TryStatementSyntax node)
        {
            Visit(node.Block);
            foreach (var c in node.Catches)
            {
                AddStep(c, SimStepKind.Branch,
                    $"catch ({c.Declaration?.ToString() ?? "Exception"})");
                Visit(c.Block);
            }
            if (node.Finally != null)
            {
                AddStep(node.Finally, SimStepKind.Statement, "finally");
                Visit(node.Finally.Block);
            }
        }

        // ── Snapshot builder ──────────────────────────────────────────────────

        private void AddStep(SyntaxNode node, SimStepKind kind, string text,
            string newVar = null, string changedVar = null,
            string calledMethod = null, bool isBranch = false, string branchCond = null)
        {
            var line = _tree.GetLineSpan(node.Span).StartLinePosition.Line + 1;

            var snapshot = _vars.Select(kv => new VariableState
            {
                Name        = kv.Key,
                Value       = kv.Value,
                TypeName    = _varTypes.TryGetValue(kv.Key, out var t) ? t : "",
                IsParameter = _paramNames.Contains(kv.Key),
                JustChanged = kv.Key == changedVar || kv.Key == newVar,
            }).ToList();

            Steps.Add(new SimulationStep
            {
                Index            = _stepIndex++,
                LineNumber       = line,
                StatementText    = text,
                Kind             = kind,
                CalledMethodName = calledMethod,
                Variables        = snapshot,
                ChangedVarNames  = new List<string>(
                    new[] { newVar, changedVar }.Where(v => v != null)),
                IsBranchPoint    = isBranch,
                BranchCondition  = branchCond,
            });
        }

        // ── Value resolution: turns source expressions into readable values ──

        private string ResolveValue(SyntaxNode exprNode, string fallbackText)
        {
            if (exprNode == null) return "null";

            switch (exprNode)
            {
                // Literals: keep as-is
                case LiteralExpressionSyntax _:
                    return exprNode.ToString();

                // Variable reference: try to resolve
                case IdentifierNameSyntax id:
                    if (_vars.TryGetValue(id.Identifier.Text, out var known))
                        return known;
                    return id.Identifier.Text;

                // new Type(…)  →  {TypeName}
                case ObjectCreationExpressionSyntax oc:
                {
                    var typePart = oc.Type.ToString().Split('<')[0].Split('.').Last();
                    return $"{{{typePart}}}";
                }

                // default(T) / typeof(T) etc.
                case DefaultExpressionSyntax _:
                    return "default";

                // null!
                case PostfixUnaryExpressionSyntax post
                    when post.OperatorToken.Text == "!":
                    return ResolveValue(post.Operand, fallbackText);

                // Await expression: resolve inner
                case AwaitExpressionSyntax aw:
                    return ResolveValue(aw.Expression, fallbackText);

                // Invocation: method call result  →  ←MethodName()
                case InvocationExpressionSyntax inv:
                {
                    var mn = ExtractCallName(inv) ?? "?";
                    return $"←{mn}()";
                }

                // true/false/null keywords
                case PredefinedTypeSyntax _:
                    return exprNode.ToString();

                // Binary: try to evaluate numerics and string concat
                case BinaryExpressionSyntax bin:
                {
                    var lv = ResolveValue(bin.Left,  bin.Left.ToString());
                    var rv = ResolveValue(bin.Right, bin.Right.ToString());
                    var op = bin.OperatorToken.Text;

                    // Numeric: both sides are integers
                    if (long.TryParse(lv, out long li) && long.TryParse(rv, out long ri))
                    {
                        switch (op)
                        {
                            case "+": return (li + ri).ToString();
                            case "-": return (li - ri).ToString();
                            case "*": return (li * ri).ToString();
                            case "/": return ri != 0 ? (li / ri).ToString() : "0";
                            case "%": return ri != 0 ? (li % ri).ToString() : "0";
                        }
                    }

                    // String concatenation
                    if (op == "+")
                        return StringConcat(lv, rv);

                    return Truncate($"{lv} {op} {rv}", 22);
                }
            }

            // LINQ chain  (e.g. files.Where(f => …).ToList())
            var text = fallbackText.Trim();
            foreach (var op in new[] { ".Where(", ".Select(", ".OrderBy(", ".GroupBy(",
                                       ".ToList(", ".ToDictionary(", ".FirstOrDefault(", ".Any(" })
            {
                int idx = text.IndexOf(op, StringComparison.Ordinal);
                if (idx < 0) continue;
                var src    = text.Substring(0, idx).Split('.').Last().Split('(')[0];
                var opName = op.TrimStart('.').TrimEnd('(');
                return $"[{src}.{opName}(…)]";
            }

            // Conditional / ternary
            if (text.Contains(" ? ") && text.Contains(" : "))
                return "?: …";

            // Generic fallback: keep it short
            return Truncate(text, 22);
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private string ExtractCallName(SyntaxNode node)
        {
            if (node == null) return null;
            if (node is InvocationExpressionSyntax inv)
            {
                var expr   = inv.Expression.ToString();
                var dotIdx = expr.LastIndexOf('.');
                return dotIdx >= 0 ? expr.Substring(dotIdx + 1) : expr;
            }
            if (node is AwaitExpressionSyntax a)
                return ExtractCallName(a.Expression);
            if (node is MemberAccessExpressionSyntax ma)
                return ma.Name.Identifier.Text;
            return null;
        }

        // Infer type from Roslyn syntax node (more precise than text-based)
        private static string InferType(SyntaxNode exprNode)
        {
            if (exprNode == null) return null;
            switch (exprNode)
            {
                case LiteralExpressionSyntax lit:
                    if (lit.Token.IsKind(SyntaxKind.StringLiteralToken) ||
                        lit.Token.IsKind(SyntaxKind.InterpolatedStringStartToken))
                        return "string";
                    if (lit.Token.IsKind(SyntaxKind.TrueKeyword) ||
                        lit.Token.IsKind(SyntaxKind.FalseKeyword))
                        return "bool";
                    if (lit.Token.IsKind(SyntaxKind.NumericLiteralToken))
                        return lit.Token.Text.Contains(".") ? "double" : "int";
                    return null;
                case ObjectCreationExpressionSyntax oc:
                    return oc.Type.ToString();
                case InvocationExpressionSyntax _:
                    return null; // can't infer without semantic model
                default:
                    return null;
            }
        }

        private static string InferTypeFromText(string initText)
        {
            if (initText == null) return "var";
            if (initText.StartsWith("\"") || initText.StartsWith("@\"")) return "string";
            if (initText == "true" || initText == "false") return "bool";
            if (initText == "null") return "object";
            if (initText.Contains(".") && double.TryParse(initText,
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out _)) return "double";
            if (int.TryParse(initText, out _)) return "int";
            if (initText.StartsWith("new "))
            {
                var rest = initText.Substring(4).Split('(')[0].Split('<')[0].Trim();
                return rest;
            }
            return "var";
        }

        // Shorten for statement display (keeps statement text readable)
        private static string Shorten(string s, int max = 40)
        {
            if (s == null) return "";
            s = s.Replace("\n", " ").Replace("\r", " ").Trim();
            // Collapse inner whitespace
            while (s.Contains("  ")) s = s.Replace("  ", " ");
            return s.Length > max ? s.Substring(0, max - 1) + "…" : s;
        }

        private static string StringConcat(string a, string b)
        {
            // Strip surrounding quotes so we can merge, then re-wrap
            string Strip(string s) => s.StartsWith("\"") && s.EndsWith("\"") && s.Length >= 2
                ? s.Substring(1, s.Length - 2) : s;
            return $"\"{Strip(a)}{Strip(b)}\"";
        }

        private static string Truncate(string s, int max = 60)
        {
            if (s == null) return "";
            s = s.Replace("\n", " ").Replace("\r", " ").Trim();
            return s.Length > max ? s.Substring(0, max - 1) + "…" : s;
        }
    }
}
