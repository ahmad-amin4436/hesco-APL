namespace HESCO.Models.Complaint
{
    public class GroupedReportData
    {

        public string CircleName { get; set; }
        public List<ReportData> Items { get; set; }
        public int TotalAllocated { get; set; }
        public int TotalReceived { get; set; }
        public int TotalTodayInstalled { get; set; }
        public int TotalTotalInstalled { get; set; }
        public int TotalPostInstalled { get; set; }
        public int TotalBalance { get; set; }
    }
}
