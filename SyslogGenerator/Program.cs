using CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Configuration;
using System.Configuration;
using System.Revision;
using System.Text;
using System.Threading.PerformanceCounter;

namespace SyslogGenerator
{
    public static class Program
    {
        public sealed class CmdMain
        {
            [Option("config", Required = true, HelpText = "config file path")]
            public string ConfigFilePath { get; set; } = null!;

			[Option("count", Required = false, HelpText = "send count")]
			public uint? Count { get; set; }
		}

        static async Task Main(string[] args)
        {
			Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            ParserResult<CmdMain> result = await Parser.Default.ParseArguments<CmdMain>(args).WithParsedAsync(async cmdMain =>
            {
                HostApplicationBuilder builder = CreateApplicationHostBuilder(cmdMain, args);
                IHost host = builder.Build();
                await host.RunAsync();
            });

            await result.WithNotParsedAsync(async errors =>
            {
                if (errors.IsVersion())
                    errors.Output().WriteLine(RevisionUtil.GetRevision<RevisionAttribute>());
                await Task.CompletedTask;
            });
        }

        public static HostApplicationBuilder CreateApplicationHostBuilder(CmdMain cmdMain, string[] args)
        {
            YamlDotNet.Serialization.Deserializer deserializer = new YamlDotNet.Serialization.Deserializer();
            Configuration configuration = deserializer.Deserialize<Configuration>(File.ReadAllText(cmdMain.ConfigFilePath));
            ConfigurationValidator.Validate(configuration);
            return CreateApplicationHostBuilder(cmdMain, configuration, args);
        }

        public static HostApplicationBuilder CreateApplicationHostBuilder(CmdMain cmd, Configuration configuration, string[] args)
        {
            HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

            builder.Logging.Services.AddSerilog(configure =>
            {
                configure.Enrich.WithCaller().WriteTo.Console(Serilog.Events.LogEventLevel.Information, CallerEnricherOutputTemplate.Default);
            });
            builder.Services.AddSingleton(cmd);
            builder.Services.AddSingleton(configuration);
            builder.Services.AddSingleton<ITaskScheduler, DefaultTaskScheduler>();
            builder.Services.AddSingleton<PerformanceCounterProvider>();
            builder.Services.AddSingleton<IPerformaceLogListener, DefaultPerformaceLogListener>();
            builder.Services.AddSingleton<PerformanceLogger>();
            builder.Services.AddSingleton<ILogQueue, FileLogQueue>();
            builder.Services.AddHostedService<GenerateService>();

            return builder;
        }

	}
}
