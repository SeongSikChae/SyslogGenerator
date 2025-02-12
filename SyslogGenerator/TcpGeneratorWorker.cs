﻿using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading.Atomic;
using System.Threading.PerformanceCounter;
using Quartz;
using Microsoft.Extensions.Logging;

namespace SyslogGenerator
{
	public sealed class TcpGeneratorWorker(int index, Configuration configuration, ILogQueue logQueue, PerformanceCounterProvider performanceCounterProvider, ITaskScheduler taskScheduler) : SyncTask, IGeneratorWorker
	{
		public const string TASK_ID = "TcpGeneratorWorker";

		private const byte SP = 0x20;

		private Socket? socket;
		private NetworkStream? networkStream;
		private BinaryReader? reader;
		private BinaryWriter? writer;

		private Encoding? sendEncoding;

		private AtomicInt64? sendSuccessCounter;
		private AtomicInt64? sendFailCounter;
		private AtomicInt64? sendTotalCounter;
		private AtomicInt64? fileQueueConsumeCounter;

		private Thread? t;

		public void Initialize()
		{
			ArgumentNullException.ThrowIfNull(configuration.SendBufferSize);
			ArgumentNullException.ThrowIfNull(configuration.Port);
			ArgumentNullException.ThrowIfNull(configuration.SEND_ENCODING_CODEPAGE);

			socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
			socket.SendBufferSize = configuration.SendBufferSize.Value;
			socket.Connect(new IPEndPoint(IPAddress.Parse(configuration.Host), configuration.Port.Value));

			networkStream = new NetworkStream(socket);
			reader = new BinaryReaderV2(networkStream, ByteOrder.BigEndian);
			writer = new BinaryWriterV2(networkStream, ByteOrder.BigEndian);

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

					byte[] block = sendEncoding.GetBytes(log);
					try
					{
						writer?.Write(block.Length);
						writer?.Write(SP);
						writer?.Write(block);
						writer?.Flush();

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
				writer?.Close();
				writer?.Dispose();

				reader?.Close();
				reader?.Dispose();

				networkStream?.Close();
				networkStream?.Dispose();

				socket?.Close();
				socket?.Dispose();

				taskScheduler.RemoveTask($"{TASK_ID}-{index}");

				disposedValue = true;
			}
		}
	}
}
