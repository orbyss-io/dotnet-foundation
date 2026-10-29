internal sealed class DynamicFixtureState
{
    public bool Published = true;
    public int Version = 1;
    public int Reads;
    public int Verifications;
    public bool BlockVerification;
    public bool RejectVerification;
    public bool ThrowVerification;
    public bool ChangeVersionOnVerify;
    public readonly TaskCompletionSource VerificationEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public readonly TaskCompletionSource ContinueVerification = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public readonly TaskCompletionSource CancellationEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public readonly TaskCompletionSource CancellationObserved = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public readonly HashSet<Guid> ResolverInstances = [];
}
