namespace Icod.Processes;

/// <summary>
/// Describes one ordered POSIX child file-descriptor duplication applied atomically at spawn time.
/// </summary>
/// <remarks>
/// The source descriptor is observed in the child launch context. When <see cref="CloseSource"/> is
/// requested, the source is closed immediately after this duplication unless source and destination
/// are the same descriptor. Actions execute in list order, so a later action may duplicate a
/// destination established by an earlier action.
/// </remarks>
public readonly record struct PosixFileDescriptorDuplication {
	/// <summary>Gets the descriptor copied by <c>dup2</c>.</summary>
	public int SourceDescriptor { get; }

	/// <summary>Gets the child descriptor replaced by <c>dup2</c>.</summary>
	public int DestinationDescriptor { get; }

	/// <summary>Gets whether the source descriptor is closed in the child after duplication.</summary>
	public bool CloseSource { get; }

	/// <summary>Initializes one ordered POSIX descriptor duplication.</summary>
	public PosixFileDescriptorDuplication(
		int sourceDescriptor,
		int destinationDescriptor,
		bool closeSource = false
	) {
		if ( 0 > sourceDescriptor ) {
			throw new ArgumentOutOfRangeException(
				nameof( sourceDescriptor )
			);
		}
		if ( 0 > destinationDescriptor ) {
			throw new ArgumentOutOfRangeException(
				nameof( destinationDescriptor )
			);
		}
		this.SourceDescriptor = sourceDescriptor;
		this.DestinationDescriptor = destinationDescriptor;
		this.CloseSource = closeSource;
	}
}
