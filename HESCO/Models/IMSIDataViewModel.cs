namespace HESCO.Models
{
    public class IMSIDataViewModel
    {
        public int id { get; set; }
        public string imsi { get; set; }
        public string sim_number { get; set; }
        public string operator_name { get; set; }
        public int? data_details { get; set; }
        public int? sms_details { get; set; }
        public int? monthly_bill { get; set; }
        public int? bill_date { get; set; }
        public int? credit_limit { get; set; }
        public string plan_name { get; set; }
        public int created_by { get; set; }
        public string created_by_username { get; set; }
        public DateTime created_at { get; set; }
        public DateTime? returned_at { get; set; }
        public DateTime? updated_at { get; set; }
        public string status { get; set; }
        public int? updated_by { get; set; }
        public string updated_by_username { get; set; }
        public int? sim_status { get; set; }
        public int? issued_to { get; set; }
        public string issued_to_username { get; set; }
        public int? returned_by { get; set; }
        public string returned_by_username { get; set; }
        public int? historic { get; set; }
        public int? user_role { get; set; }
        public int? sim_re_issued_to { get; set; }
        public string sim_re_issued_to_username { get; set; }
        public int? visible { get; set; }
        public int? sim_mapflag { get; set; }
        public DateTime? map_datetime { get; set; }
        public int? project_id { get; set; }
        public string project_name { get; set; }
        public int? change_project_id { get; set; }
        public string change_project_name { get; set; }
        public DateTime? change_project_id_at { get; set; }
        public string meter_type { get; set; }
    }


}
