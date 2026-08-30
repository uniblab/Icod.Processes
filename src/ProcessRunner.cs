namespace Icod.Processes;

using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using Icod.Timing;

/// <summary>
/// Provides a convenience facade for shared child-process execution.
/// </summary>
public static class ProcessRunner {
	/// <summary>Runs a child process asynchronously through the system executor.</summary>
	public static Task<ProcessResult> RunAsync(
		ProcessRunOptions options,
		CancellationToken cancellationToken = default
	) => SystemProcessExecutor.Instance.RunAsync(
		options,
		cancellationToken
	);
}

/// <summary>
/// Executes child processes without shell quoting, blocking waits, or redirected-stream deadlocks.
/// </summary>
public sealed class SystemProcessExecutor : IProcessExecutor {
	private static readonly object PosixSpawnWorkingDirectorySync = new();
	private static readonly TimeSpan PosixWaitPollInterval = TimeSpan.FromMilliseconds( 15 );
	private readonly IExecutableLocator _executableLocator;
	private readonly IProcessInspector _processInspector;
	private readonly IMonotonicClock _clock;

	/// <summary>Gets the shared system process executor.</summary>
	public static SystemProcessExecutor Instance {
		get;
	} = new(
		SystemExecutableLocator.Instance,
		SystemProcessInspector.Instance,
		SystemMonotonicClock.Instance
	);

	/// <summary>Initializes a system process executor with injectable providers.</summary>
	public SystemProcessExecutor(
		IExecutableLocator executableLocator,
		IProcessInspector processInspector,
		IMonotonicClock clock
	) {
		ArgumentNullException.ThrowIfNull(
			executableLocator
		);
		ArgumentNullException.ThrowIfNull(
			processInspector
		);
		ArgumentNullException.ThrowIfNull(
			clock
		);
		this._executableLocator = executableLocator;
		this._processInspector = processInspector;
		this._clock = clock;
	}

