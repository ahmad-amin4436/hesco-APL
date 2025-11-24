namespace HESCO.Models
{
    public class SimMappingDataViewModel
    {
        public int Id { get; set; }
        public int MeterId { get; set; }
        public int ImeiId { get; set; }
        public int BarcodeId { get; set; }
        public string IMEI { get; set; }
        public int ImsiId { get; set; }
        public string MSN { get; set; }
        public string IMSI { get; set; }
        public String? SimNumber { get; set; }
        public String? Operator { get; set; }
        public string BarcodeNo { get; set; }
        public DateTime CreatedAt { get; set; }
        public int CreatedBy { get; set; }
        public string CreatedByUsername { get; set; }
        public string UpdatedByUsername { get; set; }
        public DateTime UpdatedAt { get; set; }
        public int UpdatedBy { get; set; }
        public int projectId { get; set; }
        public string  projectName { get; set; }
        public string BarcodeProjectName { get; set; }
        public string IMSIProjectName { get; set; }
        public string IMEIProjectName { get; set; }
        public int status { get; set; }
        public string DisplayStatus
        {
            get
            {
				return status switch
				{
					-1 => "-",         // No status found in approved_list
					0 => "Pending",
					1 => "Approved",
					2 => "Disapproved",
					_ => "Unknown"
				};
			}
        }
    }
}
