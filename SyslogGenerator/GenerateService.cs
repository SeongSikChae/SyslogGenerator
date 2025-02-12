using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Threading.PerformanceCounter;

namespace SyslogGenerator
{
	internal class GenerateService(Configuration configuration, ILogQueue queue, PerformanceLogger performanceLogger, PerformanceCounterProvider performanceCounterProvider, ITaskScheduler taskScheduler) : IHostedService, IHostedLifecycleService
	{
		private readonly List<IGeneratorWorker> workers = new List<IGeneratorWorker>();

		public Task StartingAsync(CancellationToken cancellationToken)
		{
			performanceLogger.Initialize(["SendSuccess", "SendFail", "SendTotal", "FileQueueProduce", "FileQueueConsume"], TimeSpan.FromSeconds(10));

			queue.Initialize();

			return Task.CompletedTask;
		}

		public Task StartAsync(CancellationToken cancellationToken)
		{
			ArgumentNullException.ThrowIfNull(configuration.SenderCount);

			for (int index = 0; index < configuration.SenderCount.Value; index++)
			{
				IGeneratorWorker worker;
				switch (configuration.Mode)
				{
					case Mode.TCP:
						worker = new TcpGeneratorWorker(index, configuration, queue, performanceCounterProvider, taskScheduler);
						break;
					default:
						worker = new UdpGeneratorWorker(index, configuration, queue, performanceCounterProvider, taskScheduler);
						break;
				}

				worker.Initialize();
				workers.Add(worker);
			}

			return Task.CompletedTask;
		}

		public Task StartedAsync(CancellationToken cancellationToken)
		{
			return Task.CompletedTask;
		}

		public Task StoppingAsync(CancellationToken cancellationToken)
		{
			foreach (IGeneratorWorker worker in workers)
				worker.Dispose();
			return Task.CompletedTask;
		}

		public Task StopAsync(CancellationToken cancellationToken)
		{
			return Task.CompletedTask;
		}

		public Task StoppedAsync(CancellationToken cancellationToken)
		{
			return Task.CompletedTask;
		}
	}
}
