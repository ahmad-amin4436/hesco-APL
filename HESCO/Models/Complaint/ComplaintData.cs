namespace HESCO.Models.Complaint
{
    public class ComplaintData
    {
        public int ComplaintId { get; set; }
        public string Reference_No { get; set; }
        public string MSN { get; set; }
        public string? URL { get; set; }
        public string Address { get; set; }
        public string? Old_Imsi { get; set; }
        public string? New_Imsi { get; set; }
        public string? Telco_New { get; set; }
        public string? Telco_Old { get; set; }
        public string SubDivisionCode { get; set; }
        public string SubDivisionName { get; set; }
        public string? Project { get; set; }
        //public string SubDiv { get; set; }
        public string? Open_Description { get; set; }
        public string? Executed_Description { get; set; }
        public string? ExecutedDescriptionText { get; set; }
        public string? OtherExecutedDescription { get; set; }
        public DateTime? OpenedAt { get; set; }
        public DateTime? Execution_Date { get; set; }
        public int OpenedBy { get; set; }
        public int? AssignedTo { get; set; }
        public int Status { get; set; }
        public string ComplaintReportedBy { get; set; }
        public DateTime? lastCommunicationTime { get; set; }
        public int? DataReset { get; set; }
        public DateTime? Replaced_Date { get; set; }
        public DateTime? RemovedDate { get; set; }
        public DateTime? TobeRemovedDate { get; set; }
        public DateTime? MuteDate { get; set; }
        public int IsFaulty { get; set; }
        public int? Priority { get; set; }
       public string Remarks { get; set; }
    }
}
