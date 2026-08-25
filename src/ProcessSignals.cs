namespace Icod.Processes;

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;

/// <summary>
/// Describes a portable or host-specific signal number and canonical name.
/// </summary>
public sealed class ProcessSignal : IEquatable<ProcessSignal> {
	/// <summary>Gets the canonical signal name without the SIG prefix.</summary>
	public string Name {
		get;
	}

	/// <summary>Gets the native signal number.</summary>
	public int Number {
		get;
	}

	/// <summary>Initializes a signal value.</summary>
	public ProcessSignal(
		int number,
		string name
	) {
		ArgumentOutOfRangeException.ThrowIfNegative(
			number
		);
		ArgumentException.ThrowIfNullOrWhiteSpace(
			name
		);
		this.Number = number;
		this.Name = name.ToUpperInvariant();
	}

	/// <inheritdoc />
	public bool Equals(
		ProcessSignal? other
	) => null != other && this.Number == other.Number;

	/// <inheritdoc />
	public override bool Equals(
		object? obj
	) => this.Equals(
		obj as ProcessSignal
	);

	/// <inheritdoc />
	public override int GetHashCode() => this.Number;

	/// <inheritdoc />
	public override string ToString() => string.Concat(
		"SIG",
		this.Name
	);
}

/// <summary>
/// Describes an observed signal disposition.
/// </summary>
public enum ProcessSignalDisposition {
	/// <summary>The default signal action is installed.</summary>
	Default,
	/// <summary>The signal is ignored.</summary>
	Ignored,
	/// <summary>A signal handler is installed.</summary>
	Caught,
	/// <summary>The disposition cannot be determined.</summary>
	Unknown
}

/// <summary>
/// Parses, lists, translates, observes, and delivers process signals.
/// </summary>
public interface IProcessSignalProvider {
	/// <summary>Gets capabilities available from this provider.</summary>
	ProcessControlCapabilities Capabilities {
		get;
	}

	/// <summary>Gets the signals known on the current host.</summary>
	IReadOnlyList<ProcessSignal> ListSignals();

	/// <summary>Parses a signal name, number, or supported real-time expression.</summary>
	ProcessOperationResult<ProcessSignal> ParseSignal(
		string text
	);

	/// <summary>Translates a signal number to its canonical representation.</summary>
	ProcessOperationResult<ProcessSignal> TranslateSignal(
		int number
	);

	/// <summary>Observes a process signal disposition where the host exposes it.</summary>
	ProcessOperationResult<ProcessSignalDisposition> ObserveDisposition(
		ProcessIdentity identity,
		ProcessSignal signal
	);

	/// <summary>Delivers a signal or a documented platform substitution.</summary>
	Task<ProcessOperationResult> DeliverAsync(
		ProcessTarget target,
		ProcessSignal signal,
		int? queuedValue = null,
		CancellationToken cancellationToken = default
	);
}

/// <summary>
/// Observes the blocked-mask state of process signals where the host exposes it.
/// </summary>
public interface IProcessSignalMaskProvider {
	/// <summary>Observes whether a signal is blocked for the identified process.</summary>
	ProcessOperationResult<bool> ObserveBlocked(
		ProcessIdentity identity,
		ProcessSignal signal
	);
}

/// <summary>
/// Supplies the portable signal catalog used by process-control commands.
/// </summary>
public static class ProcessSignalCatalog {
	private static readonly ProcessSignal[] LinuxSignals = [
		new( 0, "0" ),
		new( 1, "HUP" ),
		new( 2, "INT" ),
		new( 3, "QUIT" ),
		new( 4, "ILL" ),
		new( 5, "TRAP" ),
		new( 6, "ABRT" ),
		new( 7, "BUS" ),
		new( 8, "FPE" ),
		new( 9, "KILL" ),
		new( 10, "USR1" ),
		new( 11, "SEGV" ),
		new( 12, "USR2" ),
		new( 13, "PIPE" ),
		new( 14, "ALRM" ),
		new( 15, "TERM" ),
		new( 16, "STKFLT" ),
		new( 17, "CHLD" ),
		new( 18, "CONT" ),
		new( 19, "STOP" ),
		new( 20, "TSTP" ),
		new( 21, "TTIN" ),
		new( 22, "TTOU" ),
		new( 23, "URG" ),
		new( 24, "XCPU" ),
		new( 25, "XFSZ" ),
		new( 26, "VTALRM" ),
		new( 27, "PROF" ),
		new( 28, "WINCH" ),
		new( 29, "IO" ),
		new( 30, "PWR" ),
		new( 31, "SYS" )
	];

