namespace Icod.Processes;

using System.Text;

/// <summary>
/// Identifies how a canceled or timed-out process execution handles a live child.
/// </summary>
public enum ProcessCancellationPolicy {
	/// <summary>Terminate the child and, where supported, its descendants.</summary>
	KillProcessTree,
	/// <summary>Terminate only the immediate child.</summary>
	KillProcess,
	/// <summary>Detach redirected streams and leave the child running.</summary>
	LeaveRunning
}

/// <summary>
/// Configures asynchronous child-process execution.
/// </summary>
public sealed class ProcessRunOptions {
	private ProcessCancellationPolicy _cancellationPolicy = ProcessCancellationPolicy.KillProcessTree;

	/// <summary>Gets the exact child-process arguments after argument zero.</summary>
	public IList<string> Arguments {
		get;
	} = new List<string>();

	/// <summary>
	/// Gets or sets an explicit native argument zero. A null value uses the executable
	/// name selected by the host launcher.
	/// </summary>
	public string? ArgumentZero {
		get;
		set;
	}

	/// <summary>Gets or sets whether the child starts as the leader of a new process group.</summary>
	/// <remarks>
	/// POSIX hosts create the group atomically with child launch. Windows uses the .NET
	/// process-group creation facility.
	/// </remarks>
	public bool CreateProcessGroup {
		get;
		set;
	}

	/// <summary>Gets or sets whether standard error is captured in the result.</summary>
	public bool CaptureStandardError {
		get;
		set;
	}

	/// <summary>Gets or sets whether standard output is captured in the result.</summary>
	public bool CaptureStandardOutput {
		get;
		set;
	}

	/// <summary>Gets or sets how a live child is handled after cancellation or timeout.</summary>
	public ProcessCancellationPolicy CancellationPolicy {
		get => this._cancellationPolicy;
		set {
			if ( !Enum.IsDefined(
				typeof( ProcessCancellationPolicy ),
				value
			) ) {
				throw new ArgumentOutOfRangeException(
					nameof( value )
				);
			}
			this._cancellationPolicy = value;
		}
	}

	/// <summary>Gets or sets whether the inherited environment is cleared.</summary>
	public bool ClearEnvironment {
		get;
		set;
	}

	/// <summary>
	/// Gets or sets an exact environment snapshot. When set, it takes precedence over inherited-environment selection.
	/// </summary>
	public ProcessEnvironment? Environment {
		get;
		set;
	}

	/// <summary>Gets environment changes. A null value removes a variable.</summary>
	public IDictionary<string, string?> EnvironmentVariables {
		get;
	} = new Dictionary<string, string?>(
		ProcessEnvironmentBuilder.VariableNameComparer
	);

	/// <summary>Gets the executable file name.</summary>
	public string FileName {
		get;
	}

	/// <summary>
	/// Gets or sets whether cancellation terminates the process tree.
	/// </summary>
	/// <remarks>
	/// This compatibility property maps to <see cref="CancellationPolicy"/>. Setting false
	/// selects immediate-child termination.
	/// </remarks>
	public bool KillEntireProcessTreeOnCancellation {
		get => ProcessCancellationPolicy.KillProcessTree == this.CancellationPolicy;
		set => this.CancellationPolicy = value
			? ProcessCancellationPolicy.KillProcessTree
			: ProcessCancellationPolicy.KillProcess
		;
	}

	/// <summary>Gets or sets the encoding used to decode captured output.</summary>
	public Encoding OutputEncoding {
		get;
		set;
	} = Encoding.UTF8;

	/// <summary>
	/// Gets or sets a callback invoked after the child identity is observed and before waiting begins.
	/// </summary>
	public Action<ProcessIdentity>? ProcessStarted {
		get;
		set;
	}

	/// <summary>Gets or sets launch-time signal disposition and mask changes for the child process.</summary>
	public ProcessLaunchSignalPolicy? SignalPolicy {
		get;
		set;
	}

	/// <summary>Gets or sets whether the executable is resolved before launch.</summary>
	public bool ResolveExecutable {
		get;
		set;
	}

	/// <summary>
	/// Gets or sets whether POSIX execution replaces the current process image instead of creating a child.
	/// </summary>
	/// <remarks>
	/// On successful replacement, <see cref="IProcessExecutor.RunAsync(ProcessRunOptions, CancellationToken)"/>
	/// does not return. This capability is unavailable on Windows and cannot be combined with managed
	/// standard-stream redirection or capture, a new process group, an execution timeout, or a
	/// <see cref="ProcessStarted"/> callback. Ordered <see cref="PosixFileDescriptorDuplications"/> are
	/// applied to the current process immediately before replacement and restored if exec fails.
	/// Executable text that returns <c>ENOEXEC</c> is retried through <c>/bin/sh</c>, matching the
	/// traditional <c>execvp</c> contract.
	/// </remarks>
	public bool ReplaceCurrentProcess {
		get;
		set;
	}

	/// <summary>Gets or sets whether launch failures are returned instead of thrown.</summary>
	public bool ReturnLaunchFailureResult {
		get;
		set;
	}

	/// <summary>Gets or sets a destination for standard error.</summary>
	public Stream? StandardError {
		get;
		set;
	}

	/// <summary>Gets or sets a source for standard input.</summary>
	public Stream? StandardInput {
		get;
		set;
	}

	/// <summary>Gets or sets a destination for standard output.</summary>
	public Stream? StandardOutput {
		get;
		set;
	}

	/// <summary>
	/// Gets ordered POSIX file-descriptor duplications applied at native launch or current-process replacement.
	/// </summary>
	/// <remarks>
	/// Adding an item selects the native POSIX launcher. With <see cref="ReplaceCurrentProcess"/>, actions
	/// are applied in order to the current process immediately before exec and restored if exec fails.
	/// This capability is unsupported on Windows and cannot be combined with managed standard-stream
	/// redirection or output capture.
	/// </remarks>
	public IList<PosixFileDescriptorDuplication> PosixFileDescriptorDuplications {
		get;
	} = new List<PosixFileDescriptorDuplication>();

	/// <summary>
	/// Gets or sets whether a POSIX child inherits standard input as a write-only null
	/// device so reads fail rather than return end-of-file.
	/// </summary>
	public bool UseUnreadableStandardInput {
		get;
		set;
	}

	/// <summary>Gets or sets an optional monotonic execution timeout.</summary>
	public TimeSpan? Timeout {
		get;
		set;
	}

	/// <summary>Gets or sets the working directory.</summary>
	public string? WorkingDirectory {
		get;
		set;
	}

	/// <summary>Initializes process options.</summary>
	public ProcessRunOptions(
		string fileName
	) {
		ArgumentNullException.ThrowIfNull( fileName );
		if ( fileName.Contains( '\0' ) ) {
			throw new ArgumentException(
				"Executable names cannot contain a NUL character.",
				nameof( fileName )
			);
		}
		this.FileName = fileName;
	}
}
