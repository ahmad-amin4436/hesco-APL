namespace HESCO.Models
{
    public class MeterToSimMappingModel
    {
        public int Id { get; set; }
        public int MeterId { get; set; }
        public int projectId { get; set; }
        public int? ImeiId { get; set; }
        public int? ImsiId { get; set; }
        public int? BarcodeId { get; set; }
        public string MSN { get; set; }
        public string? IMEI { get; set; }
        public string? IMSI { get; set; }
        public String? SimNumber { get; set; }
        public String? Operator { get; set; }
        public string? OptoComCode { get; set; }
        public DateTime MappedAt { get; set; }
        public int MappedBy { get; set; }
        public string Project { get; set; }
        public DateTime UpdatedAt { get; set; }
        public DateTime map_datetime { get; set; }
        public int UpdatedBy { get; set; }
        public bool IsIMEIRequired { get; set; }  // Received from AJAX
        public bool IsIMSIRequired { get; set; }  // Received from AJAX
        public bool IsBarcodeRequired { get; set; }  // Received from AJAX
    }
}