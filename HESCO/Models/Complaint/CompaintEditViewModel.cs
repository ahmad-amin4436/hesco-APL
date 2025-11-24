using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;

namespace HESCO.Models.Complaint
{
    public class ComplaintEditViewModel
    {
        //public Telco telco { get; set; }
        public int Id { get; set; }
        public string Reference_No { get; set; }
        public string? URL { get; set; }
        public string? Remarks { get; set; }
        public string MSN { get; set; }
        [RegularExpression(@"^\d{10}$", ErrorMessage = "New MSN must be exactly 10 digits.")]
        public string? NewMSN { get; set; }
        //public string? NewMSN { get; set; }
        public string? SubDiv { get; set; }
        public string? SubDivName { get; set; }

        public string? SubDivisionDisplay { get; set; }
        public string? Old_Imsi { get; set; }
        public string? New_Imsi { get; set; }
        public string? Telco_New { get; set; }
        public string? Telco_Old { get; set; }
        public string? Open_Description { get; set; }
        public string? Closed_Description { get; set; }
        public string? Executed_Description { get; set; }
        public string? ExecutedDescriptionText { get; set; }
        public string? OtherExecutedDescription { get; set; }
        public DateTime? Execution_Date { get; set; }
        public DateTime? Replaced_Date { get; set; }
        public DateTime? RemovedDate { get; set; }
        public DateTime? TobeRemovedDate { get; set; }
        public DateTime? MuteDate { get; set; }
        public DateTime? WithSubdivDate { get; set; }
        public DateTime? NotUnderWarrantyDate { get; set; }
        public DateTime? UnderWarrantyDate { get; set; }
        public DateTime? ReturnedToSubdivDate { get; set; }
        public DateTime ToBeCheckDate { get; set; }
        public int Status { get; set; }
        public int? AssignedTo { get; set; }
        public int IsFaulty { get; set; }
        public DateTime? ClosedAt { get; set; }
        public int? ClosedBy { get; set; }
        public int? Priority { get; set; }
        public int? DataReset { get; set; }
    }
}
