namespace HESCO.Models
{
    public class BarcodeData
    {
        public int Id { get; set; }
        public string OptoComCode { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public int? UpdatedBy { get; set; }
        public int? CreatedBy { get; set; }
        public string Project { get; set; }
    }
}