	/// <inheritdoc />
	public async Task<ProcessResult> RunAsync(
		ProcessRunOptions options,
		CancellationToken cancellationToken = default
	) {
		ArgumentNullException.ThrowIfNull(
			options
		);
		ArgumentNullException.ThrowIfNull(
			options.OutputEncoding
		);
		if ( options.UseUnreadableStandardInput && null != options.StandardInput ) {
			throw new ArgumentException(
				"Unreadable inherited standard input cannot be combined with a managed standard-input source.",
				nameof( options )
			);
		}
		var timeout = options.Timeout;
		if ( null != timeout && TimeSpan.Zero >= timeout.Value ) {
			throw new ArgumentOutOfRangeException(
				nameof( options ),
				"The process timeout must be positive."
			);
		}
		var startedTimestamp = this._clock.GetTimestamp();
		if ( options.UseUnreadableStandardInput && OperatingSystem.IsWindows() ) {
			const string message = "Unreadable inherited standard input is a POSIX-only launch capability.";
			if ( !options.ReturnLaunchFailureResult ) {
				throw new PlatformNotSupportedException( message );
			}
			return this.CreateLaunchFailure(
				startedTimestamp,
				message,
				ProcessLaunchFailureKind.SetupFailed
			);
		}
		if ( cancellationToken.IsCancellationRequested ) {
			return new ProcessResult(
				false,
				null,
				ProcessTermination.Canceled(),
				TimeSpan.Zero,
				null,
				null
			);
		}

		var environment = BuildEffectiveEnvironment(
			options
		);
		if ( null != options.WorkingDirectory ) {
			try {
				if ( !Directory.Exists( options.WorkingDirectory ) ) {
					const string prefix = "Working directory does not exist: ";
					var message = string.Concat( prefix, options.WorkingDirectory );
					if ( !options.ReturnLaunchFailureResult ) {
						throw new DirectoryNotFoundException( message );
					}
					return this.CreateLaunchFailure(
						startedTimestamp,
						message,
						ProcessLaunchFailureKind.SetupFailed
					);
				}
			} catch ( Exception exception ) when (
				exception is ArgumentException
				or IOException
				or NotSupportedException
				or UnauthorizedAccessException
			) {
				if ( !options.ReturnLaunchFailureResult ) {
					throw;
				}
				return this.CreateLaunchFailure(
					startedTimestamp,
					exception.Message,
					ProcessLaunchFailureKind.SetupFailed
				);
			}
		}
		var executable = options.FileName;
		if ( options.ResolveExecutable ) {
			var located = this._executableLocator.Locate(
				executable,
				environment,
				options.WorkingDirectory
			);
			if ( !located.Succeeded ) {
				var message = located.Message ?? $"Unable to locate executable '{executable}'.";
				var failureKind = ProcessOperationStatus.Vanished == located.Status
					? ProcessLaunchFailureKind.NotFound
					: ProcessLaunchFailureKind.CannotInvoke
				;
				if ( !options.ReturnLaunchFailureResult ) {
					if ( ProcessLaunchFailureKind.NotFound == failureKind ) {
						throw new FileNotFoundException(
							message,
							executable
						);
					}
					throw new InvalidOperationException(
						message
					);
				}
				return this.CreateLaunchFailure(
					startedTimestamp,
					message,
					failureKind
				);
			}
			executable = located.Value!;
		}
		if ( null != options.ArgumentZero
			|| 0 < options.PosixFileDescriptorDuplications.Count
			|| ( options.CreateProcessGroup && !OperatingSystem.IsWindows() )
		) {
			return await this.RunWithPosixSpawnAsync(
				options,
				executable,
				environment,
				startedTimestamp,
				cancellationToken
			).ConfigureAwait( false );
		}

		ProcessStartInfo startInfo;
		try {
			startInfo = BuildStartInfo(
				options,
				executable,
				environment
			);
		} catch ( Exception exception ) when (
			exception is ArgumentException
			or InvalidOperationException
			or PlatformNotSupportedException
		) {
			if ( !options.ReturnLaunchFailureResult ) {
				throw;
			}
			return this.CreateLaunchFailure(
				startedTimestamp,
				exception.Message,
				ProcessLaunchFailureKind.SetupFailed
			);
		}
		using var process = new Process {
			StartInfo = startInfo,
			EnableRaisingEvents = true
		};
		PosixProcessLaunchScope? launchScope = null;
		try {
			try {
				launchScope = PosixProcessLaunchScope.Enter(
					options.SignalPolicy,
					options.UseUnreadableStandardInput
				);
			} catch ( Exception exception ) when (
				exception is InvalidOperationException
				or PlatformNotSupportedException
			) {
				if ( !options.ReturnLaunchFailureResult ) {
					throw;
				}
				return this.CreateLaunchFailure(
					startedTimestamp,
					exception.Message,
					ProcessLaunchFailureKind.SetupFailed
				);
			}
			if ( !process.Start() ) {
				const string message = "The operating system declined to start the process.";
				if ( !options.ReturnLaunchFailureResult ) {
					throw new InvalidOperationException( message );
				}
				return this.CreateLaunchFailure(
					startedTimestamp,
					message,
					ProcessLaunchFailureKind.CannotInvoke
				);
			}
		} catch ( Exception exception ) when (
			exception is Win32Exception
			or FileNotFoundException
			or DirectoryNotFoundException
			or InvalidOperationException
			or UnauthorizedAccessException
		) {
			if ( !options.ReturnLaunchFailureResult ) {
				throw;
			}
			return this.CreateLaunchFailure(
				startedTimestamp,
				exception.Message,
				ClassifyLaunchFailure( exception, options.WorkingDirectory )
			);
		} finally {
			launchScope?.Dispose();
		}

		var identityResult = this._processInspector.ObserveIdentity(
			process.Id
		);
		var identity = identityResult.Succeeded
			? identityResult.Value!
			: new ProcessIdentity(
				process.Id
			)
		;
		using var capturedOutput = options.CaptureStandardOutput
			? new MemoryStream()
			: null
		;
		using var capturedError = options.CaptureStandardError
			? new MemoryStream()
			: null
		;
		using var timeoutCancellation = null == timeout
			? null
			: new CancellationTokenSource()
		;
		using var timeoutDelayCancellation = null == timeout
			? null
			: new CancellationTokenSource()
		;
		using var executionCancellation = null == timeoutCancellation
			? CancellationTokenSource.CreateLinkedTokenSource(
				cancellationToken
			)
			: CancellationTokenSource.CreateLinkedTokenSource(
				cancellationToken,
				timeoutCancellation.Token
			)
		;
		using var inputCancellation = CancellationTokenSource.CreateLinkedTokenSource(
			executionCancellation.Token
		);
		using var forwardingCancellation = CancellationTokenSource.CreateLinkedTokenSource(
			executionCancellation.Token
		);
		var timeoutTask = null == timeoutCancellation || null == timeoutDelayCancellation || null == timeout
			? Task.CompletedTask
			: this.TriggerTimeoutAsync(
				timeout.Value,
				timeoutCancellation,
				timeoutDelayCancellation.Token
			)
		;
		var inputTask = startInfo.RedirectStandardInput
			? ForwardInputAsync(
				options.StandardInput!,
				process.StandardInput.BaseStream,
				inputCancellation.Token
			)
			: Task.CompletedTask
		;
		var outputTask = startInfo.RedirectStandardOutput
			? ForwardOutputAsync(
				process.StandardOutput.BaseStream,
				options.StandardOutput,
				capturedOutput,
				forwardingCancellation.Token
			)
			: Task.CompletedTask
		;
		var errorTask = startInfo.RedirectStandardError
			? ForwardOutputAsync(
				process.StandardError.BaseStream,
				options.StandardError,
				capturedError,
				forwardingCancellation.Token
			)
			: Task.CompletedTask
		;
		try {
			options.ProcessStarted?.Invoke(
				identity
			);
			ProcessTermination termination;
			try {
				await process.WaitForExitAsync(
					executionCancellation.Token
				).ConfigureAwait( false );
				inputCancellation.Cancel();
				await Task.WhenAll(
					outputTask,
					errorTask
				).ConfigureAwait( false );
				termination = ProcessTermination.Exited(
					process.ExitCode
				);
			} catch ( OperationCanceledException ) when ( executionCancellation.IsCancellationRequested ) {
				var timedOut = null != timeoutCancellation
					&& timeoutCancellation.IsCancellationRequested
					&& !cancellationToken.IsCancellationRequested
				;
				inputCancellation.Cancel();
				forwardingCancellation.Cancel();
				if ( ProcessCancellationPolicy.LeaveRunning == options.CancellationPolicy ) {
					TryDisconnectRedirectedStreams(
						process,
						startInfo
					);
					termination = timedOut
						? ProcessTermination.TimedOut()
						: ProcessTermination.Canceled()
					;
				} else {
					var terminationRequested = TryKill(
						process,
						ProcessCancellationPolicy.KillProcessTree == options.CancellationPolicy
					);
					if ( terminationRequested ) {
						await WaitForExitAfterTerminationAsync(
							process
						).ConfigureAwait( false );
					} else {
						TryDisconnectRedirectedStreams(
							process,
							startInfo
						);
					}
					var exitCode = TryGetExitCode(
						process
					);
					termination = timedOut
						? ProcessTermination.TimedOut( exitCode )
						: ProcessTermination.Canceled( exitCode )
					;
				}
			}
			return new ProcessResult(
				true,
				identity,
				termination,
				this._clock.GetElapsedTime(
					startedTimestamp,
					this._clock.GetTimestamp()
				),
				Decode(
					capturedOutput,
					options.OutputEncoding
				),
				Decode(
					capturedError,
					options.OutputEncoding
				)
			);
		} catch {
			inputCancellation.Cancel();
			forwardingCancellation.Cancel();
			var terminationRequested = TryKill(
				process,
				ProcessCancellationPolicy.KillProcessTree == options.CancellationPolicy
			);
			if ( terminationRequested ) {
				await WaitForExitAfterTerminationAsync(
					process
				).ConfigureAwait( false );
			} else {
				TryDisconnectRedirectedStreams(
					process,
					startInfo
				);
			}
			throw;
		} finally {
			timeoutDelayCancellation?.Cancel();
			inputCancellation.Cancel();
			forwardingCancellation.Cancel();
			ObserveCompletion(
				timeoutTask,
				inputTask,
				outputTask,
				errorTask
			);
		}
	}

