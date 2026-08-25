namespace Icod.Processes;

/// <summary>
/// Contains the result of asynchronous process execution.
/// </summary>
public sealed class ProcessResult {
	/// <summary>Gets the monotonic elapsed execution duration.</summary>
	public TimeSpan Elapsed {
		get;
	}

	/// <summary>Gets the process exit code, when available.</summary>
	public int? ExitCode {
		get;
	}

	/// <summary>Gets the launched process identity, when the child started.</summary>
	public ProcessIdentity? Identity {
		get;
	}

	/// <summary>Gets whether a child process was started.</summary>
	public bool Started {
		get;
	}

	/// <summary>Gets captured standard error.</summary>
	public string? StandardError {
		get;
	}

	/// <summary>Gets captured standard output.</summary>
	public string? StandardOutput {
		get;
	}

	/// <summary>Gets the portable termination description.</summary>
	public ProcessTermination Termination {
		get;
	}

	/// <summary>Gets whether execution reached its timeout.</summary>
	public bool TimedOut => ProcessTerminationKind.TimedOut == this.Termination.Kind;

	/// <summary>Gets whether execution was canceled by the caller.</summary>
	public bool WasCanceled => ProcessTerminationKind.Canceled == this.Termination.Kind;

	/// <summary>Creates a process result from an explicit portable termination description.</summary>
	public static ProcessResult FromTermination(
		ProcessTermination termination,
		bool started = true,
		ProcessIdentity? identity = null,
		TimeSpan elapsed = default,
		string? standardOutput = null,
		string? standardError = null
	) => new(
		started,
		identity,
		termination,
		elapsed,
		standardOutput,
		standardError
	);

	/// <summary>Initializes a process result from the legacy exit-code shape.</summary>
	internal ProcessResult(
		int? exitCode,
		bool wasCanceled,
		string? standardOutput,
		string? standardError
	) : this(
		null != exitCode,
		null,
		wasCanceled
			? ProcessTermination.Canceled( exitCode )
			: null != exitCode
				? ProcessTermination.Exited( exitCode.Value )
				: ProcessTermination.Unknown(),
		TimeSpan.Zero,
		standardOutput,
		standardError
	) {
	}

	/// <summary>Initializes a complete process result.</summary>
	internal ProcessResult(
		bool started,
		ProcessIdentity? identity,
		ProcessTermination termination,
		TimeSpan elapsed,
		string? standardOutput,
		string? standardError
	) {
		ArgumentNullException.ThrowIfNull(
			termination
		);
		this.Started = started;
		this.Identity = identity;
		this.Termination = termination;
		this.ExitCode = termination.ExitCode;
		this.Elapsed = elapsed;
		this.StandardOutput = standardOutput;
		this.StandardError = standardError;
	}
}
