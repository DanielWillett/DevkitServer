namespace DevkitServer.Multiplayer.Networking;

/// <summary>
/// Arbitrarily tracks the progress of an operation.
/// </summary>
public interface IProgressTracker
{
    /// <summary>
    /// Max value. Represents <see cref="ProgressTicks"/> when the operation is complete.
    /// </summary>
    int TotalTicks { get; set; }

    /// <summary>
    /// Current value. Should be equal to zero at the beginning and <see cref="TotalTicks"/> when the operation is complete.
    /// </summary>
    int ProgressTicks { get; set; }

    /// <summary>
    /// Value from zero to one of how far along the progress is.
    /// </summary>
    public double Progress
    {
        get => ProgressTicks / (double)TotalTicks;
        set => ProgressTicks = (int)Math.Round(value * TotalTicks);
    }

    /// <summary>
    /// Get <see cref="Progress"/> based on a different value for <see cref="ProgressTicks"/>.
    /// </summary>
    /// <returns>A value from zero to one of how far along the progress is.</returns>
    public float GetProgress(int ticks) => ticks / (float)TotalTicks;
}


/// <summary>
/// Arbitrarily tracks the progress of an operation.
/// </summary>
public class ProgressTracker(int totalTicks) : IProgressTracker
{
    private int _progressTicks;
    private int _lastUpdateTicks;

    /// <summary>
    /// Called every time the progress is updated outside of the range <see cref="ProgressUpdateFrequency"/>, or when the <see cref="TotalTicks"/> gets updated.
    /// </summary>
    public event ProgressTrackerUpdated? OnProgressUpdated;

    /// <inheritdoc />
    public int TotalTicks
    {
        get => totalTicks;
        set
        {
            if (value == totalTicks)
                return;
            int oldTicks = (int)Math.Round(ProgressTicks / (double)TotalTicks * value);
            totalTicks = value;
            OnProgressUpdated?.Invoke(this, oldTicks);
        }
    }

    /// <inheritdoc />
    public int ProgressTicks
    {
        get => _progressTicks;
        set
        {
            if (value == _progressTicks)
                return;
            int oldTicks = _progressTicks;
            _progressTicks = value;

            if (ProgressUpdateFrequency < 2)
            {
                OnProgressUpdated?.Invoke(this, oldTicks);
                return;
            }

            oldTicks = _lastUpdateTicks;
            if (Math.Abs(value - oldTicks) >= ProgressUpdateFrequency)
            {
                _lastUpdateTicks = value;
                OnProgressUpdated?.Invoke(this, oldTicks);
            }
        }
    }

    /// <summary>
    /// Amount of ticks the progress must go up for <see cref="OnProgressUpdated"/> to be called.
    /// </summary>
    /// <remarks>Default value: 1 (every tick).</remarks>
    public int ProgressUpdateFrequency { get; set; } = 1;

    public ProgressTracker() : this(0) { }
}

public delegate void ProgressTrackerUpdated(ProgressTracker instance, int oldProgressTicks);