	private async Task<ProcessResult> RunWithPosixSpawnAsync(
		ProcessRunOptions options,
		string executable,
		ProcessEnvironment environment,
		long startedTimestamp,
		CancellationToken cancellationToken
	) {
		if ( OperatingSystem.IsWindows() ) {
			var message = 0 < options.PosixFileDescriptorDuplications.Count
				? "POSIX file-descriptor duplication is unavailable on Windows."
				: "The managed Windows launcher cannot set an independent native argument zero safely."
			;
			return this.HandlePosixSpawnSetupFailure(
				options,
				startedTimestamp,
				message
			);
		}
		if ( null != options.StandardInput
			|| null != options.StandardOutput
			|| null != options.StandardError
			|| options.CaptureStandardOutput
			|| options.CaptureStandardError
		) {
			return this.HandlePosixSpawnSetupFailure(
				options,
				startedTimestamp,
				"Native POSIX launch options currently require inherited standard streams."
			);
		}

		var processId = 0;
		int spawnResult;
		try {
			using var path = new Utf8NativeString( executable );
			var argumentValues = new List<string>( options.Arguments.Count + 1 ) {
				options.ArgumentZero ?? executable
			};
			argumentValues.AddRange( options.Arguments );
			using var arguments = new Utf8NativeStringVector( argumentValues );
			using var environmentVector = new Utf8NativeStringVector(
				environment.Variables.Select(
					static pair => string.Concat( pair.Key, "=", pair.Value )
				)
			);
			lock ( PosixSpawnWorkingDirectorySync ) {
				var previousDirectory = Environment.CurrentDirectory;
				try {
					if ( null != options.WorkingDirectory ) {
						Directory.SetCurrentDirectory( options.WorkingDirectory );
					}
					using var signalScope = PosixProcessLaunchScope.Enter(
						options.SignalPolicy,
						options.UseUnreadableStandardInput
					);
					using var spawnFileActions = new PosixSpawnFileActionsScope(
						options.PosixFileDescriptorDuplications
					);
					using var spawnAttributes = new PosixSpawnAttributeScope( options.CreateProcessGroup );
					spawnResult = ProcessNative.PosixSpawn(
						out processId,
						path.Pointer,
						spawnFileActions.Pointer,
						spawnAttributes.Pointer,
						arguments.Pointer,
						environmentVector.Pointer
					);
				} finally {
					if ( !string.Equals(
						Environment.CurrentDirectory,
						previousDirectory,
						StringComparison.Ordinal
					) ) {
						Directory.SetCurrentDirectory( previousDirectory );
					}
				}
			}
		} catch ( Exception exception ) when (
			exception is ArgumentException
			or DirectoryNotFoundException
			or IOException
			or InvalidOperationException
			or PlatformNotSupportedException
			or UnauthorizedAccessException
		) {
			if ( 0 < processId ) {
				TryTerminatePosixProcess( processId, true );
				await ReapPosixChildAsync( processId, this._clock ).ConfigureAwait( false );
			}
			return this.HandlePosixSpawnSetupFailure(
				options,
				startedTimestamp,
				exception.Message
			);
		}
		if ( 0 != spawnResult ) {
			var kind = ProcessNative.NoSuchFile == spawnResult
				? ProcessLaunchFailureKind.NotFound
				: ProcessLaunchFailureKind.CannotInvoke
			;
			if ( !options.ReturnLaunchFailureResult ) {
				throw new InvalidOperationException(
					$"posix_spawn failed with error {spawnResult}."
				);
			}
			return this.CreateLaunchFailure(
				startedTimestamp,
				$"Unable to invoke '{executable}' (error {spawnResult}).",
				kind
			);
		}

		var identityResult = this._processInspector.ObserveIdentity( processId );
		var identity = identityResult.Succeeded
			? identityResult.Value!
			: new ProcessIdentity( processId )
		;
		try {
			options.ProcessStarted?.Invoke( identity );
		} catch {
			TryTerminatePosixProcess( processId, true );
			await ReapPosixChildAsync( processId, this._clock ).ConfigureAwait( false );
			throw;
		}

		while ( true ) {
			var waitResult = ProcessNative.WaitPid(
				processId,
				out var waitStatus,
				ProcessNative.WaitNoHang
			);
			if ( processId == waitResult ) {
				return new ProcessResult(
					true,
					identity,
					TranslatePosixWaitStatus( waitStatus ),
					this._clock.GetElapsedTime( startedTimestamp, this._clock.GetTimestamp() ),
					null,
					null
				);
			}
			if ( 0 > waitResult ) {
				var error = Marshal.GetLastPInvokeError();
				if ( ProcessNative.Interrupted != error ) {
					return new ProcessResult(
						true,
						identity,
						ProcessTermination.Unknown( message: $"waitpid failed with errno {error}." ),
						this._clock.GetElapsedTime( startedTimestamp, this._clock.GetTimestamp() ),
						null,
						null
					);
				}
			}

			var timedOut = null != options.Timeout
				&& this._clock.GetElapsedTime( startedTimestamp, this._clock.GetTimestamp() ) >= options.Timeout.Value
			;
			var canceled = cancellationToken.IsCancellationRequested;
			if ( timedOut || canceled ) {
				if ( ProcessCancellationPolicy.LeaveRunning == options.CancellationPolicy ) {
					ObserveCompletion( ReapPosixChildAsync( processId, this._clock ) );
				} else {
					TryTerminatePosixProcess(
						processId,
						ProcessCancellationPolicy.KillProcessTree == options.CancellationPolicy
					);
					await ReapPosixChildAsync( processId, this._clock ).ConfigureAwait( false );
				}
				return new ProcessResult(
					true,
					identity,
					timedOut ? ProcessTermination.TimedOut() : ProcessTermination.Canceled(),
					this._clock.GetElapsedTime( startedTimestamp, this._clock.GetTimestamp() ),
					null,
					null
				);
			}
			await this._clock.DelayAsync( PosixWaitPollInterval ).ConfigureAwait( false );
		}
	}

