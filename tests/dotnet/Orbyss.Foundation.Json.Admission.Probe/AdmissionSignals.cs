namespace Orbyss.Foundation.Json.Admission.Probe;

/// <summary>Coordinates observable server parsing/cancellation without elapsed-time assumptions.</summary>
public sealed class AdmissionSignals
{
    /// <summary>Reports the real request reader starting.</summary>
    public TaskCompletionSource Reading { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    /// <summary>Reports cancellation observed by the request-owning endpoint.</summary>
    public TaskCompletionSource Canceled { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    /// <summary>Reports the request reader returning or throwing after cancellation.</summary>
    public TaskCompletionSource ReaderFinished { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    /// <summary>Detects accidental admission of the incomplete canceled request.</summary>
    public bool ReturnedSuccess { get; set; }
}
