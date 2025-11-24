namespace HESCO.Models
{
    public class ActionInfo
    {
        public int Id { get; set; }

        // Change from int Parent_id to int? Parent_id
        public int? Parent_id { get; set; }

        public string Controller { get; set; }
        public string Action { get; set; }
        public string? Name { get; set; }

        public List<ActionInfo> SubActions { get; set; } = new List<ActionInfo>();
    }
}
