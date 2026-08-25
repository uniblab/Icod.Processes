namespace Icod.Processes.Tests;

using Icod.Processes;
using Xunit;

/// <summary>Verifies the portable priority-selector model for existing-process priority commands.</summary>
public sealed class ProcessPrioritySelectorTests {
	/// <summary>Verifies zero is retained rather than confused with an invalid process identity.</summary>
	[Theory]
	[InlineData( ProcessPriorityTargetKind.Process )]
	[InlineData( ProcessPriorityTargetKind.ProcessGroup )]
	[InlineData( ProcessPriorityTargetKind.User )]
	public void SelectorTargetsPreserveZero(
		ProcessPriorityTargetKind kind
	) {
		var target = kind switch {
			ProcessPriorityTargetKind.Process => ProcessPriorityTarget.ForProcess( 0 ),
			ProcessPriorityTargetKind.ProcessGroup => ProcessPriorityTarget.ForProcessGroup( 0 ),
			ProcessPriorityTargetKind.User => ProcessPriorityTarget.ForUser( 0 ),
			_ => throw new ArgumentOutOfRangeException( nameof( kind ) )
		};
		Assert.Equal( kind, target.Kind );
		Assert.Equal( 0, target.Identifier );
	}

	/// <summary>Verifies negative selector identifiers are rejected by the shared model.</summary>
	[Fact]
	public void SelectorTargetsRejectNegativeIdentifiers() {
		Assert.Throws<ArgumentOutOfRangeException>( () => ProcessPriorityTarget.ForProcess( -1 ) );
		Assert.Throws<ArgumentOutOfRangeException>( () => ProcessPriorityTarget.ForProcessGroup( -1 ) );
		Assert.Throws<ArgumentOutOfRangeException>( () => ProcessPriorityTarget.ForUser( -1 ) );
	}

	/// <summary>Verifies system selector-zero reads and the controlled Windows group/user boundary.</summary>
	[Theory]
	[InlineData( ProcessPriorityTargetKind.Process )]
	[InlineData( ProcessPriorityTargetKind.ProcessGroup )]
	[InlineData( ProcessPriorityTargetKind.User )]
	public void SystemProviderReportsSelectorZeroSemantics(
		ProcessPriorityTargetKind kind
	) {
		var target = kind switch {
			ProcessPriorityTargetKind.Process => ProcessPriorityTarget.ForProcess( 0 ),
			ProcessPriorityTargetKind.ProcessGroup => ProcessPriorityTarget.ForProcessGroup( 0 ),
			ProcessPriorityTargetKind.User => ProcessPriorityTarget.ForUser( 0 ),
			_ => throw new ArgumentOutOfRangeException( nameof( kind ) )
		};
		var result = SystemProcessPrioritySelectorProvider.Instance.GetPriority( target );
		if ( OperatingSystem.IsWindows() && ProcessPriorityTargetKind.Process != kind ) {
			Assert.Equal( ProcessOperationStatus.Unsupported, result.Status );
			return;
		}
		Assert.True( result.Succeeded, result.Message );
		Assert.InRange( result.Value!.NiceValue, -20, 19 );
	}
}
