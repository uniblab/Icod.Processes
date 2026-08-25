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
		var managedHost = GetProcessTestHostPath();
		var hostDirectory = System.IO.Path.GetDirectoryName(
			managedHost
		) ?? throw new InvalidOperationException( "Unable to locate the process test host directory." );
		var host = System.IO.Path.Combine(
			hostDirectory,
			"Icod.Processes.ProcessTestHost"
		);
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
