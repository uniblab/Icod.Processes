namespace Icod.Processes.Tests;

using Icod.Processes;
using System.Text;
using Icod.Timing;
using Xunit;

/// <summary>
/// Exercises argument-safe launch, environment construction, working directories, and timeout cleanup.
/// </summary>
public sealed class ProcessRunnerTests {
	/// <summary>Verifies that arguments are forwarded without shell interpolation.</summary>
	[Fact]
	public async Task PreservesExactArguments() {
		var options = CreateHostOptions(
			"args",
			"a value with spaces",
			"\"quoted\"",
			"semi;colon",
			string.Empty
		);
		options.CaptureStandardOutput = true;
		ProcessIdentity? startedIdentity = null;
		options.ProcessStarted = identity => startedIdentity = identity;

		var result = await ProcessRunner.RunAsync(
			options
		);

		Assert.Equal( 0, result.ExitCode );
		Assert.True( result.Started );
		Assert.NotNull( result.Identity );
		Assert.Equal( result.Identity, startedIdentity );
		var expected = string.Concat(
			new[] {
				"a value with spaces",
				"\"quoted\"",
				"semi;colon",
				string.Empty
			}.Select(
				value => string.Concat(
					"B:",
					Convert.ToBase64String(
						Encoding.UTF8.GetBytes( value )
					),
					Environment.NewLine
				)
			)
		);
		Assert.Equal( expected, result.StandardOutput );
	}

	/// <summary>Verifies inherited environment modification and working-directory selection.</summary>
	[Fact]
	public async Task AppliesEnvironmentAndWorkingDirectory() {
		var environmentOptions = CreateHostOptions(
			"environment",
			"ICOD_PROCESSES_VALUE"
		);
		environmentOptions.Environment = ProcessEnvironment.CreateInheritedBuilder()
			.Set(
				"ICOD_PROCESSES_VALUE",
				"expected"
			)
			.Build();
		environmentOptions.CaptureStandardOutput = true;
		var environmentResult = await ProcessRunner.RunAsync(
			environmentOptions
		);

		var directory = System.IO.Path.Combine(
			AppContext.BaseDirectory,
			$"icod-processes-cwd-{Guid.NewGuid():N}"
		);
		Directory.CreateDirectory(
			directory
		);
		try {
			var directoryOptions = CreateHostOptions(
				"cwd"
			);
			directoryOptions.WorkingDirectory = directory;
			directoryOptions.CaptureStandardOutput = true;
			var directoryResult = await ProcessRunner.RunAsync(
				directoryOptions
			);

			Assert.Equal( "expected", environmentResult.StandardOutput );
			Assert.Equal(
				System.IO.Path.GetFullPath( directory ),
				System.IO.Path.GetFullPath( directoryResult.StandardOutput! )
			);
		} finally {
			Directory.Delete(
				directory,
				true
			);
		}
	}

	/// <summary>Verifies controlled not-found launch classification and portable status translation.</summary>
	[Fact]
	public async Task ClassifiesMissingExecutable() {
		var options = new ProcessRunOptions(
			$"icod-processes-missing-{Guid.NewGuid():N}"
		) {
			ResolveExecutable = true,
			ReturnLaunchFailureResult = true
		};

		var result = await ProcessRunner.RunAsync(
			options
		);

		Assert.False( result.Started );
		Assert.Equal(
			ProcessTerminationKind.LaunchFailed,
			result.Termination.Kind
		);
		Assert.Equal(
			ProcessLaunchFailureKind.NotFound,
			result.Termination.LaunchFailureKind
		);
		Assert.Equal( 127, result.Termination.ToPortableExitCode() );
	}

	/// <summary>Verifies that the POSIX native launcher accepts an explicit argument zero.</summary>
	[Fact]
	public async Task PosixNativeLaunchAcceptsCustomArgumentZero() {
		if ( OperatingSystem.IsWindows() ) {
			return;
		}

		var options = CreateNativeHostOptions(
			"exit",
			"0"
		);
		options.ArgumentZero = "icod-processes-test-host";

		var result = await ProcessRunner.RunAsync(
			options
		);

		Assert.True( result.Started );
		Assert.Equal( 0, result.ExitCode );
	}

