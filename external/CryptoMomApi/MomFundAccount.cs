using System.Runtime.Serialization;

namespace MomCrypto.Api
{
    [DataContract]
    public class MomFundAccount : AccountField
    {
        public override string ToString()
        {
            return $"{AccountId},{AccountName},{AccountType},{PreBalance},{Available}";
        }

        public MomFundAccount Clone()
        {
            return (MomFundAccount)MemberwiseClone();
        }
    }
}
