using System.Runtime.Serialization;

namespace MomCrypto.Api
{
	[DataContract]
	public class MomSpecificInstrument
	{
		[DataMember(Order = 1)]
		public string InstrumentId;
	}
}
