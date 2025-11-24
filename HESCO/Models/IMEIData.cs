namespace HESCO.Models
{
    public class IMEIData
    {
        public int Id { get; set; }
        public string IMEI { get; set; }        
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public int? UpdatedBy { get; set; }
        public int? CreatedBy { get; set; }
        public string Project { get; set; }
    }
}
