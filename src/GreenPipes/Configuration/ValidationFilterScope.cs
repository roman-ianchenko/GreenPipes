﻿// Copyright 2012-2016 Chris Patterson
//  
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use
// this file except in compliance with the License. You may obtain a copy of the 
// License at 
// 
//     http://www.apache.org/licenses/LICENSE-2.0 
// 
// Unless required by applicable law or agreed to in writing, software distributed
// under the License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR 
// CONDITIONS OF ANY KIND, either express or implied. See the License for the 
// specific language governing permissions and limitations under the License.
namespace GreenPipes
{
    using System.Collections.Generic;


    public interface ValidationFilterScope :
        ValidationContext
    {
        /// <summary>
        /// Specifies that the payload is provided by the filter.
        /// </summary>
        /// <typeparam name="T">The payload type</typeparam>
        IEnumerable<ValidationResult> ProvidesPayload<T>()
            where T : class;

        /// <summary>
        /// Specifies that the payload type is required by the filter, and that the filter will
        /// fault if the payload is not present.
        /// </summary>
        /// <typeparam name="T">The payload type</typeparam>
        /// <returns></returns>
        IEnumerable<ValidationResult> RequiresPayload<T>()
            where T : class;
    }
}