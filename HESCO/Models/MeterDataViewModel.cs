namespace HESCO.Models
{
    public class MeterDataViewModel
    {
        public int MeterId { get; set; }
        public string MSN { get; set; }
        public string Description { get; set; }
        public string MeterType { get; set; }
        public string Comments { get; set; }
        public int Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public int CreatedBy { get; set; }
        public string CreatedByUsername { get; set; }
        public int UpdatedBy { get; set; }
        public string UpdatedByUsername { get; set; }
        public string ProjectName { get; set; }
        public int MapFlag { get; set; }
        public string MeterMode { get; set; }

        public string MapFlagDisplay
        {
            get
            {
                return MapFlag switch
                {
                    0 => "No",
                    1 => "Yes",
                    _ => "Unknown" // Fallback for unexpected status values
                };
            }
        }
        public string MeterStatusDisplay
        {
            get
            {
                return Status switch
                {
                    0 => "In-Active",
                    1 => "Active",
                    _ => "Unknown" // Fallback for unexpected MeterStatus values
                };
            }
        }
    }
}