	private ProcessResult HandlePosixSpawnSetupFailure(
		ProcessRunOptions options,
		long startedTimestamp,
		string message
	) {
		if ( !options.ReturnLaunchFailureResult ) {
			throw new PlatformNotSupportedException( message );
		}
		return this.CreateLaunchFailure(
			startedTimestamp,
			message,
			ProcessLaunchFailureKind.SetupFailed
		);
	}

	private static ProcessTermination TranslatePosixWaitStatus(
		int status
	) {
		var signalNumber = status & 0x7f;
		if ( 0 == signalNumber ) {
			return ProcessTermination.Exited( ( status >> 8 ) & 0xff );
		}
		var translated = ProcessSignalCatalog.Translate( signalNumber );
		return ProcessTermination.Signaled(
			translated.Succeeded
				? translated.Value!
				: new ProcessSignal(
					signalNumber,
					signalNumber.ToString( System.Globalization.CultureInfo.InvariantCulture )
				)
		);
	}

	private static void TryTerminatePosixProcess(
		int processId,
		bool entireProcessTree
	) {
		try {
			using var process = Process.GetProcessById( processId );
			process.Kill( entireProcessTree );
			return;
		} catch ( ArgumentException ) {
			return;
		} catch ( InvalidOperationException ) {
			return;
		} catch ( Exception exception ) when (
			exception is Win32Exception
			or PlatformNotSupportedException
			or NotSupportedException
		) {
		}
		_ = ProcessNative.Kill( processId, 9 );
	}

