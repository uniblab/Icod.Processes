namespace Icod.Processes;

/// <summary>
/// Identifies a process instance with an optional token that protects against PID reuse.
/// </summary>
public sealed class ProcessIdentity : IEquatable<ProcessIdentity> {
	/// <summary>Gets the operating-system process identifier.</summary>
	public int ProcessId {
		get;
	}

	/// <summary>Gets the optional PID-reuse detection token.</summary>
	public ProcessReuseToken? ReuseToken {
		get;
	}

	/// <summary>Initializes a process identity.</summary>
	public ProcessIdentity(
		int processId,
		ProcessReuseToken? reuseToken = null
	) {
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(
			processId
		);
		this.ProcessId = processId;
		this.ReuseToken = reuseToken;
	}

	/// <inheritdoc />
	public bool Equals(
		ProcessIdentity? other
	) => null != other
		&& this.ProcessId == other.ProcessId
		&& Equals(
			this.ReuseToken,
			other.ReuseToken
		)
	;

	/// <inheritdoc />
	public override bool Equals(
		object? obj
	) => this.Equals(
		obj as ProcessIdentity
	);

	/// <inheritdoc />
	public override int GetHashCode() => HashCode.Combine(
		this.ProcessId,
		this.ReuseToken
	);

	/// <inheritdoc />
	public override string ToString() => null == this.ReuseToken
		? this.ProcessId.ToString(
			System.Globalization.CultureInfo.InvariantCulture
		)
		: string.Concat(
			this.ProcessId.ToString(
				System.Globalization.CultureInfo.InvariantCulture
			),
			"@",
			this.ReuseToken
		)
	;
}

/// <summary>
/// Contains an opaque, provider-specific value used to distinguish reused process identifiers.
/// </summary>
public sealed class ProcessReuseToken : IEquatable<ProcessReuseToken> {
	/// <summary>Gets the token scheme.</summary>
	public string Scheme {
		get;
	}

	/// <summary>Gets the opaque token value.</summary>
	public string Value {
		get;
	}

	/// <summary>Initializes a PID-reuse token.</summary>
	public ProcessReuseToken(
		string scheme,
		string value
	) {
		ArgumentException.ThrowIfNullOrWhiteSpace(
			scheme
		);
		ArgumentException.ThrowIfNullOrWhiteSpace(
			value
		);
		this.Scheme = scheme;
		this.Value = value;
	}

	/// <inheritdoc />
	public bool Equals(
		ProcessReuseToken? other
	) => null != other
		&& string.Equals(
			this.Scheme,
			other.Scheme,
			StringComparison.Ordinal
		)
		&& string.Equals(
			this.Value,
			other.Value,
			StringComparison.Ordinal
		)
	;

	/// <inheritdoc />
	public override bool Equals(
		object? obj
	) => this.Equals(
		obj as ProcessReuseToken
	);

	/// <inheritdoc />
	public override int GetHashCode() => HashCode.Combine(
		StringComparer.Ordinal.GetHashCode(
			this.Scheme
		),
		StringComparer.Ordinal.GetHashCode(
			this.Value
		)
	);

	/// <inheritdoc />
	public override string ToString() => string.Concat(
		this.Scheme,
		":",
		this.Value
	);
}
