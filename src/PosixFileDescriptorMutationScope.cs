namespace Icod.Processes;

using System.Runtime.InteropServices;

/// <summary>
/// Applies reversible POSIX descriptor mutations for current-process replacement and restores them when exec fails.
/// </summary>
internal sealed class PosixFileDescriptorMutationScope : IDisposable {
	private readonly List<PreservedDescriptor> _preserved = [];
	private bool _disposed;

	/// <summary>Applies the requested descriptor state and returns a reversible scope.</summary>
	internal static PosixFileDescriptorMutationScope Enter(
		IList<PosixFileDescriptorDuplication> duplications,
		bool unreadableStandardInput = false
	) {
		ArgumentNullException.ThrowIfNull( duplications );
		var scope = new PosixFileDescriptorMutationScope();
		if ( 0 == duplications.Count && !unreadableStandardInput ) {
			return scope;
		}
		if ( OperatingSystem.IsWindows() ) {
			throw new PlatformNotSupportedException(
				"POSIX descriptor mutation is unavailable on Windows."
			);
		}
		try {
			scope.PreserveDescriptors(
				duplications,
				unreadableStandardInput
			);
			if ( unreadableStandardInput ) {
				scope.ApplyUnreadableStandardInput();
			}
			scope.Apply(
				duplications
			);
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
		for ( var index = this._preserved.Count - 1; 0 <= index; index-- ) {
			var preserved = this._preserved[ index ];
			if ( preserved.WasOpen ) {
				_ = ProcessNative.Dup2(
					preserved.BackupDescriptor,
					preserved.Descriptor
				);
				_ = ProcessNative.Close(
					preserved.BackupDescriptor
				);
			} else {
				_ = ProcessNative.Close(
					preserved.Descriptor
				);
			}
		}
		this._preserved.Clear();
	}

	private void PreserveDescriptors(
		IList<PosixFileDescriptorDuplication> duplications,
		bool unreadableStandardInput
	) {
		var descriptorSet = new HashSet<int>();
		foreach ( var duplication in duplications ) {
			descriptorSet.Add(
				duplication.SourceDescriptor
			);
			descriptorSet.Add(
				duplication.DestinationDescriptor
			);
		}
		if ( unreadableStandardInput ) {
			descriptorSet.Add( 0 );
		}
		var descriptors = descriptorSet
			.OrderBy(
				static descriptor => descriptor
			)
			.ToArray();
		if ( 0 == descriptors.Length ) {
			return;
		}
		if ( int.MaxValue == descriptors[ ^1 ] ) {
			throw new ArgumentOutOfRangeException(
				nameof( duplications ),
				"Descriptor values leave no room for temporary preservation handles."
			);
		}
		var minimumBackupDescriptor = descriptors[ ^1 ] + 1;
		foreach ( var descriptor in descriptors ) {
			int backup;
			int duplicateError;
			if ( OperatingSystem.IsMacOS() ) {
				backup = DuplicateDescriptorOutsideSet(
					descriptor,
					descriptorSet,
					out duplicateError
				);
			} else {
				backup = ProcessNative.Fcntl(
					descriptor,
					ProcessNative.DuplicateFileDescriptor,
					minimumBackupDescriptor
				);
				duplicateError = 0 > backup
					? Marshal.GetLastPInvokeError()
					: 0
				;
			}
			if ( 0 > backup ) {
				if ( ProcessNative.BadFileDescriptor == duplicateError ) {
					this._preserved.Add(
						new PreservedDescriptor(
							descriptor,
							-1,
							false
						)
					);
					continue;
				}
				throw new InvalidOperationException(
					$"Unable to preserve file descriptor {descriptor} (errno {duplicateError})."
				);
			}
			var closeOnExecResult = OperatingSystem.IsMacOS()
				? ProcessNative.Ioctl(
					backup,
					ProcessNative.DarwinFileIoCloseOnExec
				)
				: ProcessNative.Fcntl(
					backup,
					ProcessNative.SetFileDescriptorFlags,
					ProcessNative.CloseOnExec
				)
			;
			if ( 0 > closeOnExecResult ) {
				var error = Marshal.GetLastPInvokeError();
				_ = ProcessNative.Close(
					backup
				);
				throw new InvalidOperationException(
					$"Unable to protect preserved file descriptor {descriptor} from exec inheritance (errno {error})."
				);
			}
			this._preserved.Add(
				new PreservedDescriptor(
					descriptor,
					backup,
					true
				)
			);
			if ( int.MaxValue == backup ) {
				throw new InvalidOperationException(
					"No descriptor number remains for launch-state preservation."
				);
			}
			minimumBackupDescriptor = Math.Max(
				minimumBackupDescriptor,
				backup + 1
			);
		}
	}

	private static int DuplicateDescriptorOutsideSet(
		int descriptor,
		HashSet<int> descriptorSet,
		out int error
	) {
		var reservations = new List<int>();
		try {
			while ( true ) {
				var duplicate = ProcessNative.Dup(
					descriptor
				);
				if ( 0 > duplicate ) {
					error = Marshal.GetLastPInvokeError();
					return duplicate;
				}
				if ( !descriptorSet.Contains( duplicate ) ) {
					error = 0;
					return duplicate;
				}
				reservations.Add(
					duplicate
				);
			}
		} finally {
			foreach ( var reservation in reservations ) {
				_ = ProcessNative.Close(
					reservation
				);
			}
		}
	}

	private void ApplyUnreadableStandardInput() {
		var nullDescriptor = ProcessNative.Open(
			"/dev/null",
			ProcessNative.OpenWriteOnly
		);
		if ( 0 > nullDescriptor ) {
			var error = Marshal.GetLastPInvokeError();
			throw new InvalidOperationException(
				$"Unable to open /dev/null for replacement standard input (errno {error})."
			);
		}
		if ( 0 == nullDescriptor ) {
			return;
		}
		try {
			if ( 0 > ProcessNative.Dup2(
				nullDescriptor,
				0
			) ) {
				var error = Marshal.GetLastPInvokeError();
				throw new InvalidOperationException(
					$"Unable to replace standard input before exec (errno {error})."
				);
			}
		} finally {
			_ = ProcessNative.Close(
				nullDescriptor
			);
		}
	}

	private void Apply(
		IList<PosixFileDescriptorDuplication> duplications
	) {
		foreach ( var duplication in duplications ) {
			if ( 0 > ProcessNative.Dup2(
				duplication.SourceDescriptor,
				duplication.DestinationDescriptor
			) ) {
				var error = Marshal.GetLastPInvokeError();
				throw new InvalidOperationException(
					$"Unable to duplicate file descriptor {duplication.SourceDescriptor} onto {duplication.DestinationDescriptor} (errno {error})."
				);
			}
			if ( duplication.CloseSource
				&& duplication.SourceDescriptor != duplication.DestinationDescriptor
				&& 0 != ProcessNative.Close(
					duplication.SourceDescriptor
				)
			) {
				var error = Marshal.GetLastPInvokeError();
				throw new InvalidOperationException(
					$"Unable to close duplicated source descriptor {duplication.SourceDescriptor} (errno {error})."
				);
			}
		}
	}

	private readonly record struct PreservedDescriptor(
		int Descriptor,
		int BackupDescriptor,
		bool WasOpen
	);
}
