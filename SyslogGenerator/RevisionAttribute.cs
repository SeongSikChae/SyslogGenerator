using System.Revision;

namespace SyslogGenerator
{
	[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false, Inherited = false)]
	public class RevisionAttribute(string revision) : Attribute, IRevision
	{
		public string Revision { get; } = revision;
	}
}
