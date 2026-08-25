namespace Icod.Processes;

using System.Collections.ObjectModel;

/// <summary>
/// Identifies a signal disposition requested specifically for a newly launched child process.
/// </summary>
public enum ProcessSignalLaunchDisposition {
	/// <summary>Reset the signal to its platform default disposition.</summary>
	Default,
	/// <summary>Arrange for the child process to ignore the signal.</summary>
	Ignored
}

/// <summary>
/// Describes the requested launch-time state of one signal.
/// </summary>
public sealed class ProcessSignalLaunchDirective {
	/// <summary>
	/// Gets whether the signal should be blocked or unblocked, or <see langword="null"/>
	/// when its mask state is unchanged.
	/// </summary>
	public bool? Blocked {
		get;
		internal set;
	}

	/// <summary>
	/// Gets the requested disposition, or <see langword="null"/> when the inherited
	/// disposition is unchanged.
	/// </summary>
	public ProcessSignalLaunchDisposition? Disposition {
		get;
		internal set;
	}

	/// <summary>Gets whether an unsupported disposition change may be ignored.</summary>
	public bool IgnoreDispositionErrors {
		get;
		internal set;
	}

	/// <summary>Gets the positive native signal number.</summary>
	public int SignalNumber {
		get;
	}

	/// <summary>Initializes a launch directive for one signal.</summary>
	/// <param name="signalNumber">The positive native signal number.</param>
	internal ProcessSignalLaunchDirective(
		int signalNumber
	) {
		if ( 0 >= signalNumber ) {
			throw new ArgumentOutOfRangeException( nameof( signalNumber ) );
		}
		this.SignalNumber = signalNumber;
	}
}

/// <summary>
/// Collects launch-time signal disposition and mask changes without changing the calling process permanently.
/// </summary>
public sealed class ProcessLaunchSignalPolicy {
	private readonly Dictionary<int, ProcessSignalLaunchDirective> _directives = new();

	/// <summary>Gets the configured directives keyed by native signal number.</summary>
	public IReadOnlyDictionary<int, ProcessSignalLaunchDirective> Directives =>
		new ReadOnlyDictionary<int, ProcessSignalLaunchDirective>( this._directives );

	/// <summary>Gets whether no launch-time signal changes are requested.</summary>
	public bool IsEmpty => 0 == this._directives.Count;

	/// <summary>Requests a default or ignored disposition for a signal.</summary>
	/// <param name="signal">The signal whose child disposition is changed.</param>
	/// <param name="disposition">The requested disposition.</param>
	/// <param name="ignoreErrors">Whether an unsupported disposition change may be ignored.</param>
	public void SetDisposition(
		ProcessSignal signal,
		ProcessSignalLaunchDisposition disposition,
		bool ignoreErrors = false
	) {
		ArgumentNullException.ThrowIfNull( signal );
		if ( 0 >= signal.Number ) {
			throw new ArgumentOutOfRangeException( nameof( signal ), "Signal zero has no disposition." );
		}
		if ( !Enum.IsDefined( typeof( ProcessSignalLaunchDisposition ), disposition ) ) {
			throw new ArgumentOutOfRangeException( nameof( disposition ) );
		}
		var directive = this.GetOrCreate( signal.Number );
		directive.Disposition = disposition;
		directive.IgnoreDispositionErrors = ignoreErrors;
	}

	/// <summary>Requests that a signal be blocked or unblocked in the newly launched child.</summary>
	/// <param name="signal">The signal whose mask state is changed.</param>
	/// <param name="blocked"><see langword="true"/> to block the signal; <see langword="false"/> to unblock it.</param>
	public void SetBlocked(
		ProcessSignal signal,
		bool blocked
	) {
		ArgumentNullException.ThrowIfNull( signal );
		if ( 0 >= signal.Number ) {
			throw new ArgumentOutOfRangeException( nameof( signal ), "Signal zero cannot be blocked." );
		}
		this.GetOrCreate( signal.Number ).Blocked = blocked;
	}

	private ProcessSignalLaunchDirective GetOrCreate(
		int signalNumber
	) {
		if ( !this._directives.TryGetValue( signalNumber, out var directive ) ) {
			directive = new ProcessSignalLaunchDirective( signalNumber );
			this._directives.Add( signalNumber, directive );
		}
		return directive;
	}
}
