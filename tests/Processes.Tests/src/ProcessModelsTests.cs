namespace Icod.Processes.Tests;

using Icod.Processes;
using Xunit;

/// <summary>
/// Verifies portable process identity, target, signal, and termination models.
/// </summary>
public sealed class ProcessModelsTests {
	/// <summary>Verifies that reuse tokens participate in process identity equality.</summary>
	[Fact]
	public void ProcessIdentityIncludesReuseToken() {
		var first = new ProcessIdentity(
			42,
			new ProcessReuseToken(
				"test",
				"one"
			)
		);
		var same = new ProcessIdentity(
			42,
			new ProcessReuseToken(
				"test",
				"one"
			)
		);
		var reused = new ProcessIdentity(
			42,
			new ProcessReuseToken(
				"test",
				"two"
			)
		);

		Assert.Equal( first, same );
		Assert.NotEqual( first, reused );
	}

	/// <summary>Verifies that targets retain explicit process, group, and session semantics.</summary>
	[Fact]
	public void ProcessTargetsDoNotOverloadIntegerSigns() {
		Assert.Equal(
			ProcessTargetKind.Process,
			ProcessTarget.ForProcess( 12 ).Kind
		);
		Assert.Equal(
			ProcessTargetKind.ProcessGroup,
			ProcessTarget.ForProcessGroup( 12 ).Kind
		);
		Assert.Equal(
			ProcessTargetKind.Session,
			ProcessTarget.ForSession( 12 ).Kind
		);
	}

	/// <summary>Verifies signal names, aliases, numbers, and Linux real-time notation.</summary>
	[Fact]
	public void SignalCatalogParsesNamesAliasesAndNumbers() {
		Assert.Equal(
			15,
			ProcessSignalCatalog.Parse( "SIGTERM" ).Value?.Number
		);
		Assert.Equal(
			6,
			ProcessSignalCatalog.Parse( "IOT" ).Value?.Number
		);
		Assert.Equal(
			9,
			ProcessSignalCatalog.Parse( "9" ).Value?.Number
		);
		if ( OperatingSystem.IsLinux() ) {
			Assert.Equal(
				35,
				ProcessSignalCatalog.Parse( "RTMIN+1" ).Value?.Number
			);
		}
	}

	/// <summary>Verifies portable translation of normal, signal, timeout, and launch outcomes.</summary>
	[Fact]
	public void TerminationTranslatesToPortableExitCodes() {
		Assert.Equal(
			7,
			ProcessTermination.Exited( 7 ).ToPortableExitCode()
		);
		Assert.Equal(
			143,
			ProcessTermination.Signaled(
				new ProcessSignal(
					15,
					"TERM"
				)
			).ToPortableExitCode()
		);
		Assert.Equal(
			124,
			ProcessTermination.TimedOut().ToPortableExitCode()
		);
		Assert.Equal(
			126,
			ProcessTermination.LaunchFailed(
				"denied",
				ProcessLaunchFailureKind.CannotInvoke
			).ToPortableExitCode()
		);
		Assert.Equal(
			127,
			ProcessTermination.LaunchFailed(
				"missing",
				ProcessLaunchFailureKind.NotFound
			).ToPortableExitCode()
		);
	}
}
