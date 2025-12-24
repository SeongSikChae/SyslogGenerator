
using Quartz;
using System.IO;
using System.Collections.Concurrent;
using System.Text;
using System.Threading.Atomic;
using System.Threading.PerformanceCounter;

namespace SyslogGenerator
{
	public interface ILogQueue
	{
		void Initialize();

		void Enqueue(string log);

		bool TryDequeue(out string? log);
	}

	public sealed class FileLogQueue(Program.CmdMain cmdMain, Configuration configuration, PerformanceCounterProvider performanceCounterProvider, ITaskScheduler taskScheduler) : SyncTask, ILogQueue
	{
		public const string TASK_ID = "FileLogQueue";

		private readonly AtomicInt64 readCount = new AtomicInt64();
		private readonly ConcurrentQueue<string> logDataQueue = new ConcurrentQueue<string>();

		private AtomicInt64? fileQueueProduceCounter;
		private Encoding? logFileEncoding;
		private long position;

		public void Initialize()
		{
			ArgumentNullException.ThrowIfNull(configuration.LOG_FILE_ENCODING_CODEPAGE);

			fileQueueProduceCounter = performanceCounterProvider.GetCounter("FileQueueProduce");
			taskScheduler.AddTask(TASK_ID, this, new CronExpression("* * * * * ?"));
			logFileEncoding = Encoding.GetEncoding((int)configuration.LOG_FILE_ENCODING_CODEPAGE.Value);
		}

		public void Enqueue(string log)
		{
			logDataQueue.Enqueue(log);
			fileQueueProduceCounter?.IncrementThenGet();
		}

		public override void Run(CancellationToken cancellationToken)
		{
			ArgumentNullException.ThrowIfNull(configuration.EPS);
			ArgumentNullException.ThrowIfNull(logFileEncoding);

			if (cmdMain.Count.HasValue && cmdMain.Count.Value > 0)
			{
				if (readCount.Value >= cmdMain.Count.Value)
					return;
			}

			int limit = (int)(configuration.EPS.Value * 3);
			if (logDataQueue.Count < limit)
			{
				FileInfo logFileInfo = new FileInfo(configuration.LOG_FILE_PATH);
				using FileStream fileStream = new FileStream(logFileInfo.FullName, FileMode.Open, FileAccess.Read);
				if (fileStream.Length <= position)
					position = 0;
				using BufferedStream bufferedStream = new BufferedStream(fileStream);
				bufferedStream.Seek(position, SeekOrigin.Begin);
				using StreamReader streamReader = new StreamReader(bufferedStream, encoding: logFileEncoding);

				StringBuilder builder = new StringBuilder();
				while (logDataQueue.Count < limit)
				{
					builder.Clear();
					if (cancellationToken.IsCancellationRequested)
						break;

					if (cmdMain.Count.HasValue && cmdMain.Count.Value > 0)
					{
						if (readCount.Value >= cmdMain.Count.Value)
							return;
					}

					string? line = streamReader.ReadLine();
					if (line is null)
					{
						position = 0;
                        bufferedStream.Seek(position, SeekOrigin.Begin);
                        continue;
					}

					if (string.IsNullOrWhiteSpace(line))
					{
                        position = streamReader.GetPosition();
                        continue;
					}

					Enqueue(line);
					if (cmdMain.Count.HasValue && cmdMain.Count.Value > 0)
						readCount.IncrementThenGet();

					if (streamReader.BaseStream.Position >= streamReader.BaseStream.Length)
						position = 0;
					else
                        position = streamReader.GetPosition();
                }
			}
		}

		public bool TryDequeue(out string? log)
		{
			return logDataQueue.TryDequeue(out log);
		}

		public override void Dispose()
		{
			taskScheduler.RemoveTask(TASK_ID);
		}
	}
}
