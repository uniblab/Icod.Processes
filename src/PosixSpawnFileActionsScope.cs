namespace Icod.Processes;

using System.Runtime.InteropServices;

/// <summary>
/// Owns the opaque POSIX spawn file-actions object used for child-only descriptor duplication.
/// </summary>
internal sealed class PosixSpawnFileActionsScope : IDisposable {
	// libc keeps posix_spawn_file_actions_t opaque. Reserve generous storage rather
	// than duplicating a private glibc or Darwin structure layout in managed code.
	private const int FileActionsStorageSize = 1024;
	private bool _initialized;

	/// <summary>Gets the native file-actions pointer, or zero when no actions are requested.</summary>
	internal IntPtr Pointer {
		get;
		private set;
	}

	/// <summary>Creates ordered POSIX spawn file actions.</summary>
	internal PosixSpawnFileActionsScope(
		IList<PosixFileDescriptorDuplication> duplications
	) {
		ArgumentNullException.ThrowIfNull( duplications );
		if ( 0 == duplications.Count ) {
			return;
		}

		this.Pointer = Marshal.AllocHGlobal( FileActionsStorageSize );
		try {
			Marshal.Copy(
				new byte[ FileActionsStorageSize ],
				0,
				this.Pointer,
				FileActionsStorageSize
			);
			var result = ProcessNative.PosixSpawnFileActionsInit( this.Pointer );
			if ( 0 != result ) {
				throw new InvalidOperationException(
					$"posix_spawn_file_actions_init failed with error {result}."
				);
			}
			this._initialized = true;

			foreach ( var duplication in duplications ) {
				result = ProcessNative.PosixSpawnFileActionsAddDup2(
					this.Pointer,
					duplication.SourceDescriptor,
					duplication.DestinationDescriptor
				);
				if ( 0 != result ) {
					throw new InvalidOperationException(
						$"posix_spawn_file_actions_adddup2 failed with error {result}."
					);
				}
				if ( duplication.CloseSource
					&& duplication.SourceDescriptor != duplication.DestinationDescriptor
				) {
					result = ProcessNative.PosixSpawnFileActionsAddClose(
						this.Pointer,
						duplication.SourceDescriptor
					);
					if ( 0 != result ) {
						throw new InvalidOperationException(
							$"posix_spawn_file_actions_addclose failed with error {result}."
						);
					}
				}
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
			_ = ProcessNative.PosixSpawnFileActionsDestroy( this.Pointer );
		}
		Marshal.FreeHGlobal( this.Pointer );
		this.Pointer = IntPtr.Zero;
		this._initialized = false;
	}
}
