namespace CodeFlow3D.Models
{
    public class AnalysisProgress
    {
        public string Phase { get; set; }
        public string CurrentFile { get; set; }
        public int FilesProcessed { get; set; }
        public int TotalFiles { get; set; }
        public double Percentage => TotalFiles > 0 ? (double)FilesProcessed / TotalFiles * 100 : 0;
    }
}
