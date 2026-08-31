namespace PPECB.Application.Common;

/// <summary>
/// Paging limits applied server-side. The brief requires 10 products per page; the
/// maximum exists so a caller cannot request an unbounded page and exhaust the server.
/// </summary>
public static class PagingDefaults
{
    public const int DefaultPageSize = 10;
    public const int MaxPageSize = 100;

    public static int NormalisePageNumber(int pageNumber) => pageNumber < 1 ? 1 : pageNumber;

    public static int NormalisePageSize(int pageSize) => pageSize switch
    {
        < 1 => DefaultPageSize,
        > MaxPageSize => MaxPageSize,
        _ => pageSize
    };
}
