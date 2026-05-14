namespace CodeFlow3D.Demo
{
    // This class exists solely to give the Function Simulator a clean, readable demo.
    // It is intentionally written with simple literals so variable values resolve fully.
    internal static class SimulatorShowcase
    {
        public static string AnalyzeCallGraph(string projectName, int nodeCount, bool verbose)
        {
            string status = "Initializing";
            int edgeCount = 0;
            int depth = 0;
            bool hasCircular = false;
            string report = projectName;

            // Phase 1 – scan
            status = "Scanning";
            edgeCount = nodeCount * 3;
            depth = 4;

            // Phase 2 – detect cycles
            if (nodeCount > 10)
            {
                hasCircular = true;
                depth = 6;
            }

            // Phase 3 – deepen if cyclic
            if (hasCircular)
                depth = 8;

            // Phase 4 – build report
            report = "Nodes: " + nodeCount;

            if (verbose)
                report = report + ", Edges: " + edgeCount;

            status = "Complete";
            return report;
        }
    }
}
