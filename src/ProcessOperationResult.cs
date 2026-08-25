namespace Icod.Processes;

/// <summary>
/// Describes the outcome of a process-control operation without requiring platform exceptions at command boundaries.
/// </summary>
public enum ProcessOperationStatus {
	/// <summary>The operation completed successfully.</summary>
	Succeeded,
	/// <summary>The target vanished before the operation could complete.</summary>
	Vanished,
	/// <summary>The process identifier now names a different process.</summary>
	Reused,
	/// <summary>The caller does not have permission to perform the operation.</summary>
	AccessDenied,
	/// <summary>The host does not expose the requested capability.</summary>
	Unsupported,
	/// <summary>The supplied request is invalid.</summary>
	InvalidArgument,
	/// <summary>The operation was canceled.</summary>
	Canceled,
	/// <summary>The operation reached its deadline.</summary>
	TimedOut,
	/// <summary>The operation failed for another controlled reason.</summary>
	Failed
}

/// <summary>
/// Contains the controlled outcome of an operation that does not return a value.
/// </summary>
public sealed class ProcessOperationResult {
	/// <summary>Gets an optional native error number.</summary>
	public int? NativeErrorCode {
		get;
	}

	/// <summary>Gets a diagnostic suitable for command-layer translation.</summary>
	public string? Message {
		get;
	}

	/// <summary>Gets whether a platform substitution was used.</summary>
	public bool UsedPlatformSubstitution {
		get;
	}

	/// <summary>Gets the controlled status.</summary>
	public ProcessOperationStatus Status {
		get;
	}

	/// <summary>Gets whether the operation succeeded.</summary>
	public bool Succeeded => ProcessOperationStatus.Succeeded == this.Status;

	/// <summary>Creates a successful result.</summary>
	public static ProcessOperationResult Success(
		string? message = null,
		bool usedPlatformSubstitution = false
	) => new(
		ProcessOperationStatus.Succeeded,
		message,
		null,
		usedPlatformSubstitution
	);

	/// <summary>Creates a controlled unsuccessful result.</summary>
	public static ProcessOperationResult Failure(
		ProcessOperationStatus status,
		string? message = null,
		int? nativeErrorCode = null
	) {
		if ( ProcessOperationStatus.Succeeded == status ) {
			throw new ArgumentOutOfRangeException(
				nameof( status )
			);
		}
		return new ProcessOperationResult(
			status,
			message,
			nativeErrorCode,
			false
		);
	}

	private ProcessOperationResult(
		ProcessOperationStatus status,
		string? message,
		int? nativeErrorCode,
		bool usedPlatformSubstitution
	) {
		this.Status = status;
		this.Message = message;
		this.NativeErrorCode = nativeErrorCode;
		this.UsedPlatformSubstitution = usedPlatformSubstitution;
	}
}

/// <summary>
/// Contains the controlled outcome and value of a process-control operation.
/// </summary>
/// <typeparam name="T">The successful result type.</typeparam>
public sealed class ProcessOperationResult<T> {
	/// <summary>Gets an optional native error number.</summary>
	public int? NativeErrorCode {
		get;
	}

	/// <summary>Gets a diagnostic suitable for command-layer translation.</summary>
	public string? Message {
		get;
	}

	/// <summary>Gets the controlled status.</summary>
	public ProcessOperationStatus Status {
		get;
	}

	/// <summary>Gets whether the operation succeeded.</summary>
	public bool Succeeded => ProcessOperationStatus.Succeeded == this.Status;

	/// <summary>Gets the successful value, when available.</summary>
	public T? Value {
		get;
	}

	/// <summary>Creates a successful result.</summary>
	public static ProcessOperationResult<T> Success(
		T value,
		string? message = null
	) => new(
		ProcessOperationStatus.Succeeded,
		value,
		message,
		null
	);

	/// <summary>Creates a controlled unsuccessful result.</summary>
	public static ProcessOperationResult<T> Failure(
		ProcessOperationStatus status,
		string? message = null,
		int? nativeErrorCode = null
	) {
		if ( ProcessOperationStatus.Succeeded == status ) {
			throw new ArgumentOutOfRangeException(
				nameof( status )
			);
		}
		return new ProcessOperationResult<T>(
			status,
			default,
			message,
			nativeErrorCode
		);
	}

	private ProcessOperationResult(
		ProcessOperationStatus status,
		T? value,
		string? message,
		int? nativeErrorCode
	) {
		this.Status = status;
		this.Value = value;
		this.Message = message;
		this.NativeErrorCode = nativeErrorCode;
	}
}
