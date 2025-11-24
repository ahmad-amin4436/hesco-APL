using System.ComponentModel.DataAnnotations;

namespace HESCO.Models
{
    public class User
    {
        [Key]
        public int UserId { get; set; }      
        [Required]
        public string Username { get; set; }

        [Required]
        [MinLength(6)]
        public string Password { get; set; }
        public bool RememberMe { get; set; }
        public int UserRole { get; set; }
    }
}
