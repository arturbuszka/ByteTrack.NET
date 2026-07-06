# ByteTrack.NET

A faithful C# (.NET 8) port of the tracking core of
[FoundationVision/ByteTrack](https://github.com/FoundationVision/ByteTrack)
(`yolox/tracker/`).

The **YOLOX detector is out of scope** — you feed the tracker detections that are
already in image coordinates (bounding box + confidence), and it returns stable
per-object track IDs across frames.

## What's ported

| Original (`yolox/tracker/`) | C# |
| --- | --- |
| `basetrack.py` | `TrackState`, `BaseTrack` |
| `kalman_filter.py` | `KalmanFilter.KalmanFilter` (+ `IKalmanFilter`) |
| `byte_tracker.py` | `STrack`, `ByteTracker` |
| `matching.py` (used parts) | `Matching.Matching`, `Matching.Lapjv` |

Linear algebra sits behind `ILinearAlgebra` (default: dependency-free
`ManagedLinearAlgebra`) so it can be swapped for a native/faster backend such as
MathNet.Numerics without touching the filter. The linear assignment is a from-
scratch LAPJV port with `cost_limit` support, matching `lap.lapjv`.

ReID-only helpers from the original `matching.py` (`embedding_distance`,
`fuse_motion`, `fuse_iou`, `gate_cost_matrix`, `v_iou_distance`,
`merge_matches`) are intentionally omitted — they are not on the BYTETracker code
path.

## Usage

```csharp
using ByteTrack;

var config = new ByteTrackerConfig
{
    TrackThresh = 0.5f,  // high/low detection split
    TrackBuffer = 30,    // frames a lost track is kept (at 30 fps)
    MatchThresh = 0.8f,  // IoU match threshold (first association)
    Mot20 = false,       // skip score fusion for crowded scenes
};

var tracker = new ByteTracker(config, frameRate: 30);

foreach (var frameDetections in videoStream) // your detector's output per frame
{
    // Detection: (x1, y1, x2, y2, score) in image coordinates.
    var detections = frameDetections
        .Select(d => new Detection(d.X1, d.Y1, d.X2, d.Y2, d.Score))
        .ToList();

    IReadOnlyList<STrack> tracks = tracker.Update(detections);

    foreach (STrack t in tracks)
    {
        double[] tlbr = t.Tlbr; // [x1, y1, x2, y2]
        Console.WriteLine($"id={t.TrackId} box=[{string.Join(',', tlbr)}] score={t.Score}");
    }
}
```

> Track IDs come from a process-global counter (as in the original). Call
> `BaseTrack.ResetCount()` between independent runs for deterministic IDs.

## Build & test

```bash
dotnet build ByteTrack.sln
dotnet test  ByteTrack.sln
```

The test suite (18 tests) includes **golden-value** checks: the Kalman filter is
compared against values generated from the original numpy/scipy code
(`tests/ByteTrack.Tests/Data/kf_golden.json`), and LAPJV against an independent
`scipy.optimize.linear_sum_assignment` oracle (`lap_golden.json`).
