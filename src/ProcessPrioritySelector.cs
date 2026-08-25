namespace Icod.Processes;

using System.Runtime.InteropServices;

/// <summary>
/// Reads and changes priorities using POSIX process, process-group, and user selector semantics.
/// </summary>
public interface IProcessPrioritySelectorProvider {
	/// <summary>Gets the process-control capabilities exposed by this provider.</summary>
	ProcessControlCapabilities Capabilities { get; }

	/// <summary>Reads the portable nice value for a selector target.</summary>
	ProcessOperationResult<ProcessPriorityValue> GetPriority(
		ProcessPriorityTarget target
	);

	/// <summary>Sets the portable nice value for a selector target.</summary>
	ProcessOperationResult SetPriority(
		ProcessPriorityTarget target,
		int niceValue
	);

	/// <summary>Adjusts the portable nice value for a selector target.</summary>
	ProcessOperationResult<ProcessPriorityValue> AdjustPriority(
		ProcessPriorityTarget target,
		int increment
	) {
		ArgumentNullException.ThrowIfNull( target );
		var current = this.GetPriority( target );
		if ( !current.Succeeded ) {
			return current;
		}
		var requested = checked( (int)Math.Clamp(
			(long)current.Value!.NiceValue + increment,
			-20L,
			19L
		) );
		var changed = this.SetPriority( target, requested );
		return changed.Succeeded
			? this.GetPriority( target )
			: ProcessOperationResult<ProcessPriorityValue>.Failure(
				changed.Status,
				changed.Message,
				changed.NativeErrorCode
			)
		;
	}
}

/// <summary>
/// Supplies POSIX priority selectors and controlled Windows process-priority substitutions.
/// </summary>
public sealed class SystemProcessPrioritySelectorProvider : IProcessPrioritySelectorProvider {
	private const int PriorityProcess = 0;
	private const int PriorityProcessGroup = 1;
	private const int PriorityUser = 2;
	private readonly IProcessInspector _inspector;
	private readonly IProcessPriorityProvider _processPriorities;

	/// <summary>Gets the process-wide selector provider.</summary>
	public static SystemProcessPrioritySelectorProvider Instance { get; } = new(
		SystemProcessPriorityProvider.Instance,
		SystemProcessInspector.Instance
	);

	/// <inheritdoc />
	public ProcessControlCapabilities Capabilities => this._processPriorities.Capabilities
		| ( OperatingSystem.IsWindows()
			? ProcessControlCapabilities.None
			: ProcessControlCapabilities.UserPriorityTargets )
	;

	/// <summary>Initializes a selector provider over the supplied process providers.</summary>
	public SystemProcessPrioritySelectorProvider(
		IProcessPriorityProvider processPriorities,
		IProcessInspector inspector
	) {
		ArgumentNullException.ThrowIfNull( processPriorities );
		ArgumentNullException.ThrowIfNull( inspector );
		this._processPriorities = processPriorities;
		this._inspector = inspector;
	}

	/// <inheritdoc />
	public ProcessOperationResult<ProcessPriorityValue> GetPriority(
		ProcessPriorityTarget target
	) {
		ArgumentNullException.ThrowIfNull( target );
		var identityFailure = this.ValidateIdentity( target );
		if ( null != identityFailure ) {
			return ProcessOperationResult<ProcessPriorityValue>.Failure(
				identityFailure.Status,
				identityFailure.Message,
				identityFailure.NativeErrorCode
			);
		}
		if ( OperatingSystem.IsWindows() ) {
			return this.GetWindowsPriority( target );
		}
		Marshal.SetLastPInvokeError( 0 );
		var value = ProcessNative.GetPriority(
			GetPosixWhich( target ),
			target.Identifier
		);
		var error = Marshal.GetLastPInvokeError();
		if ( -1 == value && 0 != error ) {
			return ProcessOperationResult<ProcessPriorityValue>.Failure(
				ProcessNative.MapErrno( error ),
				$"Unable to read priority for {target.Kind} {target.Identifier}.",
				error
			);
		}
		return ProcessOperationResult<ProcessPriorityValue>.Success(
			new ProcessPriorityValue(
				Math.Clamp( value, -20, 19 ),
				false
			)
		);
	}

	/// <inheritdoc />
	public ProcessOperationResult SetPriority(
		ProcessPriorityTarget target,
		int niceValue
	) {
		ArgumentNullException.ThrowIfNull( target );
		if ( -20 > niceValue || 19 < niceValue ) {
			return ProcessOperationResult.Failure(
				ProcessOperationStatus.InvalidArgument,
				"Nice values must be between -20 and 19."
			);
		}
		var identityFailure = this.ValidateIdentity( target );
		if ( null != identityFailure ) {
			return identityFailure;
		}
		if ( OperatingSystem.IsWindows() ) {
			return this.SetWindowsPriority( target, niceValue );
		}
		if ( 0 == ProcessNative.SetPriority(
			GetPosixWhich( target ),
			target.Identifier,
			niceValue
		) ) {
			return ProcessOperationResult.Success();
		}
		var error = Marshal.GetLastPInvokeError();
		return ProcessOperationResult.Failure(
			ProcessNative.MapErrno( error ),
			$"Unable to change priority for {target.Kind} {target.Identifier}.",
			error
		);
	}

	private ProcessOperationResult<ProcessPriorityValue> GetWindowsPriority(
		ProcessPriorityTarget target
	) {
		if ( ProcessPriorityTargetKind.Process != target.Kind ) {
			return ProcessOperationResult<ProcessPriorityValue>.Failure(
				ProcessOperationStatus.Unsupported,
				"Windows priority classes apply to individual processes only."
			);
		}
		return this._processPriorities.GetPriority(
			ProcessTarget.ForProcess(
				0 == target.Identifier
					? Environment.ProcessId
					: target.Identifier
			)
		);
	}

	private ProcessOperationResult SetWindowsPriority(
		ProcessPriorityTarget target,
		int niceValue
	) {
		if ( ProcessPriorityTargetKind.Process != target.Kind ) {
			return ProcessOperationResult.Failure(
				ProcessOperationStatus.Unsupported,
				"Windows priority classes apply to individual processes only."
			);
		}
		return this._processPriorities.SetPriority(
			ProcessTarget.ForProcess(
				0 == target.Identifier
					? Environment.ProcessId
					: target.Identifier
			),
			niceValue
		);
	}

	private ProcessOperationResult? ValidateIdentity(
		ProcessPriorityTarget target
	) {
		if ( ProcessPriorityTargetKind.Process != target.Kind || null == target.Identity?.ReuseToken ) {
			return null;
		}
		var observed = this._inspector.ObserveIdentity( target.Identifier );
		if ( !observed.Succeeded ) {
			return ProcessOperationResult.Failure(
				observed.Status,
				observed.Message,
				observed.NativeErrorCode
			);
		}
		return SystemProcessInspector.MatchesExpectedIdentity( target.Identity, observed.Value! )
			? null
			: ProcessOperationResult.Failure(
				ProcessOperationStatus.Reused,
				$"Process identifier {target.Identifier} has been reused."
			)
		;
	}

	private static int GetPosixWhich(
		ProcessPriorityTarget target
	) => target.Kind switch {
		ProcessPriorityTargetKind.Process => PriorityProcess,
		ProcessPriorityTargetKind.ProcessGroup => PriorityProcessGroup,
		ProcessPriorityTargetKind.User => PriorityUser,
		_ => throw new ArgumentOutOfRangeException( nameof( target ) )
	};
}
