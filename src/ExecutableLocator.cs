namespace Icod.Processes;

/// <summary>
/// Resolves executable names according to an explicit environment and working directory.
/// </summary>
public interface IExecutableLocator {
	/// <summary>Finds an executable or returns a controlled unsuccessful result.</summary>
	ProcessOperationResult<string> Locate(
		string executable,
		ProcessEnvironment? environment = null,
		string? workingDirectory = null
	);
}

/// <summary>
/// Provides platform-aware executable lookup without invoking a command shell.
/// </summary>
public sealed class SystemExecutableLocator : IExecutableLocator {
	/// <summary>Gets the shared system executable locator.</summary>
	public static SystemExecutableLocator Instance {
		get;
	} = new();

	private SystemExecutableLocator() {
	}

	/// <inheritdoc />
	public ProcessOperationResult<string> Locate(
		string executable,
		ProcessEnvironment? environment = null,
		string? workingDirectory = null
	) {
		ArgumentNullException.ThrowIfNull( executable );
		if ( executable.Contains( '\0' ) ) {
			return ProcessOperationResult<string>.Failure(
				ProcessOperationStatus.InvalidArgument,
				"Executable names cannot contain a NUL character."
			);
		}
		if ( 0 == executable.Length ) {
			return ProcessOperationResult<string>.Failure(
				ProcessOperationStatus.Vanished,
				"Executable '' was not found."
			);
		}
		try {
			return LocateCore(
				executable,
				environment,
				workingDirectory
			);
		} catch ( UnauthorizedAccessException exception ) {
			return ProcessOperationResult<string>.Failure(
				ProcessOperationStatus.AccessDenied,
				exception.Message
			);
		} catch ( IOException exception ) {
			return ProcessOperationResult<string>.Failure(
				ProcessOperationStatus.Failed,
				exception.Message
			);
		} catch ( ArgumentException exception ) {
			return ProcessOperationResult<string>.Failure(
				ProcessOperationStatus.InvalidArgument,
				exception.Message
			);
		} catch ( NotSupportedException exception ) {
			return ProcessOperationResult<string>.Failure(
				ProcessOperationStatus.InvalidArgument,
				exception.Message
			);
		}
	}

	private static ProcessOperationResult<string> LocateCore(
		string executable,
		ProcessEnvironment? environment,
		string? workingDirectory
	) {
		var baseDirectory = string.IsNullOrWhiteSpace( workingDirectory )
			? Environment.CurrentDirectory
			: System.IO.Path.GetFullPath( workingDirectory )
		;
		var variables = environment?.Variables;
		var extensions = GetCandidateExtensions(
			executable,
			GetVariable(
				variables,
				"PATHEXT"
			)
		);
		if ( System.IO.Path.IsPathRooted( executable ) || HasDirectoryComponent( executable ) ) {
			var baseCandidate = System.IO.Path.IsPathRooted( executable )
				? System.IO.Path.GetFullPath( executable )
				: System.IO.Path.GetFullPath(
					executable,
					baseDirectory
				)
			;
			return LocateCandidates(
				extensions.Select(
					extension => string.Concat(
						baseCandidate,
						extension
					)
				)
			);
		}

		var pathValue = GetVariable(
			variables,
			"PATH"
		) ?? GetDefaultSearchPath();
		var candidates = new List<string>();
		foreach ( var pathEntry in pathValue.Split( System.IO.Path.PathSeparator ) ) {
			var directory = string.IsNullOrEmpty( pathEntry )
				? baseDirectory
				: pathEntry
			;
			if ( !System.IO.Path.IsPathRooted( directory ) ) {
				directory = System.IO.Path.GetFullPath(
					directory,
					baseDirectory
				);
			}
			foreach ( var extension in extensions ) {
				candidates.Add(
					System.IO.Path.Combine(
						directory,
						string.Concat(
							executable,
							extension
						)
					)
				);
			}
		}
		var result = LocateCandidates(
			candidates
		);
		return result.Succeeded || ProcessOperationStatus.Vanished != result.Status
			? result
			: ProcessOperationResult<string>.Failure(
				ProcessOperationStatus.Vanished,
				$"Executable '{executable}' was not found."
			)
		;
	}