	private static async Task ReapPosixChildAsync(
		int processId,
		IMonotonicClock clock
	) {
		while ( true ) {
			var result = ProcessNative.WaitPid( processId, out _, ProcessNative.WaitNoHang );
			if ( processId == result ) {
				return;
			}
			if ( 0 > result ) {
				var error = Marshal.GetLastPInvokeError();
				if ( ProcessNative.Interrupted != error ) {
					return;
				}
			}
			await clock.DelayAsync( PosixWaitPollInterval ).ConfigureAwait( false );
		}
	}

	private async Task TriggerTimeoutAsync(
		TimeSpan timeout,
		CancellationTokenSource timeoutCancellation,
		CancellationToken cancellationToken
	) {
		try {
			await this._clock.DelayAsync(
				timeout,
				cancellationToken
			).ConfigureAwait( false );
			if ( !cancellationToken.IsCancellationRequested ) {
				timeoutCancellation.Cancel();
			}
		} catch ( OperationCanceledException ) when ( cancellationToken.IsCancellationRequested ) {
		}
	}

	private ProcessResult CreateLaunchFailure(
		long startedTimestamp,
		string message,
		ProcessLaunchFailureKind failureKind
	) => new(
		false,
		null,
		ProcessTermination.LaunchFailed(
			message,
			failureKind
		),
		this._clock.GetElapsedTime(
			startedTimestamp,
			this._clock.GetTimestamp()
		),
		null,
		null
	);

