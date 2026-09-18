namespace BizfreeApp.Models.DTOs
{
    public class PagedResult<T>
    {
        public IEnumerable<T> Items { get; set; } = Enumerable.Empty<T>();
        public int TotalCount { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
        public int TotalTasks { get; set; } // Added TotalTasks property
        public string? SortBy { get; set; } // Added for sorting
        public string? SortOrder { get; set; } // Added for sorting
        public string? SearchKeyword { get; set; } // Added for filtering
    }
}
