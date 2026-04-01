using CommandLine;
using System.Threading.Tasks;

internal class Program
{
	private static async Task Main(string[] args)
	{
		// Parses arguments and displays help automatically if -h/--help is passed
		// On success, invokes the proxy converter logic.
		await Parser.Default.ParseArguments<Options>(args)
			.WithParsedAsync(async options =>
			{
				await ProxyConverter.RunAsync(options);
			});
	}
}