	/// <summary>Verifies POSIX current-process replacement preserves the process identity.</summary>
	[Fact]
	public async Task PosixExecReplacementPreservesProcessIdentity() {
		if ( OperatingSystem.IsWindows() ) {
			return;
		}

		var observationPath = System.IO.Path.Combine(
			System.IO.Path.GetTempPath(),
			$"icod-processes-exec-{Guid.NewGuid():N}"
		);
		try {
			var host = GetNativeProcessTestHostPath();
			var options = CreateNativeHostOptions(
				"replace",
				host,
				"pid-file",
				observationPath
			);
			ProcessIdentity? startedIdentity = null;
			options.ProcessStarted = identity => startedIdentity = identity;

			var result = await ProcessRunner.RunAsync(
				options
			);

			Assert.True( result.Started );
			Assert.Equal( 0, result.ExitCode );
			Assert.NotNull( startedIdentity );
			var replacementProcessId = int.Parse(
				await File.ReadAllTextAsync(
					observationPath
				),
				System.Globalization.CultureInfo.InvariantCulture
			);
			Assert.Equal(
				startedIdentity.ProcessId,
				replacementProcessId
			);
		} finally {
			if ( File.Exists( observationPath ) ) {
				File.Delete(
					observationPath
				);
			}
		}
	}

	/// <summary>Verifies POSIX replacement preserves execvp-style shell fallback for executable text.</summary>
	[Fact]
	public async Task PosixExecReplacementFallsBackToShellForExecutableText() {
		if ( OperatingSystem.IsWindows() ) {
			return;
		}

		var directory = System.IO.Path.Combine(
			System.IO.Path.GetTempPath(),
			$"icod-processes-exec-shell-{Guid.NewGuid():N}"
		);
		Directory.CreateDirectory(
			directory
		);
		var scriptPath = System.IO.Path.Combine(
			directory,
			"command"
		);
		var observationPath = System.IO.Path.Combine(
			directory,
			"pid"
		);
		try {
			await File.WriteAllTextAsync(
				scriptPath,
				"printf '%s' \"$$\" > \"$1\""
			);
			File.SetUnixFileMode(
				scriptPath,
				UnixFileMode.UserRead
					| UnixFileMode.UserWrite
					| UnixFileMode.UserExecute
			);

			var options = CreateNativeHostOptions(
				"replace",
				scriptPath,
				observationPath
			);
			ProcessIdentity? startedIdentity = null;
			options.ProcessStarted = identity => startedIdentity = identity;

			var result = await ProcessRunner.RunAsync(
				options
			);

			Assert.True( result.Started );
			Assert.Equal( 0, result.ExitCode );
			Assert.NotNull( startedIdentity );
			var replacementProcessId = int.Parse(
				await File.ReadAllTextAsync(
					observationPath
				),
				System.Globalization.CultureInfo.InvariantCulture
			);
			Assert.Equal(
				startedIdentity.ProcessId,
				replacementProcessId
			);
		} finally {
			Directory.Delete(
				directory,
				true
			);
		}
	}

	/// <summary>Verifies POSIX replacement applies ordered descriptor actions without changing process identity.</summary>
	[Fact]
	public async Task PosixExecReplacementAppliesFileDescriptorDuplications() {
		if ( OperatingSystem.IsWindows() ) {
			return;
		}

		var outputPath = System.IO.Path.Combine(
			System.IO.Path.GetTempPath(),
			$"icod-processes-exec-fd-{Guid.NewGuid():N}"
		);
		try {
			var host = GetNativeProcessTestHostPath();
			var options = CreateNativeHostOptions(
				"replace-output",
				host,
				outputPath,
				"pid"
			);
			ProcessIdentity? startedIdentity = null;
			options.ProcessStarted = identity => startedIdentity = identity;

			var result = await ProcessRunner.RunAsync(
				options
			);

			Assert.True( result.Started );
			Assert.Equal( 0, result.ExitCode );
			Assert.NotNull( startedIdentity );
			var replacementProcessId = int.Parse(
				await File.ReadAllTextAsync(
					outputPath
				),
				System.Globalization.CultureInfo.InvariantCulture
			);
			Assert.Equal(
				startedIdentity.ProcessId,
				replacementProcessId
			);
		} finally {
			if ( File.Exists( outputPath ) ) {
				File.Delete(
					outputPath
				);
			}
		}
	}

