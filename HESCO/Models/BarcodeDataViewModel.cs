namespace HESCO.Models
{
    public class BarcodeDataViewModel
    {
        public int id { get; set; }
        public string serial_no { get; set; }
        public DateTime created_at { get; set; }
        public DateTime updated_at { get; set; }
        public DateTime map_datetime { get; set; }
        public int created_by { get; set; }
        public string created_by_username { get; set; }
        public int updated_by { get; set; }
        public int barcode_flag { get; set; }
        public string updated_by_username { get; set; }
        public string project_name { get; set; }
        public int project_id { get; set; }
        public int change_project_id { get; set; }
        public DateTime change_project_id_at { get; set; }
        public string change_project_name { get; set; }
    }
}
