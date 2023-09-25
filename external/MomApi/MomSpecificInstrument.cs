using System.Runtime.Serialization;

namespace Quantmom.Api
{
	[DataContract]
	public class MomSpecificInstrument
	{
		[DataMember(Order = 1)]
		public string InstrumentId = string.Empty;
	}
}
