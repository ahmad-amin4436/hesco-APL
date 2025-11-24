namespace HESCO.Models.Complaint
{
    public class ComplaintViewModel
    {
        public int Id { get; set; }
        public int InstallationId { get; set; }
        public string Remarks { get; set; }
        public string Reference_No { get; set; }
        public string ProjectName { get; set; }
        public string MSN { get; set; }
        public string? Old_Imsi { get; set; }
        public string? New_Imsi { get; set; }
        public string? Telco_New { get; set; }
        public string? Telco_Old { get; set; }
        public string? Address { get; set; }
        public string NewMSN { get; set; }
        public string CustomerId { get; set; }
        public string Open_Description { get; set; }
        public string Close_Description { get; set; }
        public string Executed_Description { get; set; }
        public string ExecutedDescriptionText { get; set; }
        public DateTime OpenedAt { get; set; }
        public DateTime ClosedAt { get; set; }
        public DateTime Execution_Date { get; set; }
        public DateTime DisplayDate { get; set; }
        public DateTime Replaced_Date { get; set; }
        public DateTime RemovedDate { get; set; }
        public DateTime MuteDate { get; set; }
        public DateTime ToBeCheckDate { get; set; }
        public DateTime TobeRemovedDate { get; set; }
        public int OpenedBy { get; set; }
        public int ClosedBy { get; set; }
        public int AssignedTo { get; set; }
        public string Latitude { get; set; }
        public string Longitude { get; set; }
        public DateTime LastCommunicationTime { get; set; }
        public string OpenedByUsername { get; set; }
        public string ClosedByUsername { get; set; }
        public string AssignedToUsername { get; set; }
        public string SubDiv { get; set; }
        public int Is_Faulty { get; set; }
        public int Status { get; set; }
        public DateTime WithSubdivDate { get; set; }
        public DateTime NotUnderWarrantyDate { get; set; }
        public DateTime? UnderWarrantyDate { get; set; }
        public DateTime ReturnedToSubdivDate { get; set; }
        public int MeterStatus { get; set; }
        public List<LocationModel> Locations { get; set; }
        public string IsFaultyDisplay => Is_Faulty == 1 ? "Yes" : "No";
        public List<string> Images { get; set; }
        public int Priority { get; set; }
        public string ComplaintReportedBy { get; set; }
        public string ReportedByUserName { get; set; }

        // Updated property to handle the additional case
        //public string ComplaintReportedByDisplay => ComplaintReportedBy switch
        //{
        //    0 => ReportedByUserName, // When ComplaintReportedBy is 0, show the ReportedByUserName
        //    1 => "Lesco LS",
        //    2 => "Lesco MI",
        //    3 => "Lesco SDO",
        //    4 => "Lesco XEN",
        //    5 => "Lesco SE",
        //    6 => "Lesco PIU",
        //    _ => ReportedByUserName // For any other value, show the ReportedByUserName
        //};
        public string StatusDisplay
        {
            get
            {
                return Status switch
                {
                    0 => "Open",
                    1 => "Executed",
                    2 => "Reinstalled/Replaced",
                    3 => "Close",
                    4 => "Mute",
                    5 => "To be check",
                    6 => "To be Removed",
                    7 => "Removed from site",
                    8 => "Returned to Subdivision",
                    9 => "Not Under Warranty",
                    10 => "With Sub-Division",
                    11 => "Under Warranty",
                    _ => "Unknown" // Fallback for unexpected status values
                };
            }
        }

        public string MeterStatusDisplay
        {
            get
            {
                return MeterStatus switch
                {
                    0 => "To be removed",
                    1 => "Received at factory",
                    _ => "Unknown" // Fallback for unexpected MeterStatus values
                };
            }
        }
    }
}
