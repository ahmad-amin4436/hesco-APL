namespace HESCO.Models
{
    public class MenuItem
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string Link { get; set; }
        public string Controller { get; set; }
        public string AllowAccess { get; set; }
        public int? ParentId { get; set; }
        public int DisplayOrder { get; set; }
        public string IsActive { get; set; }
        public string IsSubActive { get; set; }
        public string FaIcon { get; set; }
        public string ActionId { get; set; }
        public bool IsActionAllowed { get; set; }
    }
}
