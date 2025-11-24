namespace HESCO.Models.Complaint
{
    public class FaultyMeterViewModel
    {
        public string MSN { get; set; }
        public string? Project { get; set; }
        public string OpenDescription { get; set; }
        public DateTime OpenedAt { get; set; }
        public int OpenedBy { get; set; }
        public int? ComplaintId { get; set; }
        public int AssignedTo { get; set; }
        public int? DataReset { get; set; }
    }
}
