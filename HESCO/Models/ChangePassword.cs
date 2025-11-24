using System.ComponentModel.DataAnnotations;

namespace HESCO.Models
{
    public class ChangePassword
    {
        [Key]
        public int UserId { get; set; }

        //[Required]
        //[MinLength(4)]
        //public string Username { get; set; }

        [Required]
        [MinLength(6)]
        public string NewPassword { get; set; }

        [Required]       
        public string ConfirmPassword { get; set; }
    }
}
