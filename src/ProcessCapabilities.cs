namespace Icod.Processes;
/// <summary>
/// Identifies process-control capabilities exposed by a provider on the current host.
/// </summary>
[Flags]
public enum ProcessControlCapabilities {
	/// <summary>No optional capabilities are available.</summary>
	None = 0,
	/// <summary>Process identity observation is available.</summary>
	ProcessIdentity = 1 << 0,
	/// <summary>PID-reuse tokens can be observed.</summary>
	ReuseToken = 1 << 1,
	/// <summary>Process liveness can be observed.</summary>
	Liveness = 1 << 2,
	/// <summary>Arbitrary processes can be awaited.</summary>
	ArbitraryProcessWait = 1 << 3,
	/// <summary>Process-group targets are supported.</summary>
	ProcessGroupTargets = 1 << 4,
	/// <summary>Session targets are supported.</summary>
	SessionTargets = 1 << 5,
	/// <summary>Signals can be delivered.</summary>
	SignalDelivery = 1 << 6,
	/// <summary>Signal dispositions can be observed.</summary>
	SignalDisposition = 1 << 7,
	/// <summary>Priorities can be read.</summary>
	PriorityRead = 1 << 8,
	/// <summary>Priorities can be changed.</summary>
	PriorityWrite = 1 << 9,
	/// <summary>Windows termination substitutions are available.</summary>
	WindowsTerminationSubstitution = 1 << 10,
	/// <summary>Windows priority-class substitutions are available.</summary>
	WindowsPrioritySubstitution = 1 << 11,
	/// <summary>Blocked signal masks can be observed.</summary>
	SignalMaskObservation = 1 << 12,
	/// <summary>User-ID priority selectors are supported.</summary>
	UserPriorityTargets = 1 << 13,
	/// <summary>Queued signal values can be delivered to individual processes.</summary>
	QueuedSignalDelivery = 1 << 14
}