	private static readonly ProcessSignal[] DarwinSignals = [
		new( 0, "0" ),
		new( 1, "HUP" ),
		new( 2, "INT" ),
		new( 3, "QUIT" ),
		new( 4, "ILL" ),
		new( 5, "TRAP" ),
		new( 6, "ABRT" ),
		new( 7, "EMT" ),
		new( 8, "FPE" ),
		new( 9, "KILL" ),
		new( 10, "BUS" ),
		new( 11, "SEGV" ),
		new( 12, "SYS" ),
		new( 13, "PIPE" ),
		new( 14, "ALRM" ),
		new( 15, "TERM" ),
		new( 16, "URG" ),
		new( 17, "STOP" ),
		new( 18, "TSTP" ),
		new( 19, "CONT" ),
		new( 20, "CHLD" ),
		new( 21, "TTIN" ),
		new( 22, "TTOU" ),
		new( 23, "IO" ),
		new( 24, "XCPU" ),
		new( 25, "XFSZ" ),
		new( 26, "VTALRM" ),
		new( 27, "PROF" ),
		new( 28, "WINCH" ),
		new( 29, "INFO" ),
		new( 30, "USR1" ),
		new( 31, "USR2" )
	];

	private static readonly IReadOnlyDictionary<string, string> Aliases =
		new ReadOnlyDictionary<string, string>(
			new Dictionary<string, string>(
				StringComparer.OrdinalIgnoreCase
			) {
				[ "IOT" ] = "ABRT",
				[ "CLD" ] = "CHLD",
				[ "POLL" ] = "IO"
			}
		);

	private static ProcessSignal[] CurrentSignals => OperatingSystem.IsMacOS()
		? DarwinSignals
		: LinuxSignals
	;

	/// <summary>Gets the portable signal catalog.</summary>
	public static IReadOnlyList<ProcessSignal> PortableSignals => Array.AsReadOnly(
		CurrentSignals
	);

	/// <summary>Parses a portable signal specification.</summary>
	public static ProcessOperationResult<ProcessSignal> Parse(
		string text
	) {
		if ( string.IsNullOrWhiteSpace( text ) ) {
			return ProcessOperationResult<ProcessSignal>.Failure(
				ProcessOperationStatus.InvalidArgument,
				"A signal name or number is required."
			);
		}
		var normalized = text.Trim();
		if ( normalized.StartsWith(
			"SIG",
			StringComparison.OrdinalIgnoreCase
		) ) {
			normalized = normalized[ 3.. ];
		}
		if ( int.TryParse(
			normalized,
			NumberStyles.Integer,
			CultureInfo.InvariantCulture,
			out var number
		) ) {
			return Translate(
				number
			);
		}
		if ( TryParseRealtime(
			normalized,
			out var realtimeNumber
		) ) {
			return ProcessOperationResult<ProcessSignal>.Success(
				new ProcessSignal(
					realtimeNumber,
					GetRealtimeName(
						realtimeNumber
					)
				)
			);
		}
		var signal = CurrentSignals.FirstOrDefault(
			candidate => string.Equals(
				candidate.Name,
				normalized,
				StringComparison.OrdinalIgnoreCase
			)
		);
		if ( null != signal ) {
			return ProcessOperationResult<ProcessSignal>.Success(
				signal
			);
		}
		if ( Aliases.TryGetValue(
			normalized,
			out var canonicalName
		) ) {
			signal = CurrentSignals.FirstOrDefault(
				candidate => string.Equals(
					candidate.Name,
					canonicalName,
					StringComparison.OrdinalIgnoreCase
				)
			);
			if ( null != signal ) {
				return ProcessOperationResult<ProcessSignal>.Success(
					signal
				);
			}
		}
		return ProcessOperationResult<ProcessSignal>.Failure(
			ProcessOperationStatus.InvalidArgument,
			$"Unknown signal '{text}'."
		);
	}

