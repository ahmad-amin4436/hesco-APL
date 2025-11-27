using System.ComponentModel.DataAnnotations;

namespace HESCO.Models
{
    public class UserData
    {
        [Key]
        public int UserId { get; set; }

        [Required]
        [MinLength(4)]
        public string Username { get; set; }

        //[Required]
        [MinLength(6)]
        public string? Password { get; set; }   
     

        [Required]       
        public string Name { get; set; }

        [RegularExpression(@"^[^@\s]+@[^@\s]+\.(com)$", ErrorMessage = "Please enter correct email address")]
        public string Email { get; set; }

        [Required]
        [Phone]
        [MinLength(11)]
        [MaxLength(11)]
        public string ContactNumber { get; set; }

        [Required]
        public string UserRole { get; set; }
     
        public string? Team { get; set; }    
        public int? CreatedAt { get; set; }
        public int? UpdatedAt { get; set; }
        public List<RbacSelection> SelectedRights { get; set; } = new List<RbacSelection>();

        public List<int> SelectedActions { get; set; } = new List<int>();

    }
    public class RbacSelection
    {
        public int Id { get; set; }
        public string Label { get; set; }
    }
}
