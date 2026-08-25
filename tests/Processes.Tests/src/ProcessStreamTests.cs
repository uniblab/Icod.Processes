namespace Icod.Processes.Tests;

using Icod.Processes;
using System.Text;
using Xunit;

/// <summary>Exercises redirected child-process stream forwarding and cancellation.</summary>
public sealed class ProcessStreamTests {
	/// <summary>Verifies standard input can be forwarded and captured exactly.</summary>
	[Fact]
	public async Task ForwardsStandardInputAndCapturesOutput() {
		var payload = Encoding.UTF8.GetBytes(
			new string( 'x', 200000 )
		);
		await using var input = new MemoryStream(
			payload
		);
		var options = ProcessRunnerTests.CreateHostOptions(
			"copy"
		);
		options.StandardInput = input;
		options.CaptureStandardOutput = true;

		var result = await ProcessRunner.RunAsync(
			options
		);

		Assert.Equal( 0, result.ExitCode );
		Assert.Equal(
			payload,
			Encoding.UTF8.GetBytes(
				result.StandardOutput!
			)
		);
	}

	/// <summary>Verifies large standard output and error streams are drained concurrently.</summary>
	[Fact]
	public async Task DrainsLargeStandardOutputAndErrorConcurrently() {
		var options = ProcessRunnerTests.CreateHostOptions(
			"dual",
			"5000"
		);
		options.CaptureStandardOutput = true;
		options.CaptureStandardError = true;

		var result = await ProcessRunner.RunAsync(
			options
		);

		Assert.Equal( 0, result.ExitCode );
		Assert.Contains( "out-4999", result.StandardOutput! );
		Assert.Contains( "err-4999", result.StandardError! );
	}

	/// <summary>Verifies a pre-canceled token never starts the requested child.</summary>
	[Fact]
	public async Task PreCanceledTokenDoesNotStartChild() {
		using var cancellation = new CancellationTokenSource();
		cancellation.Cancel();
		var options = new ProcessRunOptions(
			"an-executable-that-must-not-be-started"
		);

		var result = await ProcessRunner.RunAsync(
			options,
			cancellation.Token
		);

		Assert.True( result.WasCanceled );
		Assert.False( result.Started );
		Assert.Null( result.ExitCode );
	}
}
