using System;

namespace QuantConnect.Algorithm.CSharp.LiveStrategy.DataType
{
    public class Underlying : IEquatable<Underlying>
    {
        public string CoinPair { get;}
        public bool HasCrypto { get;  set;}
        public bool HasFutures { get;  set;}


        public Underlying(string coinPair, bool hasCrypto, bool hasFuturues)
        {
            CoinPair = coinPair;
            HasCrypto = hasCrypto;
            HasFutures = hasFuturues;
        }

        public override bool Equals(object obj)
        {
            if (obj == null)
                return false;

            Underlying underlyingObj = obj as Underlying;
            if (underlyingObj == null)
                return false;
            else
                return Equals(underlyingObj);
        }

        public bool Equals(Underlying that)
        {
            return CoinPair == that.CoinPair && HasCrypto == that.HasCrypto 
            && HasFutures == that.HasFutures;
        }

        public static bool operator == (Underlying person1, Underlying person2)
        {
            if (((object)person1) == null || ((object)person2) == null)
                return Object.Equals(person1, person2);

            return person1.Equals(person2);
        }

        public static bool operator != (Underlying person1, Underlying person2)
        {
            if (((object)person1) == null || ((object)person2) == null)
                return ! Object.Equals(person1, person2);

            return ! (person1.Equals(person2));
        }

        public override string ToString()
        {
            var crypto = HasCrypto ? "HasCrypto" : "NoCrypto";
            var futures = HasFutures ? "HasPerpetual" : "NoPerpetual";
            return $"{CoinPair}-{crypto}-{futures}";
        }
    }
}