	/// <summary>Translates a signal number to a canonical value.</summary>
	public static ProcessOperationResult<ProcessSignal> Translate(
		int number
	) {
		var signal = CurrentSignals.FirstOrDefault(
			candidate => candidate.Number == number
		);
		if ( null != signal ) {
			return ProcessOperationResult<ProcessSignal>.Success(
				signal
			);
		}
		if ( IsRealtimeSignal( number ) ) {
			return ProcessOperationResult<ProcessSignal>.Success(
				new ProcessSignal(
					number,
					GetRealtimeName(
						number
					)
				)
			);
		}
		return ProcessOperationResult<ProcessSignal>.Failure(
			ProcessOperationStatus.InvalidArgument,
			$"Signal number {number} is not valid on this host."
		);
	}

	private static bool TryParseRealtime(
		string text,
		out int number
	) {
		number = 0;
		if ( !OperatingSystem.IsLinux() ) {
			return false;
		}
		const int minimum = 34;
		const int maximum = 64;
		if ( string.Equals(
			text,
			"RTMIN",
			StringComparison.OrdinalIgnoreCase
		) ) {
			number = minimum;
			return true;
		}
		if ( string.Equals(
			text,
			"RTMAX",
			StringComparison.OrdinalIgnoreCase
		) ) {
			number = maximum;
			return true;
		}
		if ( text.StartsWith(
			"RTMIN+",
			StringComparison.OrdinalIgnoreCase
		) && int.TryParse(
			text[ 6.. ],
			NumberStyles.None,
			CultureInfo.InvariantCulture,
			out var minimumOffset
		) ) {
			number = minimum + minimumOffset;
			return minimum <= number && maximum >= number;
		}
		if ( text.StartsWith(
			"RTMAX-",
			StringComparison.OrdinalIgnoreCase
		) && int.TryParse(
			text[ 6.. ],
			NumberStyles.None,
			CultureInfo.InvariantCulture,
			out var maximumOffset
		) ) {
			number = maximum - maximumOffset;
			return minimum <= number && maximum >= number;
		}
		return false;
	}

	private static bool IsRealtimeSignal(
		int number
	) => OperatingSystem.IsLinux() && 34 <= number && 64 >= number;

	private static string GetRealtimeName(
		int number
	) {
		const int minimum = 34;
		const int maximum = 64;
		var fromMinimum = number - minimum;
		var fromMaximum = maximum - number;
		return fromMinimum <= fromMaximum
			? 0 == fromMinimum
				? "RTMIN"
				: $"RTMIN+{fromMinimum}"
			: 0 == fromMaximum
				? "RTMAX"
				: $"RTMAX-{fromMaximum}"
		;
	}
}

/// <summary>
/// Provides host signal operations and controlled Windows substitutions.
/// </summary>
public sealed class SystemProcessSignalProvider : IProcessSignalProvider, IProcessSignalMaskProvider {
	private readonly IProcessInspector _inspector;

	/// <summary>Gets the shared system signal provider.</summary>
	public static SystemProcessSignalProvider Instance {
		get;
	} = new(
		SystemProcessInspector.Instance
	);

