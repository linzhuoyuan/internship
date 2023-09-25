/*
* QUANTCONNECT.COM - Democratizing Finance, Empowering Individuals.
* Lean Algorithmic Trading Engine v2.0. Copyright 2014 QuantConnect Corporation.
*
* Licensed under the Apache License, Version 2.0 (the "License");
* you may not use this file except in compliance with the License.
* You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
*
* Unless required by applicable law or agreed to in writing, software
* distributed under the License is distributed on an "AS IS" BASIS,
* WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
* See the License for the specific language governing permissions and
* limitations under the License.
*/

using NUnit.Framework;
using QuantConnect.AlgorithmFactory;
using System;
using System.Linq;
using QuantConnect.Util;

namespace QuantConnect.Tests.AlgorithmFactory
{
    [TestFixture]
    public class LoaderTests
    {
        private WorkerThread _workerThread;

        [SetUp]
        public void SetUp()
        {
            _workerThread = new WorkerThread();
        }

        [TearDown]
        public void TearDown()
        {
            _workerThread.Dispose();
        }

        [Test, Ignore]
        public void LoadsSamePythonAlgorithmTwice()
        {
            var assemblyPath = "../../../Algorithm.Python/BasicTemplateAlgorithm.py";

            var one = new Loader(Language.Python, TimeSpan.FromMinutes(1), names => names.SingleOrDefault(), _workerThread)
                .TryCreateAlgorithmInstanceWithIsolator(assemblyPath, 512, out var algorithm1, out var error1);

            var two = new Loader(Language.Python, TimeSpan.FromMinutes(1), names => names.SingleOrDefault(), _workerThread)
                .TryCreateAlgorithmInstanceWithIsolator(assemblyPath, 512, out var algorithm2, out var error2);

            Assert.AreNotEqual(algorithm1.ToString(), algorithm2.ToString());
        }

        [Test, Ignore]
        public void LoadsTwoDifferentPythonAlgorithm()
        {
            var assemblyPath1 = "../../../Algorithm.Python/BasicTemplateAlgorithm.py";
            var assemblyPath2 = "../../../Algorithm.Python/AddRemoveSecurityRegressionAlgorithm.py";

            var one = new Loader(Language.Python, TimeSpan.FromMinutes(1), names => names.SingleOrDefault(), _workerThread)
                .TryCreateAlgorithmInstanceWithIsolator(assemblyPath1, 512, out var algorithm1, out var error1);

            var two = new Loader(Language.Python, TimeSpan.FromMinutes(1), names => names.SingleOrDefault(), _workerThread)
                .TryCreateAlgorithmInstanceWithIsolator(assemblyPath2, 512, out var algorithm2, out var error2);

            Assert.AreNotEqual(algorithm1.ToString(), algorithm2.ToString());
        }

        [Test]
        public void LoadsAlgorithm_UsingSingleOrAlgorithmTypeName_ExtensionMethod()
        {
            var assemblyPath1 = "QuantConnect.Algorithm.CSharp.dll";

            var one = new Loader(Language.CSharp, TimeSpan.FromMinutes(1), names => names.SingleOrAlgorithmTypeName("BasicTemplateAlgorithm"), _workerThread)
                .TryCreateAlgorithmInstanceWithIsolator(assemblyPath1, 512, out var algorithm1, out var error1);

            Assert.IsTrue(one);
        }

        [Test]
        public void LoadsSeparateAlgorithm_UsingSingleOrAlgorithmTypeName_ExtensionMethod()
        {
            var assemblyPath = "QuantConnect.Algorithm.CSharp.dll";

            var one = new Loader(
                    Language.CSharp, 
                    TimeSpan.FromMinutes(1), 
                    names => 
                        names.SingleOrAlgorithmTypeName("BasicTemplateAlgorithm"), 
                    _workerThread)
                .TryCreateAlgorithmInstanceWithIsolator(
                    assemblyPath, 512, out var algorithm1, out var error1);

            var two = new Loader(
                    Language.CSharp, 
                    TimeSpan.FromMinutes(1), 
                    names => 
                        names.SingleOrAlgorithmTypeName("BasicTemplateForexAlgorithm"), _workerThread)
                .TryCreateAlgorithmInstanceWithIsolator(
                    assemblyPath, 512, out var algorithm2, out var error2);

            Assert.IsTrue(one);
            Assert.IsTrue(two);
            Assert.AreNotEqual(algorithm1.ToString(), algorithm2.ToString());
        }
    }
}