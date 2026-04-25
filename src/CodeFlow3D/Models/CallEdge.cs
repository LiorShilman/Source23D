namespace CodeFlow3D.Models
{
    public class CallEdge
    {
        public string CallerId { get; set; }
        public string CalleeId { get; set; }
        public string CallSiteFile { get; set; }
        public int CallSiteLine { get; set; }
        public bool IsAsync { get; set; }
        public bool IsVirtual { get; set; }
        public double Confidence { get; set; } = 1.0;

        public override string ToString() => $"{CallerId} -> {CalleeId}";
    }
}