	private static string? GetVariable(
		IReadOnlyDictionary<string, string>? variables,
		string name
	) {
		if ( null != variables ) {
			return variables.TryGetValue(
				name,
				out var explicitValue
			)
				? explicitValue
				: null
			;
		}
		return Environment.GetEnvironmentVariable(
			name
		);
	}

	private static string GetDefaultSearchPath() => OperatingSystem.IsWindows()
		? string.Empty
		: string.Concat( "/bin", System.IO.Path.PathSeparator, "/usr/bin" )
	;

	private static IReadOnlyList<string> GetCandidateExtensions(
		string executable,
		string? pathExtensions
	) {
		if ( !OperatingSystem.IsWindows() ) {
			return [ string.Empty ];
		}
		if ( !string.IsNullOrEmpty( System.IO.Path.GetExtension( executable ) ) ) {
			return [ string.Empty ];
		}
		var extensions = string.IsNullOrWhiteSpace( pathExtensions )
			? ".COM;.EXE;.BAT;.CMD"
			: pathExtensions
		;
		return extensions.Split(
			';',
			StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
		).Select(
			static extension => extension.StartsWith( '.' )
				? extension
				: string.Concat(
					".",
					extension
				)
		).Distinct(
			StringComparer.OrdinalIgnoreCase
		).ToArray();
	}

	private static bool HasDirectoryComponent(
		string executable
	) => executable.Contains( System.IO.Path.DirectorySeparatorChar )
		|| executable.Contains( System.IO.Path.AltDirectorySeparatorChar )
	;

	private static ProcessOperationResult<string> LocateCandidates(
		IEnumerable<string> candidates
	) {
		ProcessOperationResult<string>? firstMeaningfulFailure = null;
		foreach ( var candidate in candidates ) {
			var result = LocateCandidate(
				candidate
			);
			if ( result.Succeeded ) {
				return result;
			}
			if ( ProcessOperationStatus.Vanished != result.Status && null == firstMeaningfulFailure ) {
				firstMeaningfulFailure = result;
			}
		}
		return firstMeaningfulFailure ?? ProcessOperationResult<string>.Failure(
			ProcessOperationStatus.Vanished
		);
	}

	private static ProcessOperationResult<string> LocateCandidate(
		string candidate
	) {
		try {
			if ( Directory.Exists( candidate ) ) {
				return ProcessOperationResult<string>.Failure(
					ProcessOperationStatus.AccessDenied,
					$"File '{candidate}' is a directory and cannot be executed."
				);
			}
			if ( !File.Exists( candidate ) ) {
				return ProcessOperationResult<string>.Failure(
					ProcessOperationStatus.Vanished
				);
			}
			if ( !OperatingSystem.IsWindows() ) {
				var mode = File.GetUnixFileMode(
					candidate
				);
				const UnixFileMode executableBits = UnixFileMode.UserExecute
					| UnixFileMode.GroupExecute
					| UnixFileMode.OtherExecute
				;
				if ( 0 == ( mode & executableBits ) ) {
					return ProcessOperationResult<string>.Failure(
						ProcessOperationStatus.AccessDenied,
						$"File '{candidate}' is not executable."
					);
				}
			}
			return ProcessOperationResult<string>.Success(
				System.IO.Path.GetFullPath( candidate )
			);
		} catch ( UnauthorizedAccessException exception ) {
			return ProcessOperationResult<string>.Failure(
				ProcessOperationStatus.AccessDenied,
				exception.Message
			);
		} catch ( IOException exception ) {
			return ProcessOperationResult<string>.Failure(
				ProcessOperationStatus.Failed,
				exception.Message
			);
		} catch ( ArgumentException exception ) {
			return ProcessOperationResult<string>.Failure(
				ProcessOperationStatus.InvalidArgument,
				exception.Message
			);
		}
	}
}
