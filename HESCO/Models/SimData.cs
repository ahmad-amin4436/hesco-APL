namespace HESCO.Models
{
    public class SimData
    {
        public int Id { get; set; }
        public string IMSI { get; set; }
        public string SimNumber { get; set; }
        public string Operator { get; set; }
        public string DataDetails { get; set; }
        public string MonthlyBill { get; set; }
        public DateTime CreatedAt { get; set; }
        public int CreatedBy { get; set; }
        public string Project { get; set; }
    }
}
