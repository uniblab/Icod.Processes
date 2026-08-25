namespace Icod.Processes;

using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

/// <summary>
/// Contains an observed portable nice value and whether it approximates a native priority class.
/// </summary>
public sealed class ProcessPriorityValue {
	/// <summary>Gets whether the value is an approximation.</summary>
	public bool IsApproximation {
		get;
	}

	/// <summary>Gets the portable nice value from -20 through 19.</summary>
	public int NiceValue {
		get;
	}

	/// <summary>Initializes a priority value.</summary>
	public ProcessPriorityValue(
		int niceValue,
		bool isApproximation
	) {
		if ( -20 > niceValue || 19 < niceValue ) {
			throw new ArgumentOutOfRangeException(
				nameof( niceValue )
			);
		}
		this.NiceValue = niceValue;
		this.IsApproximation = isApproximation;
	}
}

/// <summary>
/// Reads, sets, and adjusts process priorities through an injectable provider.
/// </summary>
public interface IProcessPriorityProvider {
	/// <summary>Gets capabilities available from this provider.</summary>
	ProcessControlCapabilities Capabilities {
		get;
	}

	/// <summary>Reads the portable nice value for a process or process group.</summary>
	ProcessOperationResult<ProcessPriorityValue> GetPriority(
		ProcessTarget target
	);

	/// <summary>Sets the portable nice value for a process or process group.</summary>
	ProcessOperationResult SetPriority(
		ProcessTarget target,
		int niceValue
	);

	/// <summary>Adjusts the current portable nice value by the supplied increment.</summary>
	ProcessOperationResult<ProcessPriorityValue> AdjustPriority(
		ProcessTarget target,
		int increment
	);
}

/// <summary>
/// Provides POSIX nice-value operations and documented Windows priority-class substitutions.
/// </summary>
public sealed class SystemProcessPriorityProvider : IProcessPriorityProvider {
	private const int PriorityProcess = 0;
	private const int PriorityProcessGroup = 1;
	private readonly IProcessInspector _inspector;

	/// <summary>Gets the shared system priority provider.</summary>
	public static SystemProcessPriorityProvider Instance {
		get;
	} = new(
		SystemProcessInspector.Instance
	);

	/// <inheritdoc />
	public ProcessControlCapabilities Capabilities => ProcessControlCapabilities.PriorityRead
		| ProcessControlCapabilities.PriorityWrite
		| ( OperatingSystem.IsWindows()
			? ProcessControlCapabilities.WindowsPrioritySubstitution
			: ProcessControlCapabilities.ProcessGroupTargets )
	;

	/// <summary>Initializes a system priority provider.</summary>
	public SystemProcessPriorityProvider(
		IProcessInspector inspector
	) {
		ArgumentNullException.ThrowIfNull(
			inspector
		);
		this._inspector = inspector;
	}

