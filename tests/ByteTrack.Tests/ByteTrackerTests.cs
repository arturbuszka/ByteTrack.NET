using Xunit;

namespace ByteTrack.Tests;

[Collection("Sequential")]
public class ByteTrackerTests
{
    private static Detection Box(double x1, double y1, double x2, double y2, float score) =>
        new(x1, y1, x2, y2, score);

    [Fact]
    public void SingleObject_KeepsSameIdAcrossFrames()
    {
        BaseTrack.ResetCount();
        var tracker = new ByteTracker(new ByteTrackerConfig());

        // Frame 1: a confident detection. First frame activates immediately.
        var f1 = tracker.Update(new[] { Box(100, 100, 150, 200, 0.9f) });
        Assert.Single(f1);
        int id = f1[0].TrackId;

        // Frames 2-4: the same object drifts slightly; id must persist.
        for (int i = 0; i < 3; i++)
        {
            var frame = tracker.Update(new[] { Box(100 + i, 100 + i, 150 + i, 200 + i, 0.9f) });
            Assert.Single(frame);
            Assert.Equal(id, frame[0].TrackId);
        }
    }

    [Fact]
    public void NewObject_GetsNewId()
    {
        BaseTrack.ResetCount();
        var tracker = new ByteTracker(new ByteTrackerConfig());

        tracker.Update(new[] { Box(100, 100, 150, 200, 0.9f) });
        // A distant new object appears from frame 2. Per ByteTrack semantics, a
        // track first seen after frame 1 stays "unconfirmed" until it is matched
        // again, so it only surfaces in the output on the following frame.
        var objA = Box(100, 100, 150, 200, 0.9f);
        var objB = Box(400, 400, 450, 500, 0.9f);
        tracker.Update(new[] { objA, objB });
        var f3 = tracker.Update(new[] { objA, objB });

        var ids = f3.Select(t => t.TrackId).Distinct().ToList();
        Assert.Equal(2, ids.Count);
    }

    [Fact]
    public void LostObject_IsDroppedAfterMaxTimeLost()
    {
        BaseTrack.ResetCount();
        var config = new ByteTrackerConfig { TrackBuffer = 5 };
        var tracker = new ByteTracker(config, frameRate: 30); // maxTimeLost = 5

        // Frame 1 activates immediately (frame_id == 1).
        var f1 = tracker.Update(new[] { Box(100, 100, 150, 200, 0.9f) });
        int id = f1[0].TrackId;
        int lastSeenFrame = tracker.FrameId; // == 1

        // Now provide no detections. The track goes Lost, then is Removed once
        // frameId - endFrame > maxTimeLost (5) — i.e. from the 7th frame onward.
        for (int i = 0; i < 5; i++)
        {
            var frame = tracker.Update(Array.Empty<Detection>());
            Assert.Empty(frame);                 // nothing active while lost
            Assert.Contains(id, tracker.LostStracks.Select(t => t.TrackId));
        }

        // Frame 7 marks the track Removed (frameId - endFrame = 6 > 5). Matching
        // the original ByteTrack ordering, it is still subtracted from the lost
        // list only on the *following* frame, so run two more empty frames.
        tracker.Update(Array.Empty<Detection>()); // frame 7: Removed
        Assert.True(tracker.FrameId - lastSeenFrame > 5);
        tracker.Update(Array.Empty<Detection>()); // frame 8: pruned from lost list
        Assert.DoesNotContain(id, tracker.LostStracks.Select(t => t.TrackId));
    }

    [Fact]
    public void LowScoreOnlyDetection_DoesNotStartTrack()
    {
        BaseTrack.ResetCount();
        var tracker = new ByteTracker(new ByteTrackerConfig()); // trackThresh 0.5

        // Score 0.3 is a "second" (low) detection — it can sustain existing
        // tracks but must never initiate a new one.
        var f1 = tracker.Update(new[] { Box(100, 100, 150, 200, 0.3f) });

        Assert.Empty(f1);
    }
}
