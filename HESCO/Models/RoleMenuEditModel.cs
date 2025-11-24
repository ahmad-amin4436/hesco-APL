namespace HESCO.Models
{
    public class RoleMenuEditModel
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string? MenuId { get; set; }
        public string? ActionId { get; set; }
        public List<int> SelectedMenuIds { get; set; } = new List<int>();
        public List<int> SelectedActionsIds { get; set; } = new List<int>();
        public int AssignedActionIds { get; set; }
    }
}
