namespace HESCO.Models.Projects
{
    public class ProjectData
    {
        public int Id { get; set; }
        public string ProjectName { get; set; }
        public int CreatedBy { get; set; }
        public int MeterModeId { get; set; }
        public string? CreatedByUsername { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool IsIMEI { get; set; } = false;
        public bool IsIMSI { get; set; } = false;
        public bool IsBarcode { get; set; } = false;

    }
}
