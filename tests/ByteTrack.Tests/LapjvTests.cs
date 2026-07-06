using System.Text.Json;
using ByteTrack.Matching;
using Xunit;

namespace ByteTrack.Tests;

/// <summary>
/// Verifies the LAPJV solver against an independent optimal-assignment oracle
/// (scipy.optimize.linear_sum_assignment) plus rectangular / cost-limit cases.
/// </summary>
public class LapjvTests
{
    [Fact]
    public void Solve_SquareMatrices_MatchOracle()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "Data", "lap_golden.json");
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        foreach (JsonElement c in doc.RootElement.GetProperty("cases").EnumerateArray())
        {
            double[,] cost = ParseMatrix(c.GetProperty("cost"));
            int[] expected = ParseIntArray(c.GetProperty("row_to_col"));

            var (rowToCol, _) = Lapjv.Solve(cost, double.PositiveInfinity);

            Assert.Equal(expected, rowToCol);
        }
    }

    [Fact]
    public void Solve_Rectangular_MoreRowsThanCols_LeavesExtraRowsUnmatched()
    {
        // 3 rows, 2 cols → exactly one row unmatched (-1).
        var cost = new double[,]
        {
            { 0.1, 0.9 },
            { 0.8, 0.2 },
            { 0.5, 0.5 },
        };

        var (rowToCol, colToRow) = Lapjv.Solve(cost, double.PositiveInfinity);

        Assert.Equal(3, rowToCol.Length);
        Assert.Equal(2, colToRow.Length);
        Assert.Single(rowToCol, v => v < 0);              // one unmatched row
        Assert.DoesNotContain(-1, colToRow);              // both cols matched
        // Cheapest assignment: row0→col0, row1→col1, row2 unmatched.
        Assert.Equal(0, rowToCol[0]);
        Assert.Equal(1, rowToCol[1]);
        Assert.Equal(-1, rowToCol[2]);
    }

    [Fact]
    public void Solve_CostLimit_RejectsPairsAboveThreshold()
    {
        // Only the (0,0)/(1,1) diagonal is under the limit; off-diagonal is 0.95.
        var cost = new double[,]
        {
            { 0.1, 0.95 },
            { 0.95, 0.2 },
        };

        var (rowToCol, _) = Lapjv.Solve(cost, 0.5);

        Assert.Equal(0, rowToCol[0]);
        Assert.Equal(1, rowToCol[1]);
    }

    [Fact]
    public void Solve_CostLimit_AllAboveThreshold_LeavesEverythingUnmatched()
    {
        var cost = new double[,]
        {
            { 0.9, 0.8 },
            { 0.7, 0.95 },
        };

        var (rowToCol, colToRow) = Lapjv.Solve(cost, 0.5);

        Assert.All(rowToCol, v => Assert.Equal(-1, v));
        Assert.All(colToRow, v => Assert.Equal(-1, v));
    }

    private static double[,] ParseMatrix(JsonElement rows)
    {
        int n = rows.GetArrayLength();
        int m = rows[0].GetArrayLength();
        var result = new double[n, m];
        int i = 0;
        foreach (JsonElement row in rows.EnumerateArray())
        {
            int j = 0;
            foreach (JsonElement v in row.EnumerateArray())
            {
                result[i, j++] = v.GetDouble();
            }

            i++;
        }

        return result;
    }

    private static int[] ParseIntArray(JsonElement arr)
    {
        var list = new List<int>();
        foreach (JsonElement v in arr.EnumerateArray())
        {
            list.Add(v.GetInt32());
        }

        return list.ToArray();
    }
}