	/// <inheritdoc />
	public ProcessControlCapabilities Capabilities {
		get {
			if ( OperatingSystem.IsWindows() ) {
				return ProcessControlCapabilities.SignalDelivery
					| ProcessControlCapabilities.WindowsTerminationSubstitution
				;
			}
			var capabilities = ProcessControlCapabilities.SignalDelivery
				| ProcessControlCapabilities.ProcessGroupTargets
			;
			if ( OperatingSystem.IsLinux() ) {
				capabilities |= ProcessControlCapabilities.SignalDisposition
					| ProcessControlCapabilities.SignalMaskObservation
					| ProcessControlCapabilities.QueuedSignalDelivery
				;
			}
			return capabilities;
		}
	}

	/// <summary>Initializes a system signal provider.</summary>
	public SystemProcessSignalProvider(
		IProcessInspector inspector
	) {
		ArgumentNullException.ThrowIfNull(
			inspector
		);
		this._inspector = inspector;
	}

	/// <inheritdoc />
	public IReadOnlyList<ProcessSignal> ListSignals() {
		if ( !OperatingSystem.IsLinux() ) {
			return ProcessSignalCatalog.PortableSignals;
		}
		var signals = new List<ProcessSignal>(
			ProcessSignalCatalog.PortableSignals
		);
		for ( var number = 34; number <= 64; number++ ) {
			var translated = ProcessSignalCatalog.Translate(
				number
			);
			if ( translated.Succeeded ) {
				signals.Add(
					translated.Value!
				);
			}
		}
		return signals;
	}

	/// <inheritdoc />
	public ProcessOperationResult<ProcessSignal> ParseSignal(
		string text
	) => ProcessSignalCatalog.Parse(
		text
	);

	/// <inheritdoc />
	public ProcessOperationResult<ProcessSignal> TranslateSignal(
		int number
	) => ProcessSignalCatalog.Translate(
		number
	);

	/// <inheritdoc />
	public ProcessOperationResult<ProcessSignalDisposition> ObserveDisposition(
		ProcessIdentity identity,
		ProcessSignal signal
	) {
		ArgumentNullException.ThrowIfNull(
			identity
		);
		ArgumentNullException.ThrowIfNull(
			signal
		);
		if ( !OperatingSystem.IsLinux() ) {
			return ProcessOperationResult<ProcessSignalDisposition>.Failure(
				ProcessOperationStatus.Unsupported,
				"Signal dispositions are exposed only through Linux /proc."
			);
		}
		if ( 0 >= signal.Number || 64 < signal.Number ) {
			return ProcessOperationResult<ProcessSignalDisposition>.Failure(
				ProcessOperationStatus.InvalidArgument,
				"Only numbered Linux signals have observable dispositions."
			);
		}
		var observed = this._inspector.ObserveIdentity(
			identity.ProcessId
		);
		if ( !observed.Succeeded ) {
			return ProcessOperationResult<ProcessSignalDisposition>.Failure(
				observed.Status,
				observed.Message,
				observed.NativeErrorCode
			);
		}
		if ( !SystemProcessInspector.MatchesExpectedIdentity(
			identity,
			observed.Value!
		) ) {
			return ProcessOperationResult<ProcessSignalDisposition>.Failure(
				ProcessOperationStatus.Reused,
				$"Process identifier {identity.ProcessId} has been reused."
			);
		}
		try {
			var statusLines = File.ReadAllLines(
				$"/proc/{identity.ProcessId}/status"
			);
			var ignored = ParseSignalMask(
				statusLines,
				"SigIgn:"
			);
			var caught = ParseSignalMask(
				statusLines,
				"SigCgt:"
			);
			var bit = 1UL << ( signal.Number - 1 );
			return ProcessOperationResult<ProcessSignalDisposition>.Success(
				0 != ( ignored & bit )
					? ProcessSignalDisposition.Ignored
					: 0 != ( caught & bit )
						? ProcessSignalDisposition.Caught
						: ProcessSignalDisposition.Default
			);
		} catch ( FileNotFoundException ) {
			return ProcessOperationResult<ProcessSignalDisposition>.Failure(
				ProcessOperationStatus.Vanished,
				$"Process {identity.ProcessId} vanished."
			);
		} catch ( DirectoryNotFoundException ) {
			return ProcessOperationResult<ProcessSignalDisposition>.Failure(
				ProcessOperationStatus.Vanished,
				$"Process {identity.ProcessId} vanished."
			);
		} catch ( UnauthorizedAccessException exception ) {
			return ProcessOperationResult<ProcessSignalDisposition>.Failure(
				ProcessOperationStatus.AccessDenied,
				exception.Message
			);
		} catch ( IOException exception ) {
			return ProcessOperationResult<ProcessSignalDisposition>.Failure(
				ProcessOperationStatus.Failed,
				exception.Message
			);
		}
	}

