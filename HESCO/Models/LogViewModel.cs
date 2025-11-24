namespace HESCO.Models
{
    public class LogViewModel
    {
        public int Id { get; set; }
        public string ActionUser { get; set; }
        public string Action { get; set; }
        public string Result { get; set; }
        public DateTime ActionDate { get; set; }
        public int simMappingId { get; set; }
    }
}
