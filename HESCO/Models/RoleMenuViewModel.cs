namespace HESCO.Models
{
    public class RoleMenuViewModel
    {
        public int Id { get; set; }
        public int RBACId { get; set; }
        public int Status { get; set; }
        public string Name { get; set; }
        public List<string> MenuTitle { get; set; }
        public string ActionTitle { get; set; }
    }
}
