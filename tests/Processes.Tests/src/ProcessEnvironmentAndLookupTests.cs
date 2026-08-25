namespace Icod.Processes.Tests;

using Icod.Processes;
using Xunit;

/// <summary>
/// Verifies environment construction and executable lookup contracts.
/// </summary>
public sealed class ProcessEnvironmentAndLookupTests {
	/// <summary>Verifies exact set and remove behavior.</summary>
	[Fact]
	public void EnvironmentBuilderAppliesExplicitChanges() {
		var environment = ProcessEnvironment.CreateEmptyBuilder()
			.Set(
				"ONE",
				"1"
			)
			.Set(
				"TWO",
				"2"
			)
			.Remove(
				"TWO"
			)
			.Build();

		Assert.Equal(
			"1",
			environment.Variables[ "ONE" ]
		);
		Assert.False(
			environment.Variables.ContainsKey( "TWO" )
		);
	}

	/// <summary>Verifies PATH and PATHEXT lookup against an explicit environment.</summary>
	[Fact]
	public void ExecutableLocatorUsesExplicitSearchEnvironment() {
		var directory = System.IO.Path.Combine(
			System.IO.Path.GetTempPath(),
			$"icod-processes-locator-{Guid.NewGuid():N}"
		);
		Directory.CreateDirectory(
			directory
		);
		try {
			var fileName = OperatingSystem.IsWindows()
				? "process-tool.EXE"
				: "process-tool"
			;
			var path = System.IO.Path.Combine(
				directory,
				fileName
			);
			File.WriteAllText(
				path,
				string.Empty
			);
			if ( !OperatingSystem.IsWindows() ) {
				File.SetUnixFileMode(
					path,
					UnixFileMode.UserRead | UnixFileMode.UserExecute
				);
			}
			var builder = ProcessEnvironment.CreateEmptyBuilder().Set(
				"PATH",
				directory
			);
			if ( OperatingSystem.IsWindows() ) {
				builder.Set(
					"PATHEXT",
					".EXE"
				);
			}

			var result = SystemExecutableLocator.Instance.Locate(
				"process-tool",
				builder.Build()
			);

			Assert.True(
				result.Succeeded,
				result.Message
			);
			Assert.Equal(
				System.IO.Path.GetFullPath( path ),
				result.Value
			);
		} finally {
			Directory.Delete(
				directory,
				true
			);
		}
	}

	/// <summary>Verifies that a located but non-executable Unix file maps to cannot-invoke status.</summary>
	[Fact]
	public async Task NonExecutableFileMapsToCannotInvoke() {
		if ( OperatingSystem.IsWindows() ) {
			return;
		}
		var directory = System.IO.Path.Combine(
			System.IO.Path.GetTempPath(),
			$"icod-processes-nonexec-{Guid.NewGuid():N}"
		);
		Directory.CreateDirectory(
			directory
		);
		try {
			var path = System.IO.Path.Combine(
				directory,
				"process-nonexec"
			);
			File.WriteAllText(
				path,
				string.Empty
			);
			File.SetUnixFileMode(
				path,
				UnixFileMode.UserRead | UnixFileMode.UserWrite
			);
			var environment = ProcessEnvironment.CreateEmptyBuilder()
				.Set(
					"PATH",
					directory
				)
				.Build();
			var options = new ProcessRunOptions(
				"process-nonexec"
			) {
				Environment = environment,
				ResolveExecutable = true,
				ReturnLaunchFailureResult = true
			};

			var result = await ProcessRunner.RunAsync(
				options
			);

			Assert.False(
				result.Started
			);
			Assert.Equal(
				ProcessLaunchFailureKind.CannotInvoke,
				result.Termination.LaunchFailureKind
			);
			Assert.Equal(
				126,
				result.Termination.ToPortableExitCode()
			);
		} finally {
			Directory.Delete(
				directory,
				true
			);
		}
	}
}