	/// <summary>Verifies that POSIX process-group creation makes the child its group leader.</summary>
	[Fact]
	public async Task PosixNativeLaunchCreatesProcessGroup() {
		if ( OperatingSystem.IsWindows() ) {
			return;
		}

		var observationPath = System.IO.Path.Combine(
			System.IO.Path.GetTempPath(),
			$"icod-processes-pgid-{Guid.NewGuid():N}"
		);
		try {
			var options = CreateNativeHostOptions(
				"process-group-file",
				observationPath
			);
			options.CreateProcessGroup = true;

			var result = await ProcessRunner.RunAsync(
				options
			);

			Assert.True( result.Started );
			Assert.Equal( 0, result.ExitCode );
			Assert.NotNull( result.Identity );

			var observation = await File.ReadAllTextAsync(
				observationPath
			);
			var fields = observation.Split(
				':'
			);
			Assert.Equal( 2, fields.Length );
			Assert.True(
				int.TryParse(
					fields[ 0 ],
					out var childProcessId
				)
			);
			Assert.True(
				int.TryParse(
					fields[ 1 ],
					out var childProcessGroupId
				)
			);
			Assert.Equal( result.Identity.ProcessId, childProcessId );
			Assert.Equal( childProcessId, childProcessGroupId );
		} finally {
			if ( File.Exists( observationPath ) ) {
				File.Delete(
					observationPath
				);
			}
		}
	}

	/// <summary>Verifies native POSIX launch applies ordered child-only descriptor duplications.</summary>
	[Fact]
	public async Task PosixNativeLaunchAppliesFileDescriptorDuplications() {
		if ( OperatingSystem.IsWindows() ) {
			return;
		}

		var outputPath = System.IO.Path.Combine(
			System.IO.Path.GetTempPath(),
			$"icod-processes-fd-{Guid.NewGuid():N}"
		);
		ProcessResult result;
		try {
			await using ( var output = new FileStream(
				outputPath,
				FileMode.CreateNew,
				FileAccess.Write,
				FileShare.Read | FileShare.Write | FileShare.Delete
			) ) {
				var descriptor = output.SafeFileHandle.DangerousGetHandle().ToInt32();
				var options = CreateNativeHostOptions(
					"dual",
					"1"
				);
				options.PosixFileDescriptorDuplications.Add(
					new PosixFileDescriptorDuplication(
						descriptor,
						1,
						closeSource: true
					)
				);
				options.PosixFileDescriptorDuplications.Add(
					new PosixFileDescriptorDuplication(
						1,
						2
					)
				);

				result = await ProcessRunner.RunAsync(
					options
				);
			}

			var outputText = await File.ReadAllTextAsync(
				outputPath
			);
			Assert.True( result.Started );
			Assert.Equal( 0, result.ExitCode );
			Assert.Contains( "out-0", outputText, StringComparison.Ordinal );
			Assert.Contains( "err-0", outputText, StringComparison.Ordinal );
		} finally {
			if ( File.Exists( outputPath ) ) {
				File.Delete(
					outputPath
				);
			}
		}
	}

	/// <summary>Verifies that native POSIX launch rejects managed output capture predictably.</summary>
	[Fact]
	public async Task PosixNativeLaunchRejectsCapturedOutput() {
		if ( OperatingSystem.IsWindows() ) {
			return;
		}

		var options = CreateNativeHostOptions(
			"exit",
			"0"
		);
		options.CreateProcessGroup = true;
		options.CaptureStandardOutput = true;

		var result = await ProcessRunner.RunAsync(
			options
		);

		Assert.False( result.Started );
		Assert.Equal(
			ProcessTerminationKind.LaunchFailed,
			result.Termination.Kind
		);
		Assert.Equal(
			ProcessLaunchFailureKind.SetupFailed,
			result.Termination.LaunchFailureKind
		);
	}

