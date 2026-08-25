namespace Icod.Processes.Sample;

using Icod.Processes;

/// <summary>Demonstrates process inspection, executable lookup, and safe child execution.</summary>
public static class Program {
	/// <summary>Runs the sample.</summary>
	public static async Task<int> Main() {
		var identity = SystemProcessInspector.Instance.ObserveIdentity(
			Environment.ProcessId
		);
		if ( identity.Succeeded ) {
			Console.WriteLine( $"Current process: {identity.Value}" );
		}

		var term = ProcessSignalCatalog.Parse( "TERM" );
		if ( term.Succeeded ) {
			Console.WriteLine( $"Portable signal: {term.Value} ({term.Value!.Number})" );
		}

		var dotnet = SystemExecutableLocator.Instance.Locate( "dotnet" );
		if ( !dotnet.Succeeded ) {
			Console.WriteLine( "The dotnet host was not found on PATH; execution sample skipped." );
			return 0;
		}

		var options = new ProcessRunOptions(
			dotnet.Value!
		) {
			CaptureStandardOutput = true,
			CaptureStandardError = true,
			ReturnLaunchFailureResult = true,
			Timeout = TimeSpan.FromSeconds( 10 )
		};
		options.Arguments.Add( "--version" );

		var result = await SystemProcessExecutor.Instance.RunAsync(
			options
		);
		Console.WriteLine( $"Started: {result.Started}" );
		Console.WriteLine( $"Termination: {result.Termination.Kind}" );
		Console.WriteLine( $"Elapsed: {result.Elapsed.TotalMilliseconds:F1} ms" );
		if ( null != result.StandardOutput ) {
			Console.WriteLine( $"dotnet: {result.StandardOutput.Trim()}" );
		}
		if ( null != result.StandardError && 0 != result.StandardError.Length ) {
			Console.Error.WriteLine( result.StandardError );
		}
		return result.Termination.ToPortableExitCode();
	}
}
