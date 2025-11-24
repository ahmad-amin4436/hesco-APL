namespace HESCO.Models
{

    public class IMEIDataViewModel
    {
        public int id { get; set; }
        public string imei { get; set; }
        public int uploaded_by { get; set; }
        public string uploaded_by_username { get; set; }
        public DateTime uploaded_at { get; set; }
        public int? updated_by { get; set; }
        public string updated_by_username { get; set; }
        public DateTime? updated_at { get; set; }
        public string status { get; set; }
        public DateTime? map_datetime { get; set; }
        public int? project_id { get; set; }
        public string project_name { get; set; }
        public int? change_project_id { get; set; }
        public string change_project_name { get; set; }
        public DateTime? change_project_id_at { get; set; }
    }
    //public class IMEIDataViewModel
    //{
    //    public int Id { get; set; }
    //    public string IMEI { get; set; }
    //    public DateTime CreatedAt { get; set; }
    //    public DateTime UpdatedAt { get; set; }
    //    public DateTime ChangeProjectIdAt { get; set; }
    //    public int CreatedBy { get; set; }
    //    public string CreatedByUsername { get; set; }
    //    public int UpdatedBy { get; set; }
    //    public string UpdatedByUsername { get; set; }
    //    public string Status { get; set; }
    //    public string ProjectName { get; set; }
    //    public string ChangeProjectName { get; set; }


    //}
}
