namespace HESCO.Models
{
    public class ApprovedMappingListViewModel
    {
        public List<MappingApprovedListViewModel> ApprovedMappings { get; set; } = new List<MappingApprovedListViewModel>();
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalRecords { get; set; }
        public int PendingCount { get; set; }
        public int ApprovedCount { get; set; }
        public int DisapprovedCount { get; set; }

    }
}
