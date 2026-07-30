// File summary: Pins that shared grid filters compile into query parameters rather than SQL literals.
// Major updates:
// - 2026-07-31 pending Added after the review's SUSPECTED plan-reuse finding was confirmed by ToQueryString.

using Microsoft.EntityFrameworkCore;
using RVT.DataAccess.Context;
using RVT.Entities.Querying;
using RvtPortal.Spa.Tests.Support;
using MonitorEntity = RVT.Entities.Monitor;

namespace RvtPortal.Spa.Tests;

/// <summary>
/// <c>FilterExpression</c> built every filter value with <c>Expression.Constant</c>, which EF inlines as a SQL
/// literal: <c>WHERE m.serial_id = 'SER-123'</c>. Each distinct serial or time bound then produced distinct SQL
/// text, so PostgreSQL planned every variant from scratch and nothing was ever reused from the plan cache -
/// on the measurement grids that is the hot read path. Values are captured now and compile to parameters.
/// <para>
/// <c>ToQueryString()</c> prints each parameter's value in comment lines before the statement, so the
/// assertions look at the statement itself.
/// </para>
/// </summary>
public sealed class FilterExpressionParameterizationTests
{
    private const string ProbeSerial = "SER-PARAMETERIZED-PROBE";

    [Fact]
    // Function summary: Verifies an equality filter compiles to a parameter rather than an inlined literal.
    public void EqualityFilter_CompilesToAParameter()
    {
        string statement = StatementFor(new SingleFilter
        {
            Operation = Op.Equals,
            PropertyName = "SerialId",
            Value = ProbeSerial
        });

        Assert.DoesNotContain(ProbeSerial, statement, StringComparison.Ordinal);
        Assert.Contains("m.serial_id = @", statement, StringComparison.Ordinal);
    }

    [Fact]
    // Function summary: Verifies a text-search filter compiles to a parameter rather than an inlined literal.
    public void ContainsFilter_CompilesToAParameter()
    {
        string statement = StatementFor(new SingleFilter
        {
            Operation = Op.Contains,
            PropertyName = "SerialId",
            Value = ProbeSerial
        });

        Assert.DoesNotContain(ProbeSerial, statement, StringComparison.Ordinal);
        Assert.Contains("@", statement, StringComparison.Ordinal);
    }

    [Fact]
    /// <summary>
    /// Null comparisons stay literal on purpose: <c>= $1</c> with a null parameter never matches a row, while
    /// <c>IS NULL</c> is what the operation means.
    /// </summary>
    public void IsNullFilter_StaysALiteralNullComparison()
    {
        string statement = StatementFor(new SingleFilter
        {
            Operation = Op.IsNull,
            PropertyName = "FleetNr"
        });

        Assert.Contains("IS NULL", statement, StringComparison.OrdinalIgnoreCase);
    }

    // Function summary: Compiles one filter against the PostgreSQL model and returns the statement without EF's parameter comments.
    private static string StatementFor(Filter filter)
    {
        using RVTDbContext context = new(TestDbContexts.ModelOnlyNpgsql<RVTDbContext>());
        string sql = context.MonitorsList
            .AsNoTracking()
            .Where(FilterExpression.ExpressionBuilder.GetExpression<MonitorEntity>([filter]))
            .ToQueryString();

        return string.Join(
            Environment.NewLine,
            sql.Split(["\r\n", "\n"], StringSplitOptions.None)
                .SkipWhile(line => line.StartsWith("--", StringComparison.Ordinal)));
    }
}
