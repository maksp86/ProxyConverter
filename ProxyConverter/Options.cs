using CommandLine;
using System.Collections.Generic;

/// <summary>
/// CLI Options definition for ProxyConverter.
/// CommandLineParser uses these attributes to automatically generate the --help text.
/// </summary>
public class Options
{
	[Option("input-lines", HelpText = "Read links line-by-line from a file or stdin (-).")]
	public string? InputLines { get; set; }

	[Option("input-json", HelpText = "Read links as a JSON string array from a file or stdin (-).")]
	public string? InputJson { get; set; }

	[Option('o', "output", Default = "-", HelpText = "Output JSON to a file or stdout (-).")]
	public string Output { get; set; } = "-";

	[Option("start-port", Default = 10808, HelpText = "Starting port for Xray inbounds.")]
	public int StartPort { get; set; }

	[Option("change-ports", Default = false, HelpText = "Enable sequential port changing (+1 for each successful config).")]
	public bool ChangePorts { get; set; }

	[Value(0, MetaName = "urls", HelpText = "Positional arguments for proxy URLs (used if --input flags are omitted).")]
	public IEnumerable<string>? Urls { get; set; }
}