	/// <inheritdoc />
	public ProcessOperationResult<bool> ObserveBlocked(
		ProcessIdentity identity,
		ProcessSignal signal
	) {
		ArgumentNullException.ThrowIfNull(
			identity
		);
		ArgumentNullException.ThrowIfNull(
			signal
		);
		if ( !OperatingSystem.IsLinux() ) {
			return ProcessOperationResult<bool>.Failure(
				ProcessOperationStatus.Unsupported,
				"Blocked signal masks are exposed only through Linux /proc."
			);
		}
		if ( 0 >= signal.Number || 64 < signal.Number ) {
			return ProcessOperationResult<bool>.Failure(
				ProcessOperationStatus.InvalidArgument,
				"Only numbered Linux signals have observable blocked-mask state."
			);
		}
		var observed = this._inspector.ObserveIdentity(
			identity.ProcessId
		);
		if ( !observed.Succeeded ) {
			return ProcessOperationResult<bool>.Failure(
				observed.Status,
				observed.Message,
				observed.NativeErrorCode
			);
		}
		if ( !SystemProcessInspector.MatchesExpectedIdentity(
			identity,
			observed.Value!
		) ) {
			return ProcessOperationResult<bool>.Failure(
				ProcessOperationStatus.Reused,
				$"Process identifier {identity.ProcessId} has been reused."
			);
		}
		try {
			var statusLines = File.ReadAllLines(
				$"/proc/{identity.ProcessId}/status"
			);
			var blocked = ParseSignalMask(
				statusLines,
				"SigBlk:"
			);
			var bit = 1UL << ( signal.Number - 1 );
			return ProcessOperationResult<bool>.Success(
				0 != ( blocked & bit )
			);
		} catch ( FileNotFoundException ) {
			return ProcessOperationResult<bool>.Failure(
				ProcessOperationStatus.Vanished,
				$"Process {identity.ProcessId} vanished."
			);
		} catch ( DirectoryNotFoundException ) {
			return ProcessOperationResult<bool>.Failure(
				ProcessOperationStatus.Vanished,
				$"Process {identity.ProcessId} vanished."
			);
		} catch ( UnauthorizedAccessException exception ) {
			return ProcessOperationResult<bool>.Failure(
				ProcessOperationStatus.AccessDenied,
				exception.Message
			);
		} catch ( IOException exception ) {
			return ProcessOperationResult<bool>.Failure(
				ProcessOperationStatus.Failed,
				exception.Message
			);
		}
	}

	/// <inheritdoc />
	public Task<ProcessOperationResult> DeliverAsync(
		ProcessTarget target,
		ProcessSignal signal,
		int? queuedValue = null,
		CancellationToken cancellationToken = default
	) {
		ArgumentNullException.ThrowIfNull(
			target
		);
		ArgumentNullException.ThrowIfNull(
			signal
		);
		if ( cancellationToken.IsCancellationRequested ) {
			return Task.FromResult(
				ProcessOperationResult.Failure(
					ProcessOperationStatus.Canceled,
					"Signal delivery was canceled."
				)
			);
		}
		if ( null != queuedValue && ( !OperatingSystem.IsLinux() || ProcessTargetKind.Process != target.Kind ) ) {
			return Task.FromResult(
				ProcessOperationResult.Failure(
					ProcessOperationStatus.Unsupported,
					"Queued signal values are supported only for individual Linux process targets."
				)
			);
		}
		return Task.FromResult(
			OperatingSystem.IsWindows()
				? this.DeliverWindows(
					target,
					signal
				)
				: this.DeliverPosix(
					target,
					signal,
					queuedValue
				)
		);
	}

