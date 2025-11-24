namespace HESCO.Models.Complaint
{
    public class FaultyMeterModel
    {
        public int Id { get; set; }
        public string MSN { get; set; }
        public string SubDiv { get; set; }
        public string ProjectName { get; set; }
        public DateTime OpenedAt { get; set; }
        public string OpenedBy { get; set; } // Display username instead of ID
        public string OpenDescription { get; set; }
        public string AssignedTo { get; set; }
        public string Fault_Description { get; set; }
        public string Remarks { get; set; }
        public string Dispatched_Remarks { get; set; }
        public int Status { get; set; }
        public int ComplaintStatus { get; set; }
        public DateTime ExecutedAt { get; set; }
        public string ExecutedBy { get; set; }
        public string DispatchedBy { get; set; }
        public DateTime DispatchedAt { get; set; }
        public string VerifiedBy { get; set; }
        public DateTime VerifiedAt { get; set; }
        public DateTime RemovedDate { get; set; }
        public int DataReset { get; set; }
        public int Received { get; set; }
        public DateTime ReceivedDate { get; set; }
        public List<string> Images { get; set; }
        public string DataResetDisplay => DataReset == 1 ? "Yes" : "No";
        public string ReceivedDisplay => Received == 1 ? "Yes" : "No";
        public string StatusDisplay
        {
            get
            {
                return Status switch
                {
                    0 => "Open",
                    1 => "Executed",
                    2 => "Verified",
                    3 => "Dispatched",
                    _ => "Unknown" // Fallback for unexpected status values
                };
            }
        }
    }
}
