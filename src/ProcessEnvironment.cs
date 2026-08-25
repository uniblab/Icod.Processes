namespace Icod.Processes;

using System.Collections.ObjectModel;

/// <summary>
/// Represents an immutable child-process environment.
/// </summary>
public sealed class ProcessEnvironment {
	private readonly IReadOnlyDictionary<string, string> _variables;

	/// <summary>Gets the environment variables.</summary>
	public IReadOnlyDictionary<string, string> Variables => this._variables;

	/// <summary>Creates a builder initialized from the current process environment.</summary>
	public static ProcessEnvironmentBuilder CreateInheritedBuilder() => new(
		true
	);

	/// <summary>Creates an empty environment builder.</summary>
	public static ProcessEnvironmentBuilder CreateEmptyBuilder() => new(
		false
	);

	/// <summary>Initializes an immutable environment from a copied variable dictionary.</summary>
	internal ProcessEnvironment(
		IDictionary<string, string> variables
	) {
		this._variables = new ReadOnlyDictionary<string, string>(
			new Dictionary<string, string>(
				variables,
				ProcessEnvironmentBuilder.VariableNameComparer
			)
		);
	}
}

/// <summary>
/// Constructs child-process environments with explicit inheritance, replacement, and removal semantics.
/// </summary>
public sealed class ProcessEnvironmentBuilder {
	/// <summary>Gets the host-appropriate environment-variable name comparer.</summary>
	internal static StringComparer VariableNameComparer => OperatingSystem.IsWindows()
		? StringComparer.OrdinalIgnoreCase
		: StringComparer.Ordinal
	;

	private readonly Dictionary<string, string> _variables = new(
		VariableNameComparer
	);

	/// <summary>Initializes a process environment builder.</summary>
	public ProcessEnvironmentBuilder(
		bool inheritCurrentEnvironment = true
	) {
		if ( !inheritCurrentEnvironment ) {
			return;
		}
		foreach ( System.Collections.DictionaryEntry pair in Environment.GetEnvironmentVariables() ) {
			if ( pair.Key is string key
				&& pair.Value is string value
				&& !key.Contains( '=' )
				&& !key.Contains( '\0' )
			) {
				this._variables[ key ] = value;
			}
		}
	}

	/// <summary>Removes every environment variable.</summary>
	public ProcessEnvironmentBuilder Clear() {
		this._variables.Clear();
		return this;
	}

	/// <summary>Removes an environment variable.</summary>
	public ProcessEnvironmentBuilder Remove(
		string name
	) {
		ValidateName(
			name
		);
		this._variables.Remove(
			name
		);
		return this;
	}

	/// <summary>Sets an environment variable.</summary>
	public ProcessEnvironmentBuilder Set(
		string name,
		string value
	) {
		ValidateName(
			name
		);
		ArgumentNullException.ThrowIfNull(
			value
		);
		if ( value.Contains( '\0' ) ) {
			throw new ArgumentException(
				"Environment values cannot contain a null character.",
				nameof( value )
			);
		}
		this._variables[ name ] = value;
		return this;
	}

	/// <summary>Builds an immutable environment snapshot.</summary>
	public ProcessEnvironment Build() => new(
		this._variables
	);

	private static void ValidateName(
		string name
	) {
		ArgumentNullException.ThrowIfNull(
			name
		);
		if ( name.Contains( '=' ) || name.Contains( '\0' ) ) {
			throw new ArgumentException(
				"Environment variable names cannot contain '=' or a null character.",
				nameof( name )
			);
		}
	}
}