	private ProcessOperationResult DeliverPosix(
		ProcessTarget target,
		ProcessSignal signal,
		int? queuedValue
	) {
		if ( ProcessTargetKind.Session == target.Kind ) {
			return ProcessOperationResult.Failure(
				ProcessOperationStatus.Unsupported,
				"POSIX signal delivery has no atomic session-target operation."
			);
		}
		if ( ProcessTargetKind.Process == target.Kind && null != target.Identity?.ReuseToken ) {
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
			if ( !SystemProcessInspector.MatchesExpectedIdentity(
				target.Identity,
				observed.Value!
			) ) {
				return ProcessOperationResult.Failure(
					ProcessOperationStatus.Reused,
					$"Process identifier {target.Identifier} has been reused."
				);
			}
		}
		var nativeTarget = ProcessTargetKind.ProcessGroup == target.Kind
			? checked( -target.Identifier )
			: target.Identifier
		;
		int nativeResult;
		try {
			nativeResult = null == queuedValue
				? ProcessNative.Kill(
					nativeTarget,
					signal.Number
				)
				: ProcessNative.SigQueue(
					target.Identifier,
					signal.Number,
					new ProcessNative.SignalValue( queuedValue.Value )
				)
			;
		} catch ( EntryPointNotFoundException exception ) when ( null != queuedValue ) {
			return ProcessOperationResult.Failure(
				ProcessOperationStatus.Unsupported,
				exception.Message
			);
		}
		if ( 0 == nativeResult ) {
			return ProcessOperationResult.Success();
		}
		var error = Marshal.GetLastPInvokeError();
		return ProcessOperationResult.Failure(
			ProcessNative.MapErrno( error ),
			$"Unable to deliver {signal} to {target.Kind} {target.Identifier}.",
			error
		);
	}

	private ProcessOperationResult DeliverWindows(
		ProcessTarget target,
		ProcessSignal signal
	) {
		if ( ProcessTargetKind.Process != target.Kind ) {
			return ProcessOperationResult.Failure(
				ProcessOperationStatus.Unsupported,
				"Windows does not expose POSIX process-group or session signal targets."
			);
		}
		if ( null != target.Identity?.ReuseToken ) {
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
			if ( !SystemProcessInspector.MatchesExpectedIdentity(
				target.Identity,
				observed.Value!
			) ) {
				return ProcessOperationResult.Failure(
					ProcessOperationStatus.Reused,
					$"Process identifier {target.Identifier} has been reused."
				);
			}
		}
		if ( 0 == signal.Number ) {
			var liveness = this._inspector.ObserveLiveness(
				target
			);
			return liveness.Succeeded
				? ProcessOperationResult.Success()
				: ProcessOperationResult.Failure(
					liveness.Status,
					liveness.Message,
					liveness.NativeErrorCode
				)
			;
		}
		if ( 9 != signal.Number && 15 != signal.Number ) {
			return ProcessOperationResult.Failure(
				ProcessOperationStatus.Unsupported,
				$"Windows has no defensible substitution for {signal}."
			);
		}
		try {
			using var process = Process.GetProcessById(
				target.Identifier
			);
			process.Kill();
			return ProcessOperationResult.Success(
				$"Windows process termination substituted for {signal}.",
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

	private static ulong ParseSignalMask(
		IEnumerable<string> lines,
		string prefix
	) {
		var line = lines.FirstOrDefault(
			candidate => candidate.StartsWith(
				prefix,
				StringComparison.Ordinal
			)
		);
		if ( null == line ) {
			return 0;
		}
		return ulong.TryParse(
			line[ prefix.Length.. ].Trim(),
			NumberStyles.AllowHexSpecifier,
			CultureInfo.InvariantCulture,
			out var mask
		)
			? mask
			: 0
		;
	}
}
