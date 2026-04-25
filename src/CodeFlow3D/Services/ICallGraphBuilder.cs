using System;
using System.Threading;
using System.Threading.Tasks;
using CodeFlow3D.Models;

namespace CodeFlow3D.Services
{
    public interface ICallGraphBuilder
    {
        Task<CallGraph> BuildAsync(string projectPath, IProgress<AnalysisProgress> progress, CancellationToken ct = default);
    }
}
