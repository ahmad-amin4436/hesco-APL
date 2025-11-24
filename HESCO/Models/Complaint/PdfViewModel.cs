namespace HESCO.Models.Complaint
{
    public class PdfViewModel
    {
        public List<GroupedReportData> GroupedData { get; set; }
        public dynamic GrandTotal { get; set; }
        public string Logo1Base64 { get; set; }
        public string Logo2Base64 { get; set; }
    }
}
