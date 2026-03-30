using System;
using ServiceLib.Enums;
using ServiceLib.Handler;          // ConfigHandler, CoreConfigV2rayService и т.п.
using ServiceLib.Handler.Fmt;
using ServiceLib.Models;           // ProfileItem, Config и т.д.
using ServiceLib.Services.CoreConfig;

internal class Program
{
    private static void Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("Usage: ProxyConverter <url1> [url2] ...");
            return;
        }

        var config = ConfigHandler.LoadConfig(); // дефолтный конфиг V2RayN

        foreach (var url in args)
        {
            try
            {
                var profile = FmtHandler.ResolveConfig(url, out var msg);
                if (profile == null)
                {
                    Console.Error.WriteLine($"Error: {msg}");
                    continue;
                }

                profile.CoreType = ECoreType.Xray;

                var context = new CoreConfigContext
                {
                    Node = profile,
                    RunCoreType = ECoreType.Xray,
                    AppConfig = config
                };

                var service = new CoreConfigV2rayService(context);
                var result = service.GenerateClientConfigContent();

                // Выводим только JSON (удобно для Python)
                Console.WriteLine(result.Data.ToString());
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Exception for {url}: {ex.Message}");
            }
        }
    }
}
