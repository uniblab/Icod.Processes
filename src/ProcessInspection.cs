namespace Icod.Processes;

using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;

/// <summary>
/// Observes process identity, liveness, and termination through injectable contracts.
/// </summary>
public interface IProcessInspector {
	/// <summary>Gets capabilities available from this provider.</summary>
	ProcessControlCapabilities Capabilities {
		get;
	}

	/// <summary>Observes a process identity and an optional PID-reuse token.</summary>
	ProcessOperationResult<ProcessIdentity> ObserveIdentity(
		int processId
	);

	/// <summary>Observes whether a target is currently live.</summary>
	ProcessOperationResult<bool> ObserveLiveness(
		ProcessTarget target
	);

	/// <summary>Waits asynchronously for an arbitrary process to terminate.</summary>
	Task<ProcessOperationResult<ProcessTermination>> WaitAsync(
		ProcessIdentity identity,
		CancellationToken cancellationToken = default
	);
}

/// <summary>
/// Provides cross-platform process observation using BCL process handles and controlled native probes.
/// </summary>
public sealed class SystemProcessInspector : IProcessInspector {
	/// <summary>Gets the shared system process inspector.</summary>
	public static SystemProcessInspector Instance {
		get;
	} = new();

	/// <inheritdoc />
	public ProcessControlCapabilities Capabilities {
		get {
			var capabilities = ProcessControlCapabilities.ProcessIdentity
				| ProcessControlCapabilities.ReuseToken
				| ProcessControlCapabilities.Liveness
				| ProcessControlCapabilities.ArbitraryProcessWait
			;
			if ( !OperatingSystem.IsWindows() ) {
				capabilities |= ProcessControlCapabilities.ProcessGroupTargets;
			}
			return capabilities;
		}
	}

	private SystemProcessInspector() {
	}

	/// <inheritdoc />
	public ProcessOperationResult<ProcessIdentity> ObserveIdentity(
		int processId
	) {
		if ( 0 >= processId ) {
			return ProcessOperationResult<ProcessIdentity>.Failure(
				ProcessOperationStatus.InvalidArgument,
				"A positive process identifier is required."
			);
		}
		try {
			using var process = Process.GetProcessById(
				processId
			);
			if ( process.HasExited ) {
				return ProcessOperationResult<ProcessIdentity>.Failure(
					ProcessOperationStatus.Vanished,
					$"Process {processId} has exited."
				);
			}
			return ProcessOperationResult<ProcessIdentity>.Success(
				new ProcessIdentity(
					processId,
					TryReadReuseToken(
						process
					)
				)
			);
		} catch ( ArgumentException ) {
			return ProcessOperationResult<ProcessIdentity>.Failure(
				ProcessOperationStatus.Vanished,
				$"Process {processId} does not exist."
			);
		} catch ( InvalidOperationException ) {
			return ProcessOperationResult<ProcessIdentity>.Failure(
				ProcessOperationStatus.Vanished,
				$"Process {processId} has exited."
			);
		} catch ( Win32Exception exception ) {
			return ProcessOperationResult<ProcessIdentity>.Failure(
				ProcessOperationStatus.AccessDenied,
				exception.Message,
				exception.NativeErrorCode
			);
		} catch ( UnauthorizedAccessException exception ) {
			return ProcessOperationResult<ProcessIdentity>.Failure(
				ProcessOperationStatus.AccessDenied,
				exception.Message
			);
		}
	}

	/// <inheritdoc />
	public ProcessOperationResult<bool> ObserveLiveness(
		ProcessTarget target
	) {
		ArgumentNullException.ThrowIfNull(
			target
		);
		if ( ProcessTargetKind.Process == target.Kind ) {
			var observed = this.ObserveIdentity(
				target.Identifier
			);
			if ( !observed.Succeeded ) {
				return ProcessOperationResult<bool>.Failure(
					observed.Status,
					observed.Message,
					observed.NativeErrorCode
				);
			}
			if ( !MatchesExpectedIdentity(
				target.Identity,
				observed.Value!
			) ) {
				return ProcessOperationResult<bool>.Failure(
					ProcessOperationStatus.Reused,
					$"Process identifier {target.Identifier} has been reused."
				);
			}
			return ProcessOperationResult<bool>.Success(
				true
			);
		}
		if ( OperatingSystem.IsWindows() ) {
			return ProcessOperationResult<bool>.Failure(
				ProcessOperationStatus.Unsupported,
				"Process-group and session liveness probes are not exposed on Windows."
			);
		}
		if ( ProcessTargetKind.Session == target.Kind ) {
			return ProcessOperationResult<bool>.Failure(
				ProcessOperationStatus.Unsupported,
				"POSIX does not provide a direct session-liveness probe."
			);
		}
		var nativeTarget = checked( -target.Identifier );
		if ( 0 == ProcessNative.Kill(
			nativeTarget,
			0
		) ) {
			return ProcessOperationResult<bool>.Success(
				true
			);
		}
		var error = Marshal.GetLastPInvokeError();
		if ( ProcessNative.NoSuchProcess == error ) {
			return ProcessOperationResult<bool>.Success(
				false
			);
		}
		return ProcessOperationResult<bool>.Failure(
			ProcessNative.MapErrno( error ),
			$"Unable to inspect process group {target.Identifier}.",
			error
		);
	}

