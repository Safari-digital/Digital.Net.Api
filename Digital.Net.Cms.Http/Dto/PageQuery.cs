using Digital.Net.Core.Http.Services.Pagination;

namespace Digital.Net.Cms.Http.Dto;

public class PageQuery : Query
{
    public string? Path { get; set; }
    public bool? Published { get; set; }
    public bool? Indexed { get; set; }
}
