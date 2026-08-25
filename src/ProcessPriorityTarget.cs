namespace Icod.Processes;

/// <summary>
/// Identifies the kind of target accepted by POSIX-style priority operations.
/// </summary>
public enum ProcessPriorityTargetKind {
	/// <summary>A process identifier, where zero denotes the calling process.</summary>
	Process,
	/// <summary>A process-group identifier, where zero denotes the calling process group.</summary>
	ProcessGroup,
	/// <summary>A user identifier, where zero denotes the calling process's real user ID on POSIX.</summary>
	User
}

/// <summary>
/// Models the nonnegative selector used by <c>getpriority</c> and <c>setpriority</c>
/// without leaking native selector constants into commands.
/// </summary>
public sealed class ProcessPriorityTarget {
	/// <summary>Gets a protected process identity when one is available.</summary>
	public ProcessIdentity? Identity {
		get;
	}

	/// <summary>Gets the nonnegative process, process-group, or user identifier.</summary>
	public int Identifier {
		get;
	}

	/// <summary>Gets the priority-target kind.</summary>
	public ProcessPriorityTargetKind Kind {
		get;
	}

	/// <summary>Creates a process-priority target.</summary>
	public static ProcessPriorityTarget ForProcess(
		int processId
	) => new(
		ProcessPriorityTargetKind.Process,
		ValidateIdentifier( processId, nameof( processId ) ),
		null
	);

	/// <summary>Creates a PID-reuse-protected process-priority target.</summary>
	public static ProcessPriorityTarget ForProcess(
		ProcessIdentity identity
	) {
		ArgumentNullException.ThrowIfNull( identity );
		return new ProcessPriorityTarget(
			ProcessPriorityTargetKind.Process,
			identity.ProcessId,
			identity
		);
	}

	/// <summary>Creates a process-group-priority target.</summary>
	public static ProcessPriorityTarget ForProcessGroup(
		int processGroupId
	) => new(
		ProcessPriorityTargetKind.ProcessGroup,
		ValidateIdentifier( processGroupId, nameof( processGroupId ) ),
		null
	);

	/// <summary>Creates a user-priority target.</summary>
	public static ProcessPriorityTarget ForUser(
		int userId
	) => new(
		ProcessPriorityTargetKind.User,
		ValidateIdentifier( userId, nameof( userId ) ),
		null
	);

	private ProcessPriorityTarget(
		ProcessPriorityTargetKind kind,
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
		if ( 0 > value ) {
			throw new ArgumentOutOfRangeException( parameterName );
		}
		return value;
	}
}
