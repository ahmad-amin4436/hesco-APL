namespace HESCO.Models
{
    public class IMSIMappingModel
    {
        public int Id { get; set; }       
        public int ImsiId { get; set; }
        public string MSN { get; set; }        
        public string IMSI { get; set; }
        public String? SimNumber { get; set; }
        public String? Operator { get; set; } 
        public DateTime UpdatedAt { get; set; }
        public int UpdatedBy { get; set; }
        public DateTime map_datetime { get; set; }
    }
}
