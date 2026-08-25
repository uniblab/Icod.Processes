namespace Icod.Processes;

/// <summary>
/// Executes child processes through an injectable argument-safe contract.
/// </summary>
public interface IProcessExecutor {
	/// <summary>Runs a child process asynchronously.</summary>
	Task<ProcessResult> RunAsync(
		ProcessRunOptions options,
		CancellationToken cancellationToken = default
	);
}
