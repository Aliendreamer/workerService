using System;
using System.IO;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;
using System.Runtime.InteropServices;

namespace PhotoUploader
{
    class ConfigurationStarter
    {
        private static string _serialogConstant = "logfile";
        private static string _settingsFile = "appsettings.json";
        private static string _successMessage = "PhotoUploader started";
        private static string _failMessage = "{0} It didn't start correctly";
        public static void StartApp(string []args)
        {

            if(RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {


            }
            var configuration = new ConfigurationBuilder()
           //.AddJsonFile ("appsettings.json", false, reloadOnChange : true)
            .AddJsonFile ("appsettings.Linux.Development.json", true, reloadOnChange : true)
            .AddEnvironmentVariables()
            .Build();
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                .MinimumLevel.Override("Microsoft",LogEventLevel.Information)
                .Enrich.FromLogContext()
                .WriteTo.File(configuration.GetValue<string>(_serialogConstant))
                .WriteTo.Console()
                .CreateLogger();
            try
            {
                Log.Information(_successMessage);
                CreateHostBuilder(args).Build().Run();
            }
            catch (Exception e)
            {
                Log.Fatal(string.Format(_failMessage,e.Message));
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }

         private static IHostBuilder CreateHostBuilder(string[] args) =>
            new HostBuilder()
                .ConfigureHostConfiguration(configHost =>
                {
                    configHost.SetBasePath(Directory.GetCurrentDirectory());
                    configHost.AddJsonFile(_settingsFile, optional: true);
                    configHost.AddCommandLine(args);
                })
                .ConfigureAppConfiguration((hostContext, configApp) =>
                {
                    configApp.SetBasePath(Directory.GetCurrentDirectory());
                    configApp.AddJsonFile(_settingsFile, optional: true);
                    configApp.AddCommandLine(args);
                })
                .UseSystemd()
                //.UseWindowsService()
                .UseSerilog()
                .ConfigureServices((hostContext, services) => services.AddHostedService<Worker>());
    }
}
