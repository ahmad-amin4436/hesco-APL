using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace HESCO.Models.SubDivision
{
    public class MeterSubdivision
    {
   
        public int Id { get; set; }
        public string SubDivisionCode { get; set; }
        public string SubDivisionName { get; set; }
    }
}
