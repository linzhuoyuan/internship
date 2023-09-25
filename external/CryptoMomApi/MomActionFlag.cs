
using System.ComponentModel;

namespace MomCrypto.Api
{
	public sealed class MomActionFlag
	{
        private MomActionFlag() { }

        [Description(nameof(Delete))]
        public const byte Delete = (byte)'0';

        [Description(nameof(Modify))]
		public const byte Modify = (byte)'1';
	}
}