	private static ProcessLaunchFailureKind ClassifyLaunchFailure(
		Exception exception,
		string? workingDirectory
	) => exception switch {
		DirectoryNotFoundException when null != workingDirectory
			&& !Directory.Exists( workingDirectory ) => ProcessLaunchFailureKind.SetupFailed,
		FileNotFoundException or DirectoryNotFoundException => ProcessLaunchFailureKind.NotFound,
		Win32Exception win32Exception when (
			2 == win32Exception.NativeErrorCode || 3 == win32Exception.NativeErrorCode
		) => ProcessLaunchFailureKind.NotFound,
		_ => ProcessLaunchFailureKind.CannotInvoke
	};

	private static ProcessEnvironment BuildEffectiveEnvironment(
		ProcessRunOptions options
	) {
		var explicitEnvironment = options.Environment;
		var builder = null == explicitEnvironment
			? new ProcessEnvironmentBuilder(
				!options.ClearEnvironment
			)
			: ProcessEnvironment.CreateEmptyBuilder()
		;
		if ( null != explicitEnvironment ) {
			foreach ( var pair in explicitEnvironment.Variables ) {
				builder.Set(
					pair.Key,
					pair.Value
				);
			}
		}
		foreach ( var pair in options.EnvironmentVariables ) {
			if ( null == pair.Value ) {
				builder.Remove(
					pair.Key
				);
			} else {
				builder.Set(
					pair.Key,
					pair.Value
				);
			}
		}
		return builder.Build();
	}

