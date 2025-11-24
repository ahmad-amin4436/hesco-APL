namespace HESCO.Models
{
    public class AprrovedOrDisapprovedMappingModel
    {
        public int Id { get; set; }
        public int ImsiId { get; set; }
        public int ProjectId { get; set; }
        public int ImeiId { get; set; }
        public int OptoComCodeId { get; set; }
        public string IMEI { get; set; }
        public string MSN { get; set; }
        public string IMSI { get; set; }
        public string OptoComCode { get; set; }
        public string NewIMEI { get; set; }
        public string NewOptoComCode { get; set; }
        public List<string>? Images { get; set; }
        public string NewIMSI { get; set; }
        public string? SimNumber { get; set; }
        public string? Operator { get; set; }
        public string? ApproveReason { get; set; }
        public string UpdateReason { get; set; }
        public string? DisapproveReason { get; set; }
        public int Status { get; set; }
        public DateTime ApprovedAt { get; set; }
        public DateTime map_datetime { get; set; }
        public int ApprovedBy { get; set; }
        public DateTime DispprovedAt { get; set; }
        public int UpdatedBy { get; set; }
        public DateTime UpdatedAt { get; set; }
        public int DisapprovedBy { get; set; }
    }
}
