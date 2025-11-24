namespace HESCO.Models.Complaint
{
    public class LocationModel
    {
        public int Id { get; set; }
        public int LocationBy { get; set; }
        public string URL { get; set; }
        public DateTime LocationAt { get; set; }
        public int complaintId { get; set; }
    }
}
