﻿/*
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

using System;
using System.Collections.Generic;

namespace QuantConnect.Interfaces
{
    /// <summary>
    /// Provides the full crypto chain for a given underlying.
    /// </summary>
    public interface ICryptoChainProvider
    {
        IEnumerable<Symbol> GetCryptoContractList(Symbol symbol, DateTime date);
    }

    /// <summary>
    /// Provides the full future chain for a given underlying.
    /// </summary>
    public interface IFutureChainProvider
    {
        /// <summary>
        /// Gets the list of future contracts for a given underlying symbol
        /// </summary>
        /// <param name="symbol">The underlying symbol</param>
        /// <param name="date">The date for which to request the future chain (only used in backtesting)</param>
        /// <returns>The list of future contracts</returns>
        IEnumerable<Symbol> GetFutureContractList(Symbol symbol, DateTime date);
    }
}