	private static ProcessStartInfo BuildStartInfo(
		ProcessRunOptions options,
		string executable,
		ProcessEnvironment environment
	) {
		var startInfo = new ProcessStartInfo {
			FileName = executable,
			UseShellExecute = false,
			CreateNoWindow = true,
			RedirectStandardInput = null != options.StandardInput,
			RedirectStandardOutput = null != options.StandardOutput || options.CaptureStandardOutput,
			RedirectStandardError = null != options.StandardError || options.CaptureStandardError
		};
		if ( options.CreateProcessGroup && OperatingSystem.IsWindows() ) {
			startInfo.CreateNewProcessGroup = true;
		}
		if ( null != options.WorkingDirectory ) {
			startInfo.WorkingDirectory = options.WorkingDirectory;
		}
		foreach ( var argument in options.Arguments ) {
			startInfo.ArgumentList.Add(
				argument
			);
		}
		startInfo.Environment.Clear();
		foreach ( var pair in environment.Variables ) {
			startInfo.Environment[ pair.Key ] = pair.Value;
		}
		return startInfo;
	}

	private static async Task ForwardInputAsync(
		Stream source,
		Stream destination,
		CancellationToken cancellationToken
	) {
		try {
			await source.CopyToAsync(
				destination,
				cancellationToken
			).ConfigureAwait( false );
			await destination.FlushAsync(
				cancellationToken
			).ConfigureAwait( false );
		} catch ( IOException ) {
			// The child may close standard input after reading all data it needs.
		} finally {
			try {
				await destination.DisposeAsync().ConfigureAwait( false );
			} catch ( IOException ) {
			} catch ( ObjectDisposedException ) {
			}
		}
	}

	private static async Task ForwardOutputAsync(
		Stream source,
		Stream? destination,
		MemoryStream? capture,
		CancellationToken cancellationToken
	) {
		var buffer = new byte[ 65536 ];
		Exception? forwardingFailure = null;
		while ( true ) {
			var read = await source.ReadAsync(
				buffer.AsMemory(),
				cancellationToken
			).ConfigureAwait( false );
			if ( 0 == read ) {
				break;
			}
			if ( null != capture ) {
				lock ( capture ) {
					capture.Write(
						buffer,
						0,
						read
					);
				}
			}
			if ( null != destination && null == forwardingFailure ) {
				try {
					await destination.WriteAsync(
						buffer.AsMemory(
							0,
							read
						),
						cancellationToken
					).ConfigureAwait( false );
				} catch ( Exception exception ) {
					forwardingFailure = exception;
				}
			}
		}
		if ( null != destination && null == forwardingFailure ) {
			try {
				await destination.FlushAsync(
					cancellationToken
				).ConfigureAwait( false );
			} catch ( Exception exception ) {
				forwardingFailure = exception;
			}
		}
		if ( null != forwardingFailure ) {
			ExceptionDispatchInfo.Capture(
				forwardingFailure
			).Throw();
		}
	}

	private static void ObserveCompletion(
		params Task[] tasks
	) => _ = IgnoreCancellationAsync(
		tasks
	);

	private static async Task IgnoreCancellationAsync(
		params Task[] tasks
	) {
		try {
			await Task.WhenAll(
				tasks
			).ConfigureAwait( false );
		} catch ( OperationCanceledException ) {
		} catch ( IOException ) {
		} catch ( ObjectDisposedException ) {
		} catch ( Exception ) {
		}
	}

	private static bool TryKill(
		Process process,
		bool entireProcessTree
	) {
		try {
			if ( process.HasExited ) {
				return true;
			}
			process.Kill(
				entireProcessTree
			);
			return true;
		} catch ( PlatformNotSupportedException ) when ( entireProcessTree ) {
			return TryKillSingleProcess(
				process
			);
		} catch ( NotSupportedException ) when ( entireProcessTree ) {
			return TryKillSingleProcess(
				process
			);
		} catch ( InvalidOperationException ) {
			return true;
		} catch ( Win32Exception ) {
			return false;
		}
	}

	private static bool TryKillSingleProcess(
		Process process
	) {
		try {
			if ( process.HasExited ) {
				return true;
			}
			process.Kill();
			return true;
		} catch ( InvalidOperationException ) {
			return true;
		} catch ( Win32Exception ) {
			return false;
		}
	}

