using System.Configuration.Annotation;

namespace SyslogGenerator
{
	public sealed class Configuration
	{
		[Property(PropertyType.ENUM, DefaultValue = "UDP")]
		public Mode? Mode { get; set; }

		[Property(PropertyType.STRING, required: true)]
		public string Host { get; set; } = null!;

		[Property(PropertyType.USHORT, required: true)]
		public ushort? Port { get; set; }

		[Property(PropertyType.UINT, required: true)]
		public uint? EPS { get; set; }

		[Property(PropertyType.UINT, DefaultValue = "1")]
		public uint? Partition { get; set; }

		[Property(PropertyType.STRING, required: true)]
		public string LOG_FILE_PATH { get; set; } = null!;

		[Property(PropertyType.INT, DefaultValue = "65535")]
		public int? SendBufferSize { get; set; }

		[Property(PropertyType.UINT, DefaultValue = "65001")]
		public uint? LOG_FILE_ENCODING_CODEPAGE { get; set; }

		[Property(PropertyType.UINT, DefaultValue = "65001")]
		public uint? SEND_ENCODING_CODEPAGE { get; set; }
	}

	public enum Mode
	{
		UDP, TCP
	}
}
