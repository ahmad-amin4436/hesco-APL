namespace HESCO.Models
{
    public class MappingApprovedListViewModel
    {
        public int Id { get; set; }
        public string IMEI { get; set; }
        public string ProjectName { get; set; }
        public string MSN { get; set; }
        public string IMSI { get; set; }
        public string NewIMEI { get; set; }
        public string NewOptoComCode { get; set; }
        public string NewIMSI { get; set; }
        public string OptoComCode { get; set; }
        public string UpdateReason { get; set; }
        public string ApproveReason { get; set; }
        public string DisapproveReason { get; set; }
        public int status { get; set; }
        public DateTime RequestedAt { get; set; }
        public int RequestedBy { get; set; }
        public DateTime ApprovedAt { get; set; }
        public int ApprovedBy { get; set; }
        public DateTime DispprovedAt { get; set; }
        public List<string> Images { get; set; }
        public int DisapprovedBy { get; set; }
        public string RequestedByUsername { get; set; }
        public string ApprovedByUsername { get; set; }
        public string DisapprovedByUsername { get; set; }
        public string DisplayStatus
        {
            get
            {
                return status switch
                {
                    0 => "Pending",
                    1 => "Approved",
                    2 => "Disapproved",
                    _ => "Unknown"
                };
            }
        }
    }
}
