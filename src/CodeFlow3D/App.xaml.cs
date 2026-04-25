using System.Windows;
using Microsoft.Build.Locator;
using Microsoft.Extensions.DependencyInjection;
using CodeFlow3D.Services;
using CodeFlow3D.Analysis;
using CodeFlow3D.Graph;
using CodeFlow3D.ViewModels;

namespace CodeFlow3D
{
    public partial class App : Application
    {
        public static ServiceProvider ServiceProvider { get; private set; }

        protected override void OnStartup(StartupEventArgs e)
        {
            MSBuildLocator.RegisterDefaults();

            var services = new ServiceCollection();
            ConfigureServices(services);
            ServiceProvider = services.BuildServiceProvider();

            base.OnStartup(e);
        }

        private static void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton<AnalyzerFactory>();
            services.AddTransient<CSharpAnalyzer>();
            services.AddTransient<GenericAnalyzer>();
            services.AddSingleton<ICallGraphBuilder, CallGraphBuilder>();
            services.AddSingleton<IPathFinder, PathFinder>();

            services.AddSingleton<MainViewModel>();
            services.AddSingleton<ProjectExplorerViewModel>();
            services.AddSingleton<DiagramViewModel>();
            services.AddSingleton<CodePreviewViewModel>();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            ServiceProvider?.Dispose();
            base.OnExit(e);
        }
    }
}
