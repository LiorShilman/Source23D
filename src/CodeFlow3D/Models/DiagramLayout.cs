using System.Collections.Generic;
using System.Windows.Media.Media3D;

namespace CodeFlow3D.Models
{
    public class DiagramLayout
    {
        public List<ParticipantLayout> Participants { get; set; } = new List<ParticipantLayout>();
        public List<ArrowLayout> Arrows { get; set; } = new List<ArrowLayout>();
        public double TotalWidth { get; set; }
        public double TotalHeight { get; set; }
        public double TotalDepth { get; set; }
    }

    public class ParticipantLayout
    {
        public string Id { get; set; }
        public string Label { get; set; }
        public string FilePath { get; set; }
        public double XPosition { get; set; }
        public double Height { get; set; }
        public bool IsSource { get; set; }
        public bool IsTarget { get; set; }
    }

    public class ArrowLayout
    {
        public string CallerId { get; set; }
        public string CalleeId { get; set; }
        public string Label { get; set; }
        public int SequenceIndex { get; set; }
        public double YPosition { get; set; }
        public double ZDepth { get; set; }
        public bool IsReturn { get; set; }
        public bool IsAsync { get; set; }
        public Point3D StartPoint { get; set; }
        public Point3D EndPoint { get; set; }
        public SymbolNode CallerNode { get; set; }
        public SymbolNode CalleeNode { get; set; }
    }
}
