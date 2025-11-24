namespace HESCO.Models
{
    public class logModel
    {
        public int Id { get; set; }
        public int userId { get; set; }
        public string Action { get; set; }
        public string Result { get; set; }
        public DateTime ActionDate { get; set; } 
        public int simMappingId { get; set; }
    }
}
