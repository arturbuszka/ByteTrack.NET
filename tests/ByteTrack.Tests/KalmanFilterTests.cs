using System.Text.Json;
using ByteTrack.KalmanFilter;
using Xunit;

namespace ByteTrack.Tests;

/// <summary>
/// Compares the C# Kalman filter against golden values generated from the
/// original Python (numpy/scipy) implementation. See Data/kf_golden.json.
/// </summary>
public class KalmanFilterTests
{
    private const double Tol = 1e-6;

    private static readonly double[] Measurement = { 100.0, 200.0, 0.5, 80.0 };
    private static readonly double[] Measurement2 = { 110.0, 205.0, 0.52, 82.0 };

    private static readonly JsonElement Golden = LoadGolden();

    private static JsonElement LoadGolden()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "Data", "kf_golden.json");
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        return doc.RootElement.Clone();
    }

    private static double[] Vec(string key)
    {
        var list = new List<double>();
        foreach (JsonElement e in Golden.GetProperty(key).EnumerateArray())
        {
            list.Add(e.GetDouble());
        }

        return list.ToArray();
    }

    private static double[,] Mat(string key)
    {
        var rows = Golden.GetProperty(key);
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

    private static void AssertVec(double[] expected, double[] actual)
    {
        Assert.Equal(expected.Length, actual.Length);
        for (int i = 0; i < expected.Length; i++)
        {
            Assert.True(Math.Abs(expected[i] - actual[i]) <= Tol + 1e-6 * Math.Abs(expected[i]),
                $"index {i}: expected {expected[i]}, got {actual[i]}");
        }
    }

    private static void AssertMat(double[,] expected, double[,] actual)
    {
        Assert.Equal(expected.GetLength(0), actual.GetLength(0));
        Assert.Equal(expected.GetLength(1), actual.GetLength(1));
        for (int i = 0; i < expected.GetLength(0); i++)
        {
            for (int j = 0; j < expected.GetLength(1); j++)
            {
                Assert.True(
                    Math.Abs(expected[i, j] - actual[i, j]) <= Tol + 1e-6 * Math.Abs(expected[i, j]),
                    $"[{i},{j}]: expected {expected[i, j]}, got {actual[i, j]}");
            }
        }
    }

    [Fact]
    public void Initiate_MatchesPython()
    {
        var kf = new KalmanFilter.KalmanFilter();
        var (mean, cov) = kf.Initiate(Measurement);
        AssertVec(Vec("initiate_mean"), mean);
        AssertMat(Mat("initiate_cov"), cov);
    }

    [Fact]
    public void Predict_MatchesPython()
    {
        var kf = new KalmanFilter.KalmanFilter();
        var (m0, c0) = kf.Initiate(Measurement);
        var (m1, c1) = kf.Predict(m0, c0);
        AssertVec(Vec("predict_mean"), m1);
        AssertMat(Mat("predict_cov"), c1);
    }

    [Fact]
    public void Update_MatchesPython()
    {
        var kf = new KalmanFilter.KalmanFilter();
        var (m0, c0) = kf.Initiate(Measurement);
        var (m1, c1) = kf.Predict(m0, c0);
        var (m2, c2) = kf.Update(m1, c1, Measurement2);
        AssertVec(Vec("update_mean"), m2);
        AssertMat(Mat("update_cov"), c2);
    }

    [Fact]
    public void GatingDistance_MatchesPython()
    {
        var kf = new KalmanFilter.KalmanFilter();
        var (m0, c0) = kf.Initiate(Measurement);
        var (m1, c1) = kf.Predict(m0, c0);
        double[][] measurements =
        {
            new[] { 110.0, 205.0, 0.52, 82.0 },
            new[] { 300.0, 50.0, 1.0, 40.0 },
        };
        double[] gd = kf.GatingDistance(m1, c1, measurements);
        AssertVec(Vec("gating"), gd);
    }
}