	/// <inheritdoc />
	public async Task<ProcessOperationResult<ProcessTermination>> WaitAsync(
		ProcessIdentity identity,
		CancellationToken cancellationToken = default
	) {
		ArgumentNullException.ThrowIfNull(
			identity
		);
		try {
			using var process = Process.GetProcessById(
				identity.ProcessId
			);
			if ( process.HasExited ) {
				return ProcessOperationResult<ProcessTermination>.Failure(
					ProcessOperationStatus.Vanished,
					$"Process {identity.ProcessId} has exited."
				);
			}
			var attachedIdentity = new ProcessIdentity(
				identity.ProcessId,
				TryReadReuseToken(
					process
				)
			);
			if ( !MatchesExpectedIdentity(
				identity,
				attachedIdentity
			) ) {
				return ProcessOperationResult<ProcessTermination>.Failure(
					ProcessOperationStatus.Reused,
					$"Process identifier {identity.ProcessId} has been reused."
				);
			}
			await process.WaitForExitAsync(
				cancellationToken
			).ConfigureAwait( false );
			return ProcessOperationResult<ProcessTermination>.Success(
				ProcessTermination.Exited(
					process.ExitCode
				)
			);
		} catch ( OperationCanceledException ) when ( cancellationToken.IsCancellationRequested ) {
			return ProcessOperationResult<ProcessTermination>.Failure(
				ProcessOperationStatus.Canceled,
				"The process wait was canceled."
			);
		} catch ( ArgumentException ) {
			return ProcessOperationResult<ProcessTermination>.Failure(
				ProcessOperationStatus.Vanished,
				$"Process {identity.ProcessId} vanished before it could be awaited."
			);
		} catch ( InvalidOperationException exception ) {
			return ProcessOperationResult<ProcessTermination>.Failure(
				ProcessOperationStatus.Vanished,
				exception.Message
			);
		} catch ( Win32Exception exception ) {
			return ProcessOperationResult<ProcessTermination>.Failure(
				ProcessOperationStatus.AccessDenied,
				exception.Message,
				exception.NativeErrorCode
			);
		} catch ( UnauthorizedAccessException exception ) {
			return ProcessOperationResult<ProcessTermination>.Failure(
				ProcessOperationStatus.AccessDenied,
				exception.Message
			);
		}
	}

	/// <summary>Determines whether an observed identity still matches a protected expected identity.</summary>
	internal static bool MatchesExpectedIdentity(
		ProcessIdentity? expected,
		ProcessIdentity observed
	) => null == expected
		|| null == expected.ReuseToken
		|| expected.Equals( observed )
	;

	private static ProcessReuseToken? TryReadReuseToken(
		Process process
	) {
		if ( OperatingSystem.IsLinux() ) {
			var linuxToken = TryReadLinuxStartTime(
				process.Id
			);
			if ( null != linuxToken ) {
				return linuxToken;
			}
		}
		try {
			return new ProcessReuseToken(
				"start-time-utc-ticks",
				process.StartTime.ToUniversalTime().Ticks.ToString(
					CultureInfo.InvariantCulture
				)
			);
		} catch ( InvalidOperationException ) {
			return null;
		} catch ( Win32Exception ) {
			return null;
		} catch ( NotSupportedException ) {
			return null;
		}
	}

	private static ProcessReuseToken? TryReadLinuxStartTime(
		int processId
	) {
		try {
			var text = File.ReadAllText(
				$"/proc/{processId}/stat"
			);
			var commandEnd = text.LastIndexOf(
				')'
			);
			if ( 0 > commandEnd || commandEnd + 2 >= text.Length ) {
				return null;
			}
			var fields = text[ ( commandEnd + 2 ).. ].Split(
				' ',
				StringSplitOptions.RemoveEmptyEntries
			);
			const int StartTimeIndexFromStateField = 19;
			if ( StartTimeIndexFromStateField >= fields.Length ) {
				return null;
			}
			return new ProcessReuseToken(
				"linux-proc-starttime",
				fields[ StartTimeIndexFromStateField ]
			);
		} catch ( IOException ) {
			return null;
		} catch ( UnauthorizedAccessException ) {
			return null;
		}
	}
}
