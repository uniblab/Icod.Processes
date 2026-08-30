namespace Icod.Processes;

using System.Runtime.InteropServices;

/// <summary>
/// Contains isolated POSIX process-control entry points used by system providers.
/// </summary>
internal static class ProcessNative {
	/// <summary>Gets the POSIX permission-denied error number.</summary>
	internal const int PermissionDenied = 1;
	/// <summary>Gets the POSIX interrupted-system-call error number.</summary>
	internal const int Interrupted = 4;
	/// <summary>Gets the POSIX no-such-process error number.</summary>
	internal const int NoSuchProcess = 3;
	/// <summary>Gets the POSIX no-such-file error number.</summary>
	internal const int NoSuchFile = 2;
	/// <summary>Gets the POSIX executable-format error number.</summary>
	internal const int ExecFormatError = 8;
	/// <summary>Gets the POSIX bad-file-descriptor error number.</summary>
	internal const int BadFileDescriptor = 9;
	/// <summary>Gets the POSIX access-denied error number.</summary>
	internal const int AccessDenied = 13;
	/// <summary>Gets the POSIX invalid-argument error number.</summary>
	internal const int InvalidArgument = 22;
	/// <summary>Gets the waitpid nonblocking option.</summary>
	internal const int WaitNoHang = 1;
	/// <summary>Gets the native signal-handler error sentinel.</summary>
	internal static IntPtr SignalError => new( -1 );

	/// <summary>Gets the POSIX write-only open flag.</summary>
	internal const int OpenWriteOnly = 1;
	/// <summary>Gets the POSIX fcntl command that duplicates a descriptor at or above a minimum value.</summary>
	internal const int DuplicateFileDescriptor = 0;
	/// <summary>Gets the POSIX fcntl command that sets descriptor flags.</summary>
	internal const int SetFileDescriptorFlags = 2;
	/// <summary>Gets the POSIX close-on-exec descriptor flag.</summary>
	internal const int CloseOnExec = 1;
	/// <summary>Gets Darwin's no-argument <c>FIOCLEX</c> request for marking a descriptor close-on-exec.</summary>
	internal static nuint DarwinFileIoCloseOnExec => 0x20006601u;

	/// <summary>Represents the POSIX <c>union sigval</c> used by <c>sigqueue(3)</c>.</summary>
	[StructLayout( LayoutKind.Explicit )]
	internal struct SignalValue {
		/// <summary>Stores the queued integer member of the native union.</summary>
		[FieldOffset( 0 )]
		internal int Integer;
		/// <summary>Reserves pointer-sized storage for the native union on the current architecture.</summary>
		[FieldOffset( 0 )]
		internal IntPtr Pointer;

		/// <summary>Initializes a queued integer value while clearing the full native union storage.</summary>
		internal SignalValue(
			int integer
		) {
			this.Pointer = IntPtr.Zero;
			this.Integer = integer;
		}
	}

	/// <summary>Duplicates one open POSIX file descriptor.</summary>
	[DllImport(
		"libc",
		EntryPoint = "dup",
		SetLastError = true
	)]
	internal static extern int Dup(
		int descriptor
	);

	/// <summary>Duplicates one POSIX descriptor onto another descriptor number.</summary>
	[DllImport(
		"libc",
		EntryPoint = "dup2",
		SetLastError = true
	)]
	internal static extern int Dup2(
		int sourceDescriptor,
		int destinationDescriptor
	);

	/// <summary>Opens a POSIX pathname without creation flags.</summary>
	[DllImport(
		"libc",
		EntryPoint = "open",
		SetLastError = true
	)]
	internal static extern int Open(
		string path,
		int flags
	);

	/// <summary>Closes one POSIX file descriptor.</summary>
	[DllImport(
		"libc",
		EntryPoint = "close",
		SetLastError = true
	)]
	internal static extern int Close(
		int descriptor
	);

	/// <summary>Reads or changes one POSIX file descriptor through <c>fcntl(2)</c>.</summary>
	[DllImport(
		"libc",
		EntryPoint = "fcntl",
		SetLastError = true
	)]
	internal static extern int Fcntl(
		int descriptor,
		int command,
		int argument
	);

	/// <summary>Invokes a no-argument POSIX file-descriptor ioctl request.</summary>
	[DllImport(
		"libc",
		EntryPoint = "ioctl",
		SetLastError = true
	)]
	internal static extern int Ioctl(
		int descriptor,
		nuint request
	);

	/// <summary>Invokes POSIX kill.</summary>
	[DllImport(
		"libc",
		EntryPoint = "kill",
		SetLastError = true
	)]
	internal static extern int Kill(
		int processId,
		int signal
	);

	/// <summary>Invokes POSIX <c>sigqueue(3)</c> for one process.</summary>
	[DllImport(
		"libc",
		EntryPoint = "sigqueue",
		SetLastError = true
	)]
	internal static extern int SigQueue(
		int processId,
		int signal,
		SignalValue value
	);

	/// <summary>Invokes POSIX getpriority.</summary>
	[DllImport(
		"libc",
		EntryPoint = "getpriority",
		SetLastError = true
	)]
	internal static extern int GetPriority(
		int which,
		int who
	);

	/// <summary>Invokes POSIX setpriority.</summary>
	[DllImport(
		"libc",
		EntryPoint = "setpriority",
		SetLastError = true
	)]
	internal static extern int SetPriority(
		int which,
		int who,
		int priority
	);

