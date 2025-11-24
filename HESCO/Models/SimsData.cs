namespace HESCO.Models
{
    public class SimsData
    {

        public int MeterId { get; set; }
        public string MSN { get; set; }
        public string Description { get; set; }
        public string? MeterType { get; set; }
        public string? MeterTypeText { get; set; }
        public string MeterMode { get; set; }
        public string? Comments { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public int? UpdatedBy { get; set; }
        public int? CreatedBy { get; set; }
        public string Project { get; set; }
    }
}
