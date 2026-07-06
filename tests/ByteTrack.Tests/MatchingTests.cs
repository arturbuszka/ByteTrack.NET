using Xunit;

namespace ByteTrack.Tests;

public class MatchingTests
{
    [Fact]
    public void Ious_IdenticalBoxes_IsOne()
    {
        var a = new List<double[]> { new[] { 0.0, 0.0, 10.0, 10.0 } };
        var b = new List<double[]> { new[] { 0.0, 0.0, 10.0, 10.0 } };

        double[,] ious = Matching.Matching.Ious(a, b);

        Assert.Equal(1.0, ious[0, 0], 10);
    }

    [Fact]
    public void Ious_DisjointBoxes_IsZero()
    {
        var a = new List<double[]> { new[] { 0.0, 0.0, 10.0, 10.0 } };
        var b = new List<double[]> { new[] { 100.0, 100.0, 110.0, 110.0 } };

        double[,] ious = Matching.Matching.Ious(a, b);

        Assert.Equal(0.0, ious[0, 0], 10);
    }

    [Fact]
    public void Ious_HalfOverlap_MatchesCythonBboxFormula()
    {
        // cython_bbox adds +1 to widths/heights. Boxes [0,0,9,9] and [5,0,14,9]:
        // each area = 10*10 = 100; intersection width = min(9,14)-max(0,5)+1 = 5,
        // height = 10 → inter = 50; union = 100+100-50 = 150; IoU = 1/3.
        var a = new List<double[]> { new[] { 0.0, 0.0, 9.0, 9.0 } };
        var b = new List<double[]> { new[] { 5.0, 0.0, 14.0, 9.0 } };

        double[,] ious = Matching.Matching.Ious(a, b);

        Assert.Equal(1.0 / 3.0, ious[0, 0], 10);
    }

    [Fact]
    public void FuseScore_CombinesIouAndDetectionScore()
    {
        // cost = 1 - IoU. With IoU=0.8 (cost 0.2) and det score 0.5:
        // fuse_sim = 0.8 * 0.5 = 0.4 → fuse_cost = 0.6.
        var cost = new double[,] { { 0.2 } };
        var detections = new List<STrack> { new(new[] { 0.0, 0.0, 10.0, 10.0 }, 0.5f) };

        double[,] fused = Matching.Matching.FuseScore(cost, detections);

        Assert.Equal(0.6, fused[0, 0], 10);
    }

    [Fact]
    public void LinearAssignment_EmptyMatrix_AllUnmatched()
    {
        var cost = new double[0, 0];

        Matching.Matching.AssignmentResult r =
            Matching.Matching.LinearAssignment(cost, 0.8);

        Assert.Empty(r.Matches);
        Assert.Empty(r.UnmatchedTracks);
        Assert.Empty(r.UnmatchedDetections);
    }

    [Fact]
    public void LinearAssignment_MatchesLowestCostPairs()
    {
        var cost = new double[,]
        {
            { 0.1, 0.9 },
            { 0.9, 0.1 },
        };

        Matching.Matching.AssignmentResult r =
            Matching.Matching.LinearAssignment(cost, 0.8);

        Assert.Contains((0, 0), r.Matches);
        Assert.Contains((1, 1), r.Matches);
        Assert.Empty(r.UnmatchedTracks);
        Assert.Empty(r.UnmatchedDetections);
    }
}
