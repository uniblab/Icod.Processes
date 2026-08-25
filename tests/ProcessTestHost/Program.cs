namespace Icod.Processes.ProcessTestHost;

using System.Text;

/// <summary>
/// Provides deterministic child-process behaviors used by Icod.Processes.Tests.
/// </summary>
public static class Program {
	/// <summary>Runs the requested test-host behavior.</summary>
	public static async Task<int> Main(
		string[] args
	) {
		ArgumentNullException.ThrowIfNull( args );
		if ( 0 == args.Length ) {
			return 2;
		}

		switch ( args[ 0 ] ) {
			case "args":
				for ( var index = 1; index < args.Length; index++ ) {
					Console.WriteLine(
						string.Concat(
							"B:",
							Convert.ToBase64String(
								Encoding.UTF8.GetBytes(
									args[ index ]
								)
							)
						)
					);
				}
				return 0;

			case "copy":
				await Console.OpenStandardInput().CopyToAsync(
					Console.OpenStandardOutput()
				).ConfigureAwait( false );
				return 0;

			case "stderr":
				await Console.Error.WriteAsync(
					1 < args.Length
						? args[ 1 ]
						: string.Empty
				).ConfigureAwait( false );
				return 0;

			case "exit":
				return 1 < args.Length
					&& int.TryParse(
						args[ 1 ],
						out var exitCode
					)
						? exitCode
						: 0
				;

			case "dual": {
					var count = 1 < args.Length
						&& int.TryParse(
							args[ 1 ],
							out var parsedCount
						)
							? parsedCount
							: 1000
					;
					for ( var index = 0; index < count; index++ ) {
						await Console.Out.WriteLineAsync(
							$"out-{index}"
						).ConfigureAwait( false );
						await Console.Error.WriteLineAsync(
							$"err-{index}"
						).ConfigureAwait( false );
					}
					return 0;
				}

			case "environment":
				Console.Write(
					1 < args.Length
						? Environment.GetEnvironmentVariable( args[ 1 ] )
						: string.Empty
				);
				return 0;

			case "cwd":
				Console.Write(
					Directory.GetCurrentDirectory()
				);
				return 0;

			case "pid":
				Console.Write(
					Environment.ProcessId
				);
				return 0;

			case "sleep":
				await Task.Delay(
					1 < args.Length
						&& int.TryParse(
							args[ 1 ],
							out var milliseconds
						)
							? milliseconds
							: 30000
				).ConfigureAwait( false );
				return 0;

			default:
				return 2;
		}
	}
}
