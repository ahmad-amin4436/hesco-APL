using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace HESCO.Models.Installations
{
    public class MeterInstallation
    {
        public int Id { get; set; }
        public string ReferenceNo { get; set; }
        public string Msn { get; set; }

        public string Address { get; set; }

        public string Telco { get; set; }

        public string SimNo { get; set; }

        public string SimId { get; set; }
        public string SubDivisionCode { get; set; }
        public string SubDivisionName { get; set; }


    }
}