	/// <inheritdoc />
	public ProcessOperationResult<ProcessPriorityValue> GetPriority(
		ProcessTarget target
	) {
		ArgumentNullException.ThrowIfNull(
			target
		);
		var identityFailure = this.ValidateIdentity(
			target
		);
		if ( null != identityFailure ) {
			return ProcessOperationResult<ProcessPriorityValue>.Failure(
				identityFailure.Status,
				identityFailure.Message,
				identityFailure.NativeErrorCode
			);
		}
		if ( OperatingSystem.IsWindows() ) {
			return GetWindowsPriority(
				target
			);
		}
		var which = GetPosixWhich(
			target
		);
		if ( null == which ) {
			return ProcessOperationResult<ProcessPriorityValue>.Failure(
				ProcessOperationStatus.Unsupported,
				"Session priorities do not have a portable getpriority target."
			);
		}
		Marshal.SetLastPInvokeError(
			0
		);
		var value = ProcessNative.GetPriority(
			which.Value,
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
				Math.Clamp(
					value,
					-20,
					19
				),
				false
			)
		);
	}

	/// <inheritdoc />
	public ProcessOperationResult SetPriority(
		ProcessTarget target,
		int niceValue
	) {
		ArgumentNullException.ThrowIfNull(
			target
		);
		if ( -20 > niceValue || 19 < niceValue ) {
			return ProcessOperationResult.Failure(
				ProcessOperationStatus.InvalidArgument,
				"Nice values must be between -20 and 19."
			);
		}
		var identityFailure = this.ValidateIdentity(
			target
		);
		if ( null != identityFailure ) {
			return identityFailure;
		}
		if ( OperatingSystem.IsWindows() ) {
			return SetWindowsPriority(
				target,
				niceValue
			);
		}
		var which = GetPosixWhich(
			target
		);
		if ( null == which ) {
			return ProcessOperationResult.Failure(
				ProcessOperationStatus.Unsupported,
				"Session priorities do not have a portable setpriority target."
			);
		}
		if ( 0 == ProcessNative.SetPriority(
			which.Value,
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

	/// <inheritdoc />
	public ProcessOperationResult<ProcessPriorityValue> AdjustPriority(
		ProcessTarget target,
		int increment
	) {
		var current = this.GetPriority(
			target
		);
		if ( !current.Succeeded ) {
			return current;
		}
		var requested = checked( (int)Math.Clamp(
			(long)current.Value!.NiceValue + increment,
			-20L,
			19L
		) );
		var changed = this.SetPriority(
			target,
			requested
		);
		return changed.Succeeded
			? this.GetPriority(
				target
			)
			: ProcessOperationResult<ProcessPriorityValue>.Failure(
				changed.Status,
				changed.Message,
				changed.NativeErrorCode
			)
		;
	}

	private ProcessOperationResult? ValidateIdentity(
		ProcessTarget target
	) {
		if ( ProcessTargetKind.Process != target.Kind || null == target.Identity?.ReuseToken ) {
			return null;
		}
		var observed = this._inspector.ObserveIdentity(
			target.Identifier
		);
		if ( !observed.Succeeded ) {
			return ProcessOperationResult.Failure(
				observed.Status,
				observed.Message,
				observed.NativeErrorCode
			);
		}
		return SystemProcessInspector.MatchesExpectedIdentity(
			target.Identity,
			observed.Value!
		)
			? null
			: ProcessOperationResult.Failure(
				ProcessOperationStatus.Reused,
				$"Process identifier {target.Identifier} has been reused."
			)
		;
	}

	private static int? GetPosixWhich(
		ProcessTarget target
	) => target.Kind switch {
		ProcessTargetKind.Process => PriorityProcess,
		ProcessTargetKind.ProcessGroup => PriorityProcessGroup,
		_ => null
	};

	private static ProcessOperationResult<ProcessPriorityValue> GetWindowsPriority(
		ProcessTarget target
	) {
		if ( ProcessTargetKind.Process != target.Kind ) {
			return ProcessOperationResult<ProcessPriorityValue>.Failure(
				ProcessOperationStatus.Unsupported,
				"Windows priority classes apply to individual processes."
			);
		}
		try {
			using var process = Process.GetProcessById(
				target.Identifier
			);
			var value = process.PriorityClass switch {
				ProcessPriorityClass.RealTime => -20,
				ProcessPriorityClass.High => -10,
				ProcessPriorityClass.AboveNormal => -5,
				ProcessPriorityClass.Normal => 0,
				ProcessPriorityClass.BelowNormal => 10,
				ProcessPriorityClass.Idle => 19,
				_ => 0
			};
			return ProcessOperationResult<ProcessPriorityValue>.Success(
				new ProcessPriorityValue(
					value,
					true
				),
				"The nice value approximates a Windows process priority class."
			);
		} catch ( ArgumentException ) {
			return ProcessOperationResult<ProcessPriorityValue>.Failure(
				ProcessOperationStatus.Vanished,
				$"Process {target.Identifier} does not exist."
			);
		} catch ( InvalidOperationException ) {
			return ProcessOperationResult<ProcessPriorityValue>.Failure(
				ProcessOperationStatus.Vanished,
				$"Process {target.Identifier} has exited."
			);
		} catch ( Win32Exception exception ) {
			return ProcessOperationResult<ProcessPriorityValue>.Failure(
				ProcessOperationStatus.AccessDenied,
				exception.Message,
				exception.NativeErrorCode
			);
		}
	}

	private static ProcessOperationResult SetWindowsPriority(
		ProcessTarget target,
		int niceValue
	) {
		if ( ProcessTargetKind.Process != target.Kind ) {
			return ProcessOperationResult.Failure(
				ProcessOperationStatus.Unsupported,
				"Windows priority classes apply to individual processes."
			);
		}
		var priorityClass = niceValue switch {
			<= -15 => ProcessPriorityClass.RealTime,
			<= -8 => ProcessPriorityClass.High,
			<= -3 => ProcessPriorityClass.AboveNormal,
			<= 5 => ProcessPriorityClass.Normal,
			<= 14 => ProcessPriorityClass.BelowNormal,
			_ => ProcessPriorityClass.Idle
		};
		try {
			using var process = Process.GetProcessById(
				target.Identifier
			);
			process.PriorityClass = priorityClass;
			return ProcessOperationResult.Success(
				$"Windows priority class {priorityClass} substituted for nice value {niceValue}.",
				true
			);
		} catch ( ArgumentException ) {
			return ProcessOperationResult.Failure(
				ProcessOperationStatus.Vanished,
				$"Process {target.Identifier} does not exist."
			);
		} catch ( InvalidOperationException ) {
			return ProcessOperationResult.Failure(
				ProcessOperationStatus.Vanished,
				$"Process {target.Identifier} has exited."
			);
		} catch ( Win32Exception exception ) {
			return ProcessOperationResult.Failure(
				ProcessOperationStatus.AccessDenied,
				exception.Message,
				exception.NativeErrorCode
			);
		}
	}
}
