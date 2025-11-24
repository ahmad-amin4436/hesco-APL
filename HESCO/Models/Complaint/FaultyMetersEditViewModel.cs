namespace HESCO.Models.Complaint
{
    public class FaultyMetersEditViewModel
    {
        public int Id { get; set; }
        public string? MSN { get; set; }
        //public string? SubDiv { get; set; }
        public string? Fault_Description { get; set; }
        public string? OpenDescription { get; set; }
        public string? Remarks { get; set; }
        public string? Dispatched_Remarks { get; set; }
        public int AssignedTo { get; set; }
        public int? Verified { get; set; }
        public int? ReceivedAtFactory { get; set; }
        public int? Status { get; set; }
        public int? ComplaintStatus { get; set; }
        public DateTime? ExecutedAt { get; set; }
        public int? ExecutedBy { get; set; }
        public int? DispatchedBy { get; set; }
        public DateTime? DispatchedAt { get; set; }
        public int? VerifiedBy { get; set; }
        public DateTime? VerifiedAt { get; set; }
        public int? DataReset { get; set; }
    }
}
