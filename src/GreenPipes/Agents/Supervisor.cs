﻿// Copyright 2012-2018 Chris Patterson
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
namespace GreenPipes.Agents
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Internals.Extensions;
    using Util;


    /// <summary>
    /// Supervises a set of agents, allowing for graceful Start, Stop, and Ready state management
    /// </summary>
    public class Supervisor :
        Agent,
        ISupervisor
    {
        readonly Dictionary<long, IAgent> _agents;
        long _nextId;
        int _peakActiveCount;
        long _totalCount;

        /// <summary>
        /// Creates a Supervisor
        /// </summary>
        public Supervisor()
        {
            _agents = new Dictionary<long, IAgent>();
        }

        /// <inheritdoc />
        public void Add(IAgent agent)
        {
            if (IsStopping)
                throw new OperationCanceledException("The agent is stopped or has been stopped, no additional provocateurs can be created.");

            var id = Interlocked.Increment(ref _nextId);
            lock (_agents)
            {
                _agents.Add(id, agent);

                _totalCount++;
                var currentActiveCount = _agents.Count;

                if (currentActiveCount > _peakActiveCount)
                    _peakActiveCount = currentActiveCount;

                if (!IsAlreadyReady)
                    SetReady(Task.WhenAll(_agents.Values.Select(x => x.Ready).ToArray()));
            }

            void RemoveAgent(Task task)
            {
                Remove(id);
            }

            agent.Completed.ContinueWith(RemoveAgent, TaskScheduler.Default);
        }

        /// <inheritdoc />
        public int PeakActiveCount => _peakActiveCount;

        /// <inheritdoc />
        public long TotalCount => _totalCount;

        /// <inheritdoc />
        public override void SetReady()
        {
            if (!IsAlreadyReady)
                lock (_agents)
                {
                    if (_agents.Count == 0)
                        SetReady(TaskUtil.Completed);
                    else
                        SetReady(Task.WhenAll(_agents.Values.Select(x => x.Ready).ToArray()));
                }
        }

        /// <inheritdoc />
        protected override Task StopAgent(StopContext context)
        {
            IAgent[] agents;
            lock (_agents)
            {
                if (_agents.Count == 0)
                    agents = new IAgent[0];
                else
                    agents = _agents.Values.Where(x => !x.Completed.IsCompleted).ToArray();
            }

            return StopSupervisor(new Context(context, agents));
        }

        protected virtual async Task StopSupervisor(StopSupervisorContext context)
        {
            if (context.Agents.Length == 0)
            {
                SetCompleted(TaskUtil.Completed);
            }
            if (context.Agents.Length == 1)
            {
                SetCompleted(context.Agents[0].Completed);

                await context.Agents[0].Stop(context).UntilCompletedOrCanceled(context.CancellationToken).ConfigureAwait(false);
            }
            else if (context.Agents.Length > 1)
            {
                Task[] completedTasks = new Task[context.Agents.Length];
                for (int i = 0; i < context.Agents.Length; i++)
                {
                    completedTasks[i] = context.Agents[i].Completed;
                }

                SetCompleted(Task.WhenAll(completedTasks));

                Task[] stopTasks = new Task[context.Agents.Length];
                for (int i = 0; i < context.Agents.Length; i++)
                {
                    stopTasks[i] = context.Agents[i].Stop(context);
                }

                await Task.WhenAll(stopTasks).UntilCompletedOrCanceled(context.CancellationToken).ConfigureAwait(false);
            }

            await Completed.UntilCompletedOrCanceled(context.CancellationToken).ConfigureAwait(false);
        }

        void Remove(long id)
        {
            lock (_agents)
            {
                _agents.Remove(id);
            }
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return "Supervisor";
        }


        class Context :
            StopSupervisorContext
        {
            readonly StopContext _context;

            public Context(StopContext context, IAgent[] agents)
            {
                _context = context;
                Agents = agents;
            }

            bool PipeContext.HasPayloadType(Type payloadType)
            {
                return _context.HasPayloadType(payloadType);
            }

            bool PipeContext.TryGetPayload<TPayload>(out TPayload payload)
            {
                return _context.TryGetPayload(out payload);
            }

            TPayload PipeContext.GetOrAddPayload<TPayload>(PayloadFactory<TPayload> payloadFactory)
            {
                return _context.GetOrAddPayload(payloadFactory);
            }

            T PipeContext.AddOrUpdatePayload<T>(PayloadFactory<T> addFactory, UpdatePayloadFactory<T> updateFactory)
            {
                return _context.AddOrUpdatePayload(addFactory, updateFactory);
            }

            CancellationToken PipeContext.CancellationToken => _context.CancellationToken;

            string StopContext.Reason => _context.Reason;

            public IAgent[] Agents { get; }
        }
    }
}