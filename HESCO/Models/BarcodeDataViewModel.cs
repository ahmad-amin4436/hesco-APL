namespace HESCO.Models
{
    public class BarcodeDataViewModel
    {
        public int Id { get; set; }
        public string OptoComCode { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public int CreatedBy { get; set; }
        public string CreatedByUsername { get; set; }
        public int UpdatedBy { get; set; }
        public int BarcodeFlag { get; set; }
        public string UpdatedByUsername { get; set; }
        public string ProjectName { get; set; }
        public DateTime ChangeProjectIdAt { get; set; }
        public string ChangeProjectName { get; set; }
    }
}
