namespace Icod.Processes;

/// <summary>
/// Identifies the kind of operating-system process target.
/// </summary>
public enum ProcessTargetKind {
	/// <summary>A single process.</summary>
	Process,
	/// <summary>A POSIX process group.</summary>
	ProcessGroup,
	/// <summary>A POSIX session.</summary>
	Session
}

/// <summary>
/// Models a process, process group, or session target without overloading integer sign conventions.
/// </summary>
public sealed class ProcessTarget {
	/// <summary>Gets the process identity when the target is a protected single process.</summary>
	public ProcessIdentity? Identity {
		get;
	}

	/// <summary>Gets the native process, group, or session identifier.</summary>
	public int Identifier {
		get;
	}

	/// <summary>Gets the target kind.</summary>
	public ProcessTargetKind Kind {
		get;
	}

	/// <summary>Creates a single-process target.</summary>
	public static ProcessTarget ForProcess(
		int processId
	) => ForProcess(
		new ProcessIdentity(
			processId
		)
	);

	/// <summary>Creates a PID-reuse-protected single-process target.</summary>
	public static ProcessTarget ForProcess(
		ProcessIdentity identity
	) {
		ArgumentNullException.ThrowIfNull(
			identity
		);
		return new ProcessTarget(
			ProcessTargetKind.Process,
			identity.ProcessId,
			identity
		);
	}

	/// <summary>Creates a process-group target.</summary>
	public static ProcessTarget ForProcessGroup(
		int processGroupId
	) => new(
		ProcessTargetKind.ProcessGroup,
		ValidateIdentifier(
			processGroupId,
			nameof( processGroupId )
		),
		null
	);

	/// <summary>Creates a session target.</summary>
	public static ProcessTarget ForSession(
		int sessionId
	) => new(
		ProcessTargetKind.Session,
		ValidateIdentifier(
			sessionId,
			nameof( sessionId )
		),
		null
	);

	private ProcessTarget(
		ProcessTargetKind kind,
		int identifier,
		ProcessIdentity? identity
	) {
		this.Kind = kind;
		this.Identifier = identifier;
		this.Identity = identity;
	}

	private static int ValidateIdentifier(
		int value,
		string parameterName
	) {
		if ( 0 >= value ) {
			throw new ArgumentOutOfRangeException(
				parameterName
			);
		}
		return value;
	}
}
