﻿using NUnit.Framework;
using QuantConnect.Lean.Engine.DataFeeds;

namespace QuantConnect.Tests.Engine.DataCacheProviders
{
    [TestFixture]
    public class SingleEntryDataCacheProviderTests
    {
        private SingleEntryDataCacheProvider _singleEntryDataCacheProvider;

        [OneTimeSetUp]
        public void Setup()
        {
            _singleEntryDataCacheProvider = new SingleEntryDataCacheProvider(new DefaultDataProvider());
        }

        [Test]
        public void SingleEntryDataCache_CanFetchDataThatExists()
        {
            var stream = _singleEntryDataCacheProvider.Fetch("../../../../Data/equity/usa/minute/aapl/20140606_trade.zip");

            Assert.IsNotNull(stream);
        }

        [Test]
        public void SingleEntryDataCache_CannotFetchDataThatDoesNotExist()
        {
            var stream = _singleEntryDataCacheProvider.Fetch("../../../../Data/equity/usa/minute/aapl/19980606_trade.zip");

            Assert.IsNull(stream);
        }
    }
}
