using NUnit.Framework;

namespace QuantConnect.Tests
{
    [TestFixture]
    public class ExtensionsBenchmark
    {
        /*
        [Test]
        public void ToDecimalTest()
        {
            const int count = 100000;
            var rand = new Random();
            var list = new List<string>(count);
            var expected = new List<decimal>(count);
            var actual = new List<decimal>(count);
            for (var i = 0; i < count; i++)
            {
                var num = (decimal)rand.NextDouble() * rand.Next(1000);
                expected.Add(num);
                list.Add(num.ToString(CultureInfo.InvariantCulture));
            }

            actual.Clear();
            var sw = Stopwatch.StartNew();
            foreach (var str in list)
            {
                actual.Add(str.ToDecimalOld());
            }
            sw.Stop();
            var time1 = sw.ElapsedTicks;
            Log.Trace($"ToDecimal: {time1}");

            for (var i = 0; i < count; i++)
            {
                Assert.AreEqual(expected[i], actual[i]);
            }

            actual.Clear();
            sw.Restart();
            foreach (var str in list)
            {
                actual.Add(str.ParseDecimal());
            }
            sw.Stop();
            var time2 = sw.ElapsedTicks;
            Log.Trace($"ParseDecimal: {time2}");
            
            for (var i = 0; i < count; i++)
            {
                Assert.AreEqual(expected[i], actual[i]);
            }

            Assert.Greater(time1, time2);
        }*/
    }
}
