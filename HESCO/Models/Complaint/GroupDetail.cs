namespace HESCO.Models.Complaint
{
    public class GroupDetail
    {
        public int GroupId { get; set; }
        public string GroupName { get; set; }
        public string? MSN { get; set; }
        public string? ReferenceNo { get; set; }
        public int CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; }
        public int GroupType { get; set; }
        public string GroupTypeDisplay
        {
            get
            {
                return GroupType switch
                {
                    0 => "Complaint List",
                    1 => "FaultyMeter List",
                    2 => "Recieve Pending List",
                    _ => "Unknown" // Fallback for unexpected MeterStatus values
                };
            }
        }

    }
}
