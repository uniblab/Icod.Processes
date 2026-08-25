namespace Icod.Processes;

using System.Runtime.InteropServices;

/// <summary>
/// Owns the opaque storage used by POSIX spawn attributes needed for atomic child process-group creation.
/// </summary>
internal sealed class PosixSpawnAttributeScope : IDisposable {
	// glibc 2.4x uses 336 bytes and Darwin uses substantially less. Keep generous
	// opaque storage so the managed boundary does not duplicate a libc-private layout.
	private const int AttributeStorageSize = 1024;
	private bool _initialized;

	/// <summary>Gets the native attribute pointer, or zero when no attributes are requested.</summary>
	internal IntPtr Pointer {
		get;
		private set;
	}

	/// <summary>Creates spawn attributes for the requested launch policy.</summary>
	internal PosixSpawnAttributeScope(
		bool createProcessGroup
	) {
		if ( !createProcessGroup ) {
			return;
		}
		this.Pointer = Marshal.AllocHGlobal( AttributeStorageSize );
		try {
			Marshal.Copy( new byte[ AttributeStorageSize ], 0, this.Pointer, AttributeStorageSize );
			var result = ProcessNative.PosixSpawnAttributeInit( this.Pointer );
			if ( 0 != result ) {
				throw new InvalidOperationException( $"posix_spawnattr_init failed with error {result}." );
			}
			this._initialized = true;
			result = ProcessNative.PosixSpawnAttributeSetProcessGroup( this.Pointer, 0 );
			if ( 0 != result ) {
				throw new InvalidOperationException( $"posix_spawnattr_setpgroup failed with error {result}." );
			}
			result = ProcessNative.PosixSpawnAttributeSetFlags(
				this.Pointer,
				ProcessNative.PosixSpawnSetProcessGroup
			);
			if ( 0 != result ) {
				throw new InvalidOperationException( $"posix_spawnattr_setflags failed with error {result}." );
			}
		} catch {
			this.Dispose();
			throw;
		}
	}

	/// <inheritdoc />
	public void Dispose() {
		if ( IntPtr.Zero == this.Pointer ) {
			return;
		}
		if ( this._initialized ) {
			_ = ProcessNative.PosixSpawnAttributeDestroy( this.Pointer );
		}
		Marshal.FreeHGlobal( this.Pointer );
		this.Pointer = IntPtr.Zero;
		this._initialized = false;
	}
}