	private static async Task WaitForExitAfterTerminationAsync(
		Process process
	) {
		try {
			await process.WaitForExitAsync().ConfigureAwait( false );
		} catch ( InvalidOperationException ) {
		} catch ( Win32Exception ) {
		}
	}

	private static void TryDisconnectRedirectedStreams(
		Process process,
		ProcessStartInfo startInfo
	) {
		if ( startInfo.RedirectStandardInput ) {
			TryDispose(
				process.StandardInput
			);
		}
		if ( startInfo.RedirectStandardOutput ) {
			TryDispose(
				process.StandardOutput
			);
		}
		if ( startInfo.RedirectStandardError ) {
			TryDispose(
				process.StandardError
			);
		}
	}

	private static void TryDispose(
		IDisposable disposable
	) {
		try {
			disposable.Dispose();
		} catch ( IOException ) {
		} catch ( ObjectDisposedException ) {
		}
	}

	private static int? TryGetExitCode(
		Process process
	) {
		try {
			return process.HasExited
				? process.ExitCode
				: null
			;
		} catch ( InvalidOperationException ) {
			return null;
		} catch ( Win32Exception ) {
			return null;
		}
	}

	private static string? Decode(
		MemoryStream? stream,
		System.Text.Encoding encoding
	) {
		if ( null == stream ) {
			return null;
		}
		lock ( stream ) {
			return encoding.GetString(
				stream.GetBuffer(),
				0,
				checked( (int)stream.Length )
			);
		}
	}
}

/// <summary>Owns one unmanaged UTF-8 string used by a native POSIX process launch.</summary>
internal sealed class Utf8NativeString : IDisposable {
	/// <summary>Gets the unmanaged null-terminated UTF-8 pointer.</summary>
	internal IntPtr Pointer {
		get;
		private set;
	}

	/// <summary>Initializes an unmanaged UTF-8 string.</summary>
	internal Utf8NativeString(
		string value
	) {
		ArgumentNullException.ThrowIfNull( value );
		this.Pointer = Marshal.StringToCoTaskMemUTF8( value );
	}

	/// <inheritdoc />
	public void Dispose() {
		if ( IntPtr.Zero == this.Pointer ) {
			return;
		}
		Marshal.FreeCoTaskMem( this.Pointer );
		this.Pointer = IntPtr.Zero;
	}
}

/// <summary>Owns an unmanaged null-terminated vector of UTF-8 strings.</summary>
internal sealed class Utf8NativeStringVector : IDisposable {
	private readonly List<IntPtr> _strings = [];

	/// <summary>Gets the unmanaged vector pointer.</summary>
	internal IntPtr Pointer {
		get;
		private set;
	}

	/// <summary>Initializes an unmanaged UTF-8 vector.</summary>
	internal Utf8NativeStringVector(
		IEnumerable<string> values
	) {
		ArgumentNullException.ThrowIfNull( values );
		var materialized = values.ToArray();
		this.Pointer = Marshal.AllocHGlobal(
			checked( ( materialized.Length + 1 ) * IntPtr.Size )
		);
		try {
			for ( var index = 0; index < materialized.Length; index++ ) {
				var pointer = Marshal.StringToCoTaskMemUTF8( materialized[ index ] );
				this._strings.Add( pointer );
				Marshal.WriteIntPtr( this.Pointer, index * IntPtr.Size, pointer );
			}
			Marshal.WriteIntPtr( this.Pointer, materialized.Length * IntPtr.Size, IntPtr.Zero );
		} catch {
			this.Dispose();
			throw;
		}
	}

	/// <inheritdoc />
	public void Dispose() {
		foreach ( var pointer in this._strings ) {
			Marshal.FreeCoTaskMem( pointer );
		}
		this._strings.Clear();
		if ( IntPtr.Zero != this.Pointer ) {
			Marshal.FreeHGlobal( this.Pointer );
			this.Pointer = IntPtr.Zero;
		}
	}
}
