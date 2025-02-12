namespace SyslogGenerator
{
	public interface IGeneratorWorker : IDisposable
	{
		void Initialize();
	}	
}
