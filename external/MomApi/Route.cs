using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;

namespace Quantmom.Api
{
    [DataContract]
    public class Route
    {
        [DataMember(Order = 1)]
        public long RouteId { get; set; }
        [DataMember(Order = 2)]
        public bool Enabled { get; set; }
        [DataMember(Order = 3)]
        public List<byte> ProductClasses { get; set; } = new();

        public bool IncludeProductClass(byte value)
        {
            return ProductClasses.Any(item => item == MomProductClassType.All || item == value);
        }

        public void AddProductClass(string name)
        {
            var value = ConstantHelper.GetValue<MomProductClassType>(name);
            if (!ProductClasses.Contains(value))
            {
                ProductClasses.Add(value);
            }
        }

        public string GetProductClasses()
        {
            return ConstantHelper.GetNames<MomProductClassType>(ProductClasses);
        }
    }
}
