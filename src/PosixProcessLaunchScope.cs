namespace Icod.Processes;

using System.Runtime.InteropServices;

/// <summary>
/// Applies launch-only POSIX signal state and restores the managed host immediately after process creation.
/// </summary>
internal sealed class PosixProcessLaunchScope : IDisposable {
	private const int SignalSetStorageSize = 128;
	private static readonly object SyncRoot = new();
	private readonly List<KeyValuePair<int, IntPtr>> _previousHandlers = [];
	private IntPtr _previousMask;
	private int _previousStandardInput = -1;
	private bool _disposed;
	private bool _lockHeld;

	/// <summary>Enters a launch scope for the requested signal policy.</summary>
	internal static PosixProcessLaunchScope Enter(
		ProcessLaunchSignalPolicy? policy,
		bool unreadableStandardInput = false
	) {
		var scope = new PosixProcessLaunchScope();
		if ( ( null == policy || policy.IsEmpty ) && !unreadableStandardInput ) {
			return scope;
		}
		if ( OperatingSystem.IsWindows() ) {
			throw new PlatformNotSupportedException(
				"Launch-time POSIX signal and descriptor policy is not available on Windows."
			);
		}
		Monitor.Enter( SyncRoot );
		scope._lockHeld = true;
		try {
			if ( unreadableStandardInput ) {
				scope.ApplyUnreadableStandardInput();
			}
			if ( null != policy && !policy.IsEmpty ) {
				scope.Apply( policy );
			}
			return scope;
		} catch {
			scope.Dispose();
			throw;
		}
	}

	/// <inheritdoc />
	public void Dispose() {
		if ( this._disposed ) {
			return;
		}
		this._disposed = true;
		try {
			if ( 0 <= this._previousStandardInput ) {
				_ = ProcessNative.Dup2( this._previousStandardInput, 0 );
				_ = ProcessNative.Close( this._previousStandardInput );
				this._previousStandardInput = -1;
			}
			for ( var index = this._previousHandlers.Count - 1; 0 <= index; index-- ) {
				var previous = this._previousHandlers[ index ];
				_ = ProcessNative.Signal( previous.Key, previous.Value );
			}
			if ( IntPtr.Zero != this._previousMask ) {
				_ = ProcessNative.PThreadSignalMask(
					GetSetMaskOperation(),
					this._previousMask,
					IntPtr.Zero
				);
				Marshal.FreeHGlobal( this._previousMask );
				this._previousMask = IntPtr.Zero;
			}
		} finally {
			if ( this._lockHeld ) {
				this._lockHeld = false;
				Monitor.Exit( SyncRoot );
			}
		}
	}

	private void ApplyUnreadableStandardInput() {
		this._previousStandardInput = ProcessNative.Dup( 0 );
		if ( 0 > this._previousStandardInput ) {
			var error = Marshal.GetLastPInvokeError();
			throw new InvalidOperationException(
				$"Unable to preserve standard input for child launch (errno {error})."
			);
		}
		var nullDescriptor = ProcessNative.Open( "/dev/null", ProcessNative.OpenWriteOnly );
		if ( 0 > nullDescriptor ) {
			var error = Marshal.GetLastPInvokeError();
			throw new InvalidOperationException(
				$"Unable to open /dev/null for child standard input (errno {error})."
			);
		}
		try {
			if ( 0 > ProcessNative.Dup2( nullDescriptor, 0 ) ) {
				var error = Marshal.GetLastPInvokeError();
				throw new InvalidOperationException(
					$"Unable to replace child standard input for launch (errno {error})."
				);
			}
		} finally {
			_ = ProcessNative.Close( nullDescriptor );
		}
	}

	private void Apply(
		ProcessLaunchSignalPolicy policy
	) {
		var changesMask = policy.Directives.Values.Any( static directive => null != directive.Blocked );
		if ( changesMask ) {
			this.ApplySignalMask( policy );
		}
		foreach ( var directive in policy.Directives.Values.OrderBy( static directive => directive.SignalNumber ) ) {
			if ( null == directive.Disposition ) {
				continue;
			}
			var handler = ProcessSignalLaunchDisposition.Default == directive.Disposition
				? IntPtr.Zero
				: new IntPtr( 1 )
			;
			var previous = ProcessNative.Signal( directive.SignalNumber, handler );
			if ( ProcessNative.SignalError == previous ) {
				if ( directive.IgnoreDispositionErrors ) {
					continue;
				}
				var error = Marshal.GetLastPInvokeError();
				throw new InvalidOperationException(
					$"Unable to set launch disposition for signal {directive.SignalNumber} (errno {error})."
				);
			}
			this._previousHandlers.Add(
				new KeyValuePair<int, IntPtr>( directive.SignalNumber, previous )
			);
		}
	}

	private void ApplySignalMask(
		ProcessLaunchSignalPolicy policy
	) {
		this._previousMask = Marshal.AllocHGlobal( SignalSetStorageSize );
		var nextMask = Marshal.AllocHGlobal( SignalSetStorageSize );
		try {
			ZeroMemory( this._previousMask );
			ZeroMemory( nextMask );
			var getResult = ProcessNative.PThreadSignalMask(
				GetSetMaskOperation(),
				IntPtr.Zero,
				this._previousMask
			);
			if ( 0 != getResult ) {
				Marshal.FreeHGlobal( this._previousMask );
				this._previousMask = IntPtr.Zero;
				throw new InvalidOperationException(
					$"Unable to read the launch signal mask (error {getResult})."
				);
			}
			var bytes = new byte[ SignalSetStorageSize ];
			Marshal.Copy( this._previousMask, bytes, 0, bytes.Length );
			Marshal.Copy( bytes, 0, nextMask, bytes.Length );
			foreach ( var directive in policy.Directives.Values ) {
				if ( null == directive.Blocked ) {
					continue;
				}
				var result = directive.Blocked.Value
					? ProcessNative.SigAddSet( nextMask, directive.SignalNumber )
					: ProcessNative.SigDeleteSet( nextMask, directive.SignalNumber )
				;
				if ( 0 != result ) {
					var error = Marshal.GetLastPInvokeError();
					throw new InvalidOperationException(
						$"Unable to change launch mask state for signal {directive.SignalNumber} (errno {error})."
					);
				}
			}
			var setResult = ProcessNative.PThreadSignalMask(
				GetSetMaskOperation(),
				nextMask,
				IntPtr.Zero
			);
			if ( 0 != setResult ) {
				throw new InvalidOperationException(
					$"Unable to set the launch signal mask (error {setResult})."
				);
			}
		} finally {
			Marshal.FreeHGlobal( nextMask );
		}
	}

	private static int GetSetMaskOperation() => OperatingSystem.IsMacOS() ? 3 : 2;

	private static void ZeroMemory(
		IntPtr buffer
	) {
		var bytes = new byte[ SignalSetStorageSize ];
		Marshal.Copy( bytes, 0, buffer, bytes.Length );
	}
}
