namespace HESCO.Models.Complaint
{
    public class LogViewModel
    {
        public int Id { get; set; }
        public string ActionUser { get; set; }
        public string Action { get; set; }
        public DateTime ActionDate { get; set; }
        public int complaintId { get; set; }
    }
}
