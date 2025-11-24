namespace HESCO.Models
{
    public class SimDataViewModel
    {
        public int Id { get; set; }
        public string IMSI { get; set; }
        public string SimNumber { get; set; }
        public string Operator { get; set; }
        public string DataDetails { get; set; }
        public string MonthlyBill { get; set; }
        public string PlanName { get; set; }
        public int SimMapFlag { get; set; }
        public DateTime CreatedAt { get; set; }
        public int CreatedBy { get; set; }
        public string ChangeProjectName { get; set; }
        public DateTime ChangeProjectIdAt { get; set; }
        public int BillDate { get; set; }
        public string CreatedByUsername { get; set; }
        public string ProjectName { get; set; }
        public string SimMapFlagDisplay
        {
            get
            {
                return SimMapFlag switch
                {
                    0 => "No",
                    1 => "Yes",
                    _ => "Unknown"
                };
            }
        }

    }
}
