using System.Diagnostics;
using UnityEngine;

// One shared budget for all generation stages in a frame.
public sealed class GenerationBudget
{
    private readonly double milliseconds;
    private readonly Stopwatch timer = new Stopwatch();
    private int frame = -1;

    public GenerationBudget(float milliseconds) => this.milliseconds = Mathf.Max(0.1f, milliseconds);

    public bool ShouldYield()
    {
        if (frame != Time.frameCount)
        {
            frame = Time.frameCount;
            timer.Restart();
        }
        return timer.Elapsed.TotalMilliseconds >= milliseconds;
    }
}
