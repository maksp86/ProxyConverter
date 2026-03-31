using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using ServiceLib.Enums;
using ServiceLib.Handler;
using ServiceLib.Handler.Fmt;
using ServiceLib.Models;
using ServiceLib.Services.CoreConfig;

internal class Program
{
    private static void Main(string[] args)
    {
        if (args.Length == 0 || ContainsHelpFlag(args))
        {
            ShowUsage();
            return;
        }

        // Парсинг флагов (простой ручной парсер)
        string? inputPath = null;
        bool isJsonInput = false;
        string? outputPath = null;
        int startPort = 10808;
        bool changePorts = false;
        var urls = new List<string>();

        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];

            if (arg == "--input-lines")
            {
                if (i + 1 < args.Length)
                {
                    inputPath = args[++i];
                    isJsonInput = false;
                }
            }
            else if (arg == "--input-json")
            {
                if (i + 1 < args.Length)
                {
                    inputPath = args[++i];
                    isJsonInput = true;
                }
            }
            else if (arg == "--output")
            {
                if (i + 1 < args.Length)
                {
                    outputPath = args[++i];
                }
            }
            else if (arg == "--start-port")
            {
                if (i + 1 < args.Length && int.TryParse(args[++i], out var port))
                {
                    startPort = port;
                }
            }
            else if (arg == "--change-ports")
            {
                changePorts = true;
            }
            else if (!arg.StartsWith("-"))
            {
                // позиционные аргументы (URL-ы)
                urls.Add(arg);
            }
        }

        // Загрузка конфигурации один раз (как в оригинале)
        var appConfig = ConfigHandler.LoadConfig();

        // Определяем источник ввода
        bool hasInputFlag = inputPath != null;
        if (hasInputFlag)
        {
            urls.Clear(); // игнорируем позиционные аргументы

            if (inputPath == "-")
            {
                // stdin
                if (isJsonInput)
                {
                    string jsonText = Console.In.ReadToEnd();
                    try
                    {
                        var jsonNode = JsonNode.Parse(jsonText);
                        if (jsonNode is JsonArray arr)
                        {
                            foreach (var item in arr)
                            {
                                if (item?.GetValueKind() == JsonValueKind.String)
                                {
                                    urls.Add(item.GetValue<string>());
                                }
                            }
                        }
                        else
                        {
                            Console.Error.WriteLine("Ошибка: JSON-ввод должен быть массивом строк.");
                            return;
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine($"Ошибка разбора JSON из stdin: {ex.Message}");
                        return;
                    }
                }
                else
                {
                    // lines
                    string? line;
                    while ((line = Console.ReadLine()) != null)
                    {
                        line = line.Trim();
                        if (!string.IsNullOrEmpty(line))
                        {
                            urls.Add(line);
                        }
                    }
                }
            }
            else
            {
                // файл
                if (!File.Exists(inputPath))
                {
                    Console.Error.WriteLine($"Ошибка: файл ввода не найден: {inputPath}");
                    return;
                }

                if (isJsonInput)
                {
                    string jsonText = File.ReadAllText(inputPath);
                    try
                    {
                        var jsonNode = JsonNode.Parse(jsonText);
                        if (jsonNode is JsonArray arr)
                        {
                            foreach (var item in arr)
                            {
                                if (item?.GetValueKind() == JsonValueKind.String)
                                {
                                    urls.Add(item.GetValue<string>());
                                }
                            }
                        }
                        else
                        {
                            Console.Error.WriteLine("Ошибка: JSON-файл должен быть массивом строк.");
                            return;
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine($"Ошибка разбора JSON-файла: {ex.Message}");
                        return;
                    }
                }
                else
                {
                    // lines
                    var lines = File.ReadAllLines(inputPath);
                    foreach (var l in lines)
                    {
                        var trimmed = l.Trim();
                        if (!string.IsNullOrEmpty(trimmed))
                        {
                            urls.Add(trimmed);
                        }
                    }
                }
            }
        }
        else
        {
            // только позиционные аргументы
            if (urls.Count == 0)
            {
                Console.Error.WriteLine("Ошибка: не указаны ссылки. Используйте позиционные аргументы или флаги --input-*.");
                ShowUsage();
                return;
            }
        }

        if (urls.Count == 0)
        {
            // пустой ввод — выводим пустой объект
            var emptyObj = new JsonObject();
            WriteOutput(emptyObj, outputPath);
            return;
        }

        // Основная обработка
        var outputObj = new JsonObject();
        int currentPort = startPort;

        foreach (var url in urls)
        {
            JsonNode? configNode = null;

            try
            {
                var profile = FmtHandler.ResolveConfig(url, out var msg);
                if (profile == null)
                {
                    Console.Error.WriteLine($"Error: {msg}");
                }
                else
                {
                    profile.CoreType = ECoreType.Xray;

                    var context = new CoreConfigContext
                    {
                        Node = profile,
                        RunCoreType = ECoreType.Xray,
                        AppConfig = appConfig
                    };

                    var service = new CoreConfigV2rayService(context);
                    var result = service.GenerateClientConfigContent();

                    string configStr = result.Data.ToString();

                    // Парсим в JsonNode (чтобы можно было изменить порт и потом сериализовать компактно)
                    configNode = JsonNode.Parse(configStr);

                    // Изменяем порт, если включено
                    if (changePorts && configNode is JsonObject configObj)
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
                configNode = null;
            }

            outputObj[url] = configNode;
        }

        WriteOutput(outputObj, outputPath);
    }

    private static bool ContainsHelpFlag(string[] args)
    {
        foreach (var arg in args)
        {
            if (arg == "-h" || arg == "--help")
                return true;
        }
        return false;
    }

    private static void WriteOutput(JsonObject outputObj, string? outputPath)
    {
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

    private static void ShowUsage()
    {
        Console.WriteLine(@"ProxyConverter — конвертер прокси-ссылок (ss://, vless://, hysteria:// и др.) в JSON-конфиги Xray.

Использование:
  ProxyConverter [options] [url1 url2 ...]

Опции:
  -h, --help                    Показать эту справку
  --input-lines <file|- >       Читать ссылки построчно из файла или stdin (-)
  --input-json <file|- >        Читать ссылки как JSON-массив строк из файла или stdin (-)
  --output <file|- >            Вывод JSON в файл или stdout (по умолчанию: -)
  --start-port <int>            Начальный порт для inbounds (по умолчанию: 10808)
  --change-ports                Включить последовательное изменение портов (+1 для каждого успешного конфига)

Если не указаны --input-*, то используются позиционные аргументы как список ссылок.
Вывод всегда в формате компактного JSON:
{
  ""ссылка1"": {полный конфиг Xray или null},
  ""ссылка2"": {полный конфиг Xray или null},
  ...
}

• Порт меняется только при --change-ports (только в inbounds[0].port).
• Ошибки выводятся в stderr, соответствующий ключ получает null.
• result.Data.ToString() автоматически сворачивается в minify-формат при выводе.");
    }
}