	/// <summary>Verifies monotonic timeout classification and child cleanup.</summary>
	[Fact]
	public async Task TimesOutAndTerminatesChild() {
		var options = CreateHostOptions(
			"sleep",
			"30000"
		);
		options.Timeout = TimeSpan.FromSeconds( 30 );
		var executor = new SystemProcessExecutor(
			SystemExecutableLocator.Instance,
			SystemProcessInspector.Instance,
			new ImmediateTimeoutClock()
		);

		var result = await executor.RunAsync(
			options
		);

		Assert.True( result.Started );
		Assert.True( result.TimedOut );
		Assert.False( result.WasCanceled );
		Assert.True( TimeSpan.Zero < result.Elapsed );
	}

	private sealed class ImmediateTimeoutClock : IMonotonicClock {
		private long _timestamp;

		/// <inheritdoc />
		public long GetTimestamp() => Interlocked.Read(
			ref this._timestamp
		);

		/// <inheritdoc />
		public TimeSpan GetElapsedTime(
			long startingTimestamp,
			long endingTimestamp
		) => TimeSpan.FromTicks(
			endingTimestamp - startingTimestamp
		);

		/// <inheritdoc />
		public ValueTask DelayAsync(
			TimeSpan delay,
			CancellationToken cancellationToken = default
		) {
			cancellationToken.ThrowIfCancellationRequested();
			Interlocked.Add(
				ref this._timestamp,
				delay.Ticks
			);
			return ValueTask.CompletedTask;
		}
	}

	/// <summary>Creates process options that invoke the deterministic test host.</summary>
	internal static ProcessRunOptions CreateHostOptions(
		params string[] arguments
	) {
		var host = GetProcessTestHostPath();
		Assert.True(
			File.Exists( host ),
			$"Process test host was not built at '{host}'."
		);
		var dotnet = Environment.GetEnvironmentVariable(
			"DOTNET_HOST_PATH"
		) ?? "dotnet";
		var options = new ProcessRunOptions(
			dotnet
		) {
			ReturnLaunchFailureResult = true
		};
		options.Arguments.Add(
			host
		);
		foreach ( var argument in arguments ) {
			options.Arguments.Add(
				argument
			);
		}
		return options;
	}

	private static ProcessRunOptions CreateNativeHostOptions(
		params string[] arguments
	) {
		var host = GetNativeProcessTestHostPath();
		Assert.True(
			File.Exists( host ),
			$"Native process test host was not built at '{host}'."
		);
		var options = new ProcessRunOptions(
			host
		) {
			ReturnLaunchFailureResult = true
		};
		foreach ( var argument in arguments ) {
			options.Arguments.Add(
				argument
			);
		}
		return options;
	}

	private static string GetNativeProcessTestHostPath() {
		var managedHost = GetProcessTestHostPath();
		var hostDirectory = System.IO.Path.GetDirectoryName(
			managedHost
		) ?? throw new InvalidOperationException( "Unable to locate the process test host directory." );
		return System.IO.Path.Combine(
			hostDirectory,
			"Icod.Processes.ProcessTestHost"
		);
	}

	private static string GetProcessTestHostPath() {
		var targetFrameworkDirectory = new DirectoryInfo(
			AppContext.BaseDirectory
		);
		var configurationDirectory = targetFrameworkDirectory.Parent
			?? throw new InvalidOperationException( "Unable to locate the test configuration directory." );
		var testsDirectory = configurationDirectory.Parent?.Parent?.Parent
			?? throw new InvalidOperationException( "Unable to locate the tests directory." );
		return System.IO.Path.Combine(
			testsDirectory.FullName,
			"ProcessTestHost",
			"bin",
			configurationDirectory.Name,
			targetFrameworkDirectory.Name,
			"Icod.Processes.ProcessTestHost.dll"
		);
	}
}
