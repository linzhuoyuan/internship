using System.ComponentModel;

namespace Quantmom.Api
{
	public static class MomOptionsTypeType
	{
        [Description("Call")]
		public const byte CallOptions = 49;
        [Description("Put")]
		public const byte PutOptions = 50;
	}
}