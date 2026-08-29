using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

namespace Benchmarks;

[SimpleJob(RuntimeMoniker.Net10_0, warmupCount: 3, iterationCount: 5)]
public class PagingClampBenchmarks
{
    private const int DefaultPage = 1;
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;

    private readonly int? _page = null;
    private readonly int? _pageSize = 101;

    [Benchmark(Baseline = true)]
    public PagingValues MathMinMax()
    {
        var effectivePage = Math.Max(_page ?? DefaultPage, DefaultPage);
        var effectivePageSize = Math.Min(_pageSize ?? DefaultPageSize, MaxPageSize);
        return new PagingValues(effectivePage, effectivePageSize);
    }

    [Benchmark]
    public PagingValues ConditionalOperators()
    {
        var page = _page ?? DefaultPage;
        var pageSize = _pageSize ?? DefaultPageSize;
        return new PagingValues(
            page < DefaultPage ? DefaultPage : page,
            pageSize > MaxPageSize ? MaxPageSize : pageSize);
    }

    [Benchmark]
    public PagingValues IfStatements()
    {
        var page = _page ?? DefaultPage;
        if (page < DefaultPage)
        {
            page = DefaultPage;
        }

        var pageSize = _pageSize ?? DefaultPageSize;
        if (pageSize > MaxPageSize)
        {
            pageSize = MaxPageSize;
        }

        return new PagingValues(page, pageSize);
    }

    public readonly record struct PagingValues(int Page, int PageSize);
}
