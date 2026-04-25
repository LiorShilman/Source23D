using System;
using System.Threading;
using System.Threading.Tasks;
using CodeFlow3D.Models;

namespace CodeFlow3D.Services
{
    public interface IProjectAnalyzer
    {
        string Language { get; }
        string[] SupportedExtensions { get; }
        Task<CallGraph> AnalyzeAsync(string projectPath, IProgress<AnalysisProgress> progress, CancellationToken ct = default);
        Task<CallGraph> AnalyzeFileAsync(string filePath, CancellationToken ct = default);
    }
}
