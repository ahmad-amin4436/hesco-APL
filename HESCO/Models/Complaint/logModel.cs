namespace HESCO.Models.Complaint
{
    public class logModel
    {
        public int Id { get; set; }
        public int userId { get; set; }
        public string Action { get; set; }
        public string? Result { get; set; }
        public DateTime ActionDate { get; set; }
        public string? UserIP { get; set; }
        public DateTime? ResultDate { get; set; }
        public int complaintId { get; set; }
    }
}
