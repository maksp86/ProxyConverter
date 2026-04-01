using ServiceLib.Enums;
using ServiceLib.Handler;
using ServiceLib.Handler.Fmt;
using ServiceLib.Models;
using ServiceLib.Services.CoreConfig;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

public static class ProxyConverter
{
	/// <summary>
	/// Main execution pipeline for transforming proxy URLs into Xray configs.
	/// </summary>
	public static async Task RunAsync(Options opts)
	{
		var urls = await GetUrlsAsync(opts);

		if (urls.Count == 0)
		{
			Console.Error.WriteLine("Error: empty input. Use positional arguments or flags --input-*.");
			WriteOutput(new JsonObject(), opts.Output);
			return;
		}

		JsonObject outputObj = new JsonObject();
		int currentPort = opts.StartPort;

		foreach (var url in urls)
		{
			JsonNode? configNode = null;

			try
			{
				Config appConfig = ConfigHandler.LoadConfig();
				ProfileItem? profile = await AddBatchServersCommonStripped(appConfig, url);

				if (profile != null)
				{
					CoreConfigContext context = new CoreConfigContext
					{
						Node = profile,
						RunCoreType = ECoreType.Xray,
						AppConfig = appConfig
					};

					CoreConfigV2rayService service = new CoreConfigV2rayService(context);
					RetResult result = service.GenerateClientConfigContent();

					string configStr = result.Data.ToString();

					// Parse into JsonNode to allow port modification and compact serialization
					configNode = JsonNode.Parse(configStr);

					// Increment and assign ports if enabled
					if (opts.ChangePorts && configNode is JsonObject configObj)
					{
						if (configObj["inbounds"] is JsonArray inbounds &&
							inbounds.Count > 0 &&
							inbounds[0] is JsonObject inbound)
						{
							inbound["port"] = currentPort;
							currentPort++;
						}
					}
				}
			}
			catch (Exception ex)
			{
				Console.Error.WriteLine($"Exception for {url}: {ex.Message}");
				configNode = null; // Ensure null is assigned on failure
			}

			outputObj[url] = configNode;
		}

		WriteOutput(outputObj, opts.Output);
	}

	private static async Task<ProfileItem?> AddBatchServersCommonStripped(Config config, string strData)
	{
		if (string.IsNullOrEmpty(strData)) return null;

		ProfileItem profileItem = FmtHandler.ResolveConfig(strData, out var msg);
		if (profileItem is null) return null;

		profileItem.CoreType = ECoreType.Xray;
		profileItem.Subid = null;
		profileItem.IsSub = false;

		var addStatus = profileItem.ConfigType switch
		{
			EConfigType.VMess => await ConfigHandler.AddVMessServer(config, profileItem, false),
			EConfigType.Shadowsocks => await ConfigHandler.AddShadowsocksServer(config, profileItem, false),
			EConfigType.SOCKS => await ConfigHandler.AddSocksServer(config, profileItem, false),
			EConfigType.Trojan => await ConfigHandler.AddTrojanServer(config, profileItem, false),
			EConfigType.VLESS => await ConfigHandler.AddVlessServer(config, profileItem, false),
			EConfigType.Hysteria2 => await ConfigHandler.AddHysteria2Server(config, profileItem, false),
			EConfigType.TUIC => await ConfigHandler.AddTuicServer(config, profileItem, false),
			EConfigType.WireGuard => await ConfigHandler.AddWireguardServer(config, profileItem, false),
			EConfigType.Anytls => await ConfigHandler.AddAnytlsServer(config, profileItem, false),
			_ => -1,
		};

		return profileItem;
	}

	/// <summary>
	/// Consolidates URL extraction from files, stdin, or CLI arguments.
	/// </summary>
	private static async Task<List<string>> GetUrlsAsync(Options opts)
	{
		var urls = new List<string>();

		// Load positional arguments initially
		if (opts.Urls != null)
		{
			urls.AddRange(opts.Urls);
		}

		// Flags override positional arguments (retaining original logic)
		if (!string.IsNullOrEmpty(opts.InputLines))
		{
			urls.Clear();
			urls.AddRange(await ReadLinesAsync(opts.InputLines));
		}
		else if (!string.IsNullOrEmpty(opts.InputJson))
		{
			urls.Clear();
			urls.AddRange(await ReadJsonArrayAsync(opts.InputJson));
		}

		// Return only non-empty lines
		return urls.Where(u => !string.IsNullOrWhiteSpace(u)).ToList();
	}

	private static async Task<IEnumerable<string>> ReadLinesAsync(string path)
	{
		var lines = new List<string>();

		if (path == "-")
		{
			string? line;
			while ((line = await Console.In.ReadLineAsync()) != null)
			{
				lines.Add(line.Trim());
			}
		}
		else
		{
			if (!File.Exists(path))
			{
				Console.Error.WriteLine($"Error: input file does not exist: {path}");
				return lines;
			}
			var fileLines = await File.ReadAllLinesAsync(path);
			lines.AddRange(fileLines.Select(l => l.Trim()));
		}

		return lines;
	}

	private static async Task<IEnumerable<string>> ReadJsonArrayAsync(string path)
	{
		var urls = new List<string>();
		string jsonText;

		if (path == "-")
		{
			jsonText = await Console.In.ReadToEndAsync();
		}
		else
		{
			if (!File.Exists(path))
			{
				Console.Error.WriteLine($"Error: input file does not exist: {path}");
				return urls;
			}
			jsonText = await File.ReadAllTextAsync(path);
		}

		try
		{
			var jsonNode = JsonNode.Parse(jsonText);
			if (jsonNode is JsonArray arr)
			{
				urls.AddRange(arr
					.Where(item => item?.GetValueKind() == JsonValueKind.String)
					.Select(item => item!.GetValue<string>()));
			}
			else
			{
				Console.Error.WriteLine("Error: JSON-input must be a string array.");
			}
		}
		catch (Exception ex)
		{
			Console.Error.WriteLine($"Error while parsing JSON: {ex.Message}");
		}

		return urls;
	}

	private static void WriteOutput(JsonObject outputObj, string? outputPath)
	{
		// WriteIndented = false ensures minified output as required
		var options = new JsonSerializerOptions { WriteIndented = false };
		string jsonOutput = outputObj.ToJsonString(options);

		if (string.IsNullOrEmpty(outputPath) || outputPath == "-")
		{
			Console.WriteLine(jsonOutput);
		}
		else
		{
			File.WriteAllText(outputPath, jsonOutput);
		}
	}
}