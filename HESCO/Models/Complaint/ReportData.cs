namespace HESCO.Models.Complaint
{
    public class ReportData
    {
        public string circle_name { get; set; }
        public string division_name { get; set; }
        public string sub_div_code { get; set; }
        public string sub_division_name { get; set; }
        public int allocated { get; set; }
        public int received { get; set; }
        public int today_installed { get; set; }
        public List<string> Images { get; set; }
        public int PostInstalled { get; set; }
        public int total_installed { get; set; }
        public int balance { get; set; }
       public string remarks { get;set; }
        public int Returned { get; set; }
    }
}
