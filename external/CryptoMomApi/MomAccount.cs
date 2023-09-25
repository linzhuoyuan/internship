using System.Runtime.Serialization;

namespace MomCrypto.Api
{
    [DataContract]
    public class MomAccount : AccountField
    {
        public override string ToString()
        {
            return $"{AccountId},{AccountName},{AccountType},{PreBalance},{Available}";
        }

        public MomAccount Clone()
        {
            return (MomAccount)MemberwiseClone();
        }
    }
}
