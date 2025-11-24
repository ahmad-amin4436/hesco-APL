namespace HESCO.Models
{
    public class SimMappingEditModel
    {
        public int Id { get; set; }
        public int projectId { get; set; }
        public string IMEI { get; set; }
        public string MSN { get; set; }
        public string IMSI { get; set; }
        public string NewIMEI { get; set; }
        public string NewOptoComCode { get; set; }
        public string NewIMSI { get; set; }
        public string OptoComCode { get; set; }
        public string UpdateReason { get; set; }
        public DateTime RequestedAt { get; set; }
        public int RequestedBy { get; set; }
    }
}
