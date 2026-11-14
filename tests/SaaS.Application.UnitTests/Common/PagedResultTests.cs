using SaaS.Application.Common.Models;
using Shouldly;

namespace SaaS.Application.UnitTests.Common;

public sealed class PagedResultTests
{
    [Fact]
    public void PagedResult_ShouldComputePageMetadataCorrectly()
    {
        var result = new PagedResult<string>(["a", "b"], page: 2, pageSize: 2, totalCount: 5);

        result.TotalPages.ShouldBe(3);
        result.HasPreviousPage.ShouldBeTrue();
        result.HasNextPage.ShouldBeTrue();
    }

    [Fact]
    public void PagedResult_WhenEmpty_ShouldReportZeroTotalPages()
    {
        var result = new PagedResult<string>([], page: 1, pageSize: 20, totalCount: 0);

        result.TotalPages.ShouldBe(0);
        result.HasPreviousPage.ShouldBeFalse();
        result.HasNextPage.ShouldBeFalse();
    }

    [Fact]
    public void PaginationQuery_ShouldClampPageSizeToMaximum()
    {
        var query = new PaginationQuery { Page = 0, PageSize = 500, SortDirection = "asc" };

        query.NormalizedPage.ShouldBe(1);
        query.NormalizedPageSize.ShouldBe(PaginationQuery.MaxPageSize);
        query.IsAscending.ShouldBeTrue();
    }
}
