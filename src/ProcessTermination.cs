namespace Icod.Processes;

/// <summary>
/// Identifies why a process operation ended.
/// </summary>
public enum ProcessTerminationKind {
	/// <summary>The process exited normally.</summary>
	Exited,
	/// <summary>The process was terminated by a signal.</summary>
	Signaled,
	/// <summary>The operation reached its timeout.</summary>
	TimedOut,
	/// <summary>The operation was canceled.</summary>
	Canceled,
	/// <summary>The executable could not be launched.</summary>
	LaunchFailed,
	/// <summary>The observed process vanished.</summary>
	Vanished,
	/// <summary>No more specific termination reason is available.</summary>
	Unknown
}

/// <summary>
/// Identifies the portable class of a child-process launch failure.
/// </summary>
public enum ProcessLaunchFailureKind {
	/// <summary>The failure cannot be classified more specifically.</summary>
	Unknown,
	/// <summary>The requested executable could not be found.</summary>
	NotFound,
	/// <summary>The executable was found but could not be invoked.</summary>
	CannotInvoke,
	/// <summary>Launch preparation such as signal policy or working-directory setup failed before invocation.</summary>
	SetupFailed
}

/// <summary>
/// Describes child exit, signal termination, timeout, cancellation, or launch failure in a portable form.
/// </summary>
public sealed class ProcessTermination {
	/// <summary>Gets the portable launch-failure class, when applicable.</summary>
	public ProcessLaunchFailureKind? LaunchFailureKind {
		get;
	}

	/// <summary>Gets the child exit code, when available.</summary>
	public int? ExitCode {
		get;
	}

	/// <summary>Gets a diagnostic for launch or control failures.</summary>
	public string? Message {
		get;
	}

	/// <summary>Gets the terminating signal, when known.</summary>
	public ProcessSignal? Signal {
		get;
	}

	/// <summary>Gets the termination kind.</summary>
	public ProcessTerminationKind Kind {
		get;
	}

	/// <summary>Creates a normal exit result.</summary>
	public static ProcessTermination Exited(
		int exitCode
	) => new(
		ProcessTerminationKind.Exited,
		exitCode,
		null,
		null,
		null
	);

	/// <summary>Creates a signal-termination result.</summary>
	public static ProcessTermination Signaled(
		ProcessSignal signal,
		int? observedExitCode = null
	) {
		ArgumentNullException.ThrowIfNull(
			signal
		);
		return new ProcessTermination(
			ProcessTerminationKind.Signaled,
			observedExitCode,
			signal,
			null,
			null
		);
	}

	/// <summary>Creates a timeout result.</summary>
	public static ProcessTermination TimedOut(
		int? observedExitCode = null
	) => new(
		ProcessTerminationKind.TimedOut,
		observedExitCode,
		null,
		null,
		null
	);

	/// <summary>Creates a cancellation result.</summary>
	public static ProcessTermination Canceled(
		int? observedExitCode = null
	) => new(
		ProcessTerminationKind.Canceled,
		observedExitCode,
		null,
		null,
		null
	);

	/// <summary>Creates a launch-failure result.</summary>
	public static ProcessTermination LaunchFailed(
		string message,
		ProcessLaunchFailureKind failureKind = ProcessLaunchFailureKind.Unknown
	) {
		ArgumentException.ThrowIfNullOrWhiteSpace(
			message
		);
		return new ProcessTermination(
			ProcessTerminationKind.LaunchFailed,
			null,
			null,
			message,
			failureKind
		);
	}

	/// <summary>Creates a vanished-process result.</summary>
	public static ProcessTermination Vanished() => new(
		ProcessTerminationKind.Vanished,
		null,
		null,
		null,
		null
	);

	/// <summary>Creates an unknown result.</summary>
	public static ProcessTermination Unknown(
		int? observedExitCode = null,
		string? message = null
	) => new(
		ProcessTerminationKind.Unknown,
		observedExitCode,
		null,
		message,
		null
	);

	/// <summary>
	/// Translates the termination to the conventional command exit status used by GNU-facing commands.
	/// </summary>
	public int ToPortableExitCode(
		int timeoutExitCode = 124
	) => this.Kind switch {
		ProcessTerminationKind.Exited => this.ExitCode ?? 1,
		ProcessTerminationKind.Signaled => null == this.Signal
			? this.ExitCode ?? 1
			: 128 + this.Signal.Number,
		ProcessTerminationKind.TimedOut => timeoutExitCode,
		ProcessTerminationKind.Canceled => 125,
		ProcessTerminationKind.LaunchFailed => this.LaunchFailureKind switch {
			ProcessLaunchFailureKind.NotFound => 127,
			ProcessLaunchFailureKind.SetupFailed => 125,
			_ => 126
		},
		ProcessTerminationKind.Vanished => 1,
		_ => this.ExitCode ?? 1
	};

	private ProcessTermination(
		ProcessTerminationKind kind,
		int? exitCode,
		ProcessSignal? signal,
		string? message,
		ProcessLaunchFailureKind? launchFailureKind
	) {
		this.Kind = kind;
		this.ExitCode = exitCode;
		this.Signal = signal;
		this.Message = message;
		this.LaunchFailureKind = launchFailureKind;
	}
}
