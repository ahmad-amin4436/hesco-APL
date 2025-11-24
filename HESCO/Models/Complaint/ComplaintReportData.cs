namespace HESCO.Models.Complaint
{
    public class ComplaintReportData
    {
        public string circle_name { get; set; }
        public string division_name { get; set; }
        public string sub_div_code { get; set; }
        public string sub_division_name { get; set; }
        public int status { get; set; }
        public int Total_Opened { get; set; }
        public int Total_Executed { get; set; }
        public int Total_Closed { get; set; }
        public int Total_Mute { get; set; }
        public int Total_ToBeRemoved{ get; set; }
        public int Total_Removed { get; set; }
        public int Total_Replaced { get; set; }
        public int Total_ToBeCheck { get; set; }
        public int TotalComplaints { get; set; }
        public int SubDivTotal { get; set; }
    }
}
