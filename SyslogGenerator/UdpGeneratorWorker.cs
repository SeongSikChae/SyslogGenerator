using Quartz;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading.Atomic;
using System.Threading.PerformanceCounter;

namespace SyslogGenerator
{
	public sealed class UdpGeneratorWorker(int index, Configuration configuration, ILogQueue logQueue, PerformanceCounterProvider performanceCounterProvider, ITaskScheduler taskScheduler) : SyncTask, IGeneratorWorker
	{
		public const string TASK_ID = "UdpGeneratorWorker";

		private Socket? socket;
		private EndPoint? endPoint;
		private Encoding? sendEncoding;

		private AtomicInt64? sendSuccessCounter;
		private AtomicInt64? sendFailCounter;
		private AtomicInt64? sendTotalCounter;
		private AtomicInt64? fileQueueConsumeCounter;

		public void Initialize()
		{
			ArgumentNullException.ThrowIfNull(configuration.SendBufferSize);
			ArgumentNullException.ThrowIfNull(configuration.Port);
			ArgumentNullException.ThrowIfNull(configuration.SEND_ENCODING_CODEPAGE);

			socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
			socket.SendBufferSize = configuration.SendBufferSize.Value;
			socket.Blocking = false;

			endPoint = new IPEndPoint(IPAddress.Parse(configuration.Host), configuration.Port.Value);

			sendEncoding = Encoding.GetEncoding((int)configuration.SEND_ENCODING_CODEPAGE.Value);

			sendSuccessCounter = performanceCounterProvider.GetCounter("SendSuccess");
			sendFailCounter = performanceCounterProvider.GetCounter("SendFail");
			sendTotalCounter = performanceCounterProvider.GetCounter("SendTotal");
			fileQueueConsumeCounter = performanceCounterProvider.GetCounter("FileQueueConsume");

			taskScheduler.AddTask($"{TASK_ID}-{index}", this, new CronExpression("* * * * * ?"));
		}

		public override void Run(CancellationToken cancellationToken)
		{
			ArgumentNullException.ThrowIfNull(configuration.EPS);

			for (int i = 0; i < configuration.EPS.Value; i++)
			{
				if (cancellationToken.IsCancellationRequested)
					return;

				if (logQueue.TryDequeue(out string? log))
				{
					if (string.IsNullOrWhiteSpace(log))
					{
						i--;
						continue;
					}

					fileQueueConsumeCounter?.IncrementThenGet();

					ArgumentNullException.ThrowIfNull(sendEncoding);
					ArgumentNullException.ThrowIfNull(endPoint);

					byte[] block = sendEncoding.GetBytes(log);
					try
					{
						socket?.SendTo(block, endPoint);
						sendSuccessCounter?.IncrementThenGet();
					}
					catch (Exception)
					{
						logQueue.Enqueue(log);
						sendFailCounter?.IncrementThenGet();
					}
					finally
					{
						sendTotalCounter?.IncrementThenGet();
					}
				}
			}
		}

		private bool disposedValue = false;

		public override void Dispose()
		{
			if (!disposedValue)
			{
				socket?.Close();
				socket?.Dispose();
				taskScheduler.RemoveTask($"{TASK_ID}-{index}");
				disposedValue = true;
			}
		}
	}
}