	/// <summary>Sets one POSIX signal disposition and returns the previous handler.</summary>
	[DllImport(
		"libc",
		EntryPoint = "signal",
		SetLastError = true
	)]
	internal static extern IntPtr Signal(
		int signal,
		IntPtr handler
	);

	/// <summary>Initializes an empty POSIX signal set.</summary>
	[DllImport(
		"libc",
		EntryPoint = "sigemptyset",
		SetLastError = true
	)]
	internal static extern int SigEmptySet(
		IntPtr set
	);

	/// <summary>Adds one signal to a POSIX signal set.</summary>
	[DllImport(
		"libc",
		EntryPoint = "sigaddset",
		SetLastError = true
	)]
	internal static extern int SigAddSet(
		IntPtr set,
		int signal
	);

	/// <summary>Removes one signal from a POSIX signal set.</summary>
	[DllImport(
		"libc",
		EntryPoint = "sigdelset",
		SetLastError = true
	)]
	internal static extern int SigDeleteSet(
		IntPtr set,
		int signal
	);

	/// <summary>Reads or changes the calling thread's POSIX signal mask.</summary>
	[DllImport(
		"libc",
		EntryPoint = "pthread_sigmask",
		SetLastError = false
	)]
	internal static extern int PThreadSignalMask(
		int how,
		IntPtr set,
		IntPtr oldSet
	);

	/// <summary>Gets the POSIX spawn flag that assigns the child process group.</summary>
	internal const short PosixSpawnSetProcessGroup = 0x0002;

	/// <summary>Initializes one opaque POSIX spawn file-actions object.</summary>
	[DllImport(
		"libc",
		EntryPoint = "posix_spawn_file_actions_init",
		SetLastError = false
	)]
	internal static extern int PosixSpawnFileActionsInit(
		IntPtr fileActions
	);

	/// <summary>Destroys one initialized POSIX spawn file-actions object.</summary>
	[DllImport(
		"libc",
		EntryPoint = "posix_spawn_file_actions_destroy",
		SetLastError = false
	)]
	internal static extern int PosixSpawnFileActionsDestroy(
		IntPtr fileActions
	);

	/// <summary>Adds one ordered <c>dup2</c> operation to POSIX spawn file actions.</summary>
	[DllImport(
		"libc",
		EntryPoint = "posix_spawn_file_actions_adddup2",
		SetLastError = false
	)]
	internal static extern int PosixSpawnFileActionsAddDup2(
		IntPtr fileActions,
		int sourceDescriptor,
		int destinationDescriptor
	);

	/// <summary>Adds one ordered descriptor close to POSIX spawn file actions.</summary>
	[DllImport(
		"libc",
		EntryPoint = "posix_spawn_file_actions_addclose",
		SetLastError = false
	)]
	internal static extern int PosixSpawnFileActionsAddClose(
		IntPtr fileActions,
		int descriptor
	);

	/// <summary>Initializes one opaque POSIX spawn-attribute object.</summary>
	[DllImport(
		"libc",
		EntryPoint = "posix_spawnattr_init",
		SetLastError = false
	)]
	internal static extern int PosixSpawnAttributeInit(
		IntPtr attributes
	);

	/// <summary>Destroys one initialized POSIX spawn-attribute object.</summary>
	[DllImport(
		"libc",
		EntryPoint = "posix_spawnattr_destroy",
		SetLastError = false
	)]
	internal static extern int PosixSpawnAttributeDestroy(
		IntPtr attributes
	);

	/// <summary>Sets POSIX spawn-attribute flags.</summary>
	[DllImport(
		"libc",
		EntryPoint = "posix_spawnattr_setflags",
		SetLastError = false
	)]
	internal static extern int PosixSpawnAttributeSetFlags(
		IntPtr attributes,
		short flags
	);

	/// <summary>Sets the child process group requested by POSIX spawn attributes.</summary>
	[DllImport(
		"libc",
		EntryPoint = "posix_spawnattr_setpgroup",
		SetLastError = false
	)]
	internal static extern int PosixSpawnAttributeSetProcessGroup(
		IntPtr attributes,
		int processGroupId
	);

	/// <summary>Creates a POSIX child process using exact argument and environment vectors.</summary>
	[DllImport(
		"libc",
		EntryPoint = "posix_spawn",
		SetLastError = false
	)]
	internal static extern int PosixSpawn(
		out int processId,
		IntPtr path,
		IntPtr fileActions,
		IntPtr attributes,
		IntPtr arguments,
		IntPtr environment
	);

	/// <summary>Replaces the current POSIX process image using exact argument and environment vectors.</summary>
	[DllImport(
		"libc",
		EntryPoint = "execve",
		SetLastError = true
	)]
	internal static extern int ExecVe(
		IntPtr path,
		IntPtr arguments,
		IntPtr environment
	);

	/// <summary>Waits for or polls one POSIX child process.</summary>
	[DllImport(
		"libc",
		EntryPoint = "waitpid",
		SetLastError = true
	)]
	internal static extern int WaitPid(
		int processId,
		out int status,
		int options
	);

	/// <summary>Maps a POSIX error number to a controlled process-operation status.</summary>
	internal static ProcessOperationStatus MapErrno(
		int error
	) => error switch {
		NoSuchProcess or NoSuchFile => ProcessOperationStatus.Vanished,
		PermissionDenied or AccessDenied => ProcessOperationStatus.AccessDenied,
		InvalidArgument => ProcessOperationStatus.InvalidArgument,
		_ => ProcessOperationStatus.Failed
	};
}
