﻿// *********************************************************************
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License
// *********************************************************************
using System;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using Microsoft.StreamProcessing.Aggregates;
using Microsoft.StreamProcessing.Internal;
using Microsoft.StreamProcessing.Internal.Collections;

namespace Microsoft.StreamProcessing
{
    /// <summary>
    /// Operator for tumbling windows, has no support for ECQ
    /// </summary>
    [DataContract]
    internal sealed class SnapshotWindowTumblingPipeSimple<TInput, TState, TOutput> : UnaryPipe<Empty, TInput, TOutput>
    {
        private static readonly bool hasDisposableState = typeof(IDisposable).GetTypeInfo().IsAssignableFrom(typeof(TState));
        private readonly MemoryPool<Empty, TOutput> pool;
        private readonly string errorMessages;
        private readonly IAggregate<TInput, TState, TOutput> aggregate;

        [SchemaSerialization]
        private readonly long hop;
        [SchemaSerialization]
        private readonly Expression<Func<TState>> initialStateExpr;
        private readonly Func<TState> initialState;
        [SchemaSerialization]
        private readonly Expression<Func<TState, long, TInput, TState>> accumulateExpr;
        private readonly Func<TState, long, TInput, TState> accumulate;
        [SchemaSerialization]
        private readonly Expression<Func<TState, TOutput>> computeResultExpr;
        private readonly Func<TState, TOutput> computeResult;

        [DataMember]
        private StreamMessage<Empty, TOutput> batch;

        [DataMember]
        private long lastSyncTime = long.MinValue;
        [DataMember]
        private HeldState<TState> currentState;

        [Obsolete("Used only by serialization. Do not call directly.")]
        public SnapshotWindowTumblingPipeSimple() { }

        public SnapshotWindowTumblingPipeSimple(
            SnapshotWindowStreamable<Empty, TInput, TState, TOutput> stream,
            IStreamObserver<Empty, TOutput> observer, long hop)
            : base(stream, observer)
        {
            this.aggregate = stream.Aggregate;
            this.initialStateExpr = this.aggregate.InitialState();
            this.initialState = this.initialStateExpr.Compile();
            this.accumulateExpr = this.aggregate.Accumulate();
            this.accumulate = this.accumulateExpr.Compile();
            this.computeResultExpr = this.aggregate.ComputeResult();
            this.computeResult = this.computeResultExpr.Compile();

            this.errorMessages = stream.ErrorMessages;
            this.pool = MemoryManager.GetMemoryPool<Empty, TOutput>(false);
            this.pool.Get(out this.batch);
            this.batch.Allocate();

            this.hop = hop;
        }

        public override void ProduceQueryPlan(PlanNode previous)
            => this.Observer.ProduceQueryPlan(new SnapshotWindowPlanNode<TInput, TState, TOutput>(
                previous, this, typeof(Empty), typeof(TInput), typeof(TOutput),
                AggregatePipeType.StartEdge, this.aggregate, false, this.errorMessages, false));

        public override unsafe void OnNext(StreamMessage<Empty, TInput> batch)
        {
            var count = batch.Count;
            var colkey = batch.key.col;
            var colpayload = batch.payload.col;

            fixed (long* col_vsync = batch.vsync.col)
            fixed (long* col_vother = batch.vother.col)
            fixed (long* col_bv = batch.bitvector.col)
            for (int i = 0; i < count; i++)
            {
                if ((col_bv[i >> 6] & (1L << (i & 0x3f))) != 0)
                {
                    if (col_vother[i] == StreamEvent.PunctuationOtherTime)
                    {
                        // We have found a row that corresponds to punctuation
                        OnPunctuation(col_vsync[i]);

                        int c = this.batch.Count;
                        this.batch.vsync.col[c] = col_vsync[i];
                        this.batch.vother.col[c] = StreamEvent.PunctuationOtherTime;
                        this.batch.key.col[c] = Empty.Default;
                        this.batch.hash.col[c] = 0;
                        this.batch.bitvector.col[c >> 6] |= (1L << (c & 0x3f));
                        this.batch.Count++;
                        if (this.batch.Count == Config.DataBatchSize) FlushContents();
                    }
                    continue;
                }
                var syncTime = col_vsync[i];

                // Handle time moving forward
                if (syncTime > this.lastSyncTime)
                {
                    if (this.currentState != null)
                    {
                        int c = this.batch.Count;
                        this.batch.vsync.col[c] = this.currentState.timestamp;
                        this.batch.vother.col[c] = this.currentState.timestamp + this.hop;
                        this.batch.payload.col[c] = this.computeResult(this.currentState.state);
                        this.batch.key.col[c] = Empty.Default;
                        this.batch.hash.col[c] = 0;
                        this.batch.Count++;
                        if (this.batch.Count == Config.DataBatchSize) FlushContents();

                        if (hasDisposableState) DisposeStateLocal();
                        this.currentState = null;
                    }

                    // Since sync time changed, set lastSyncTime
                    this.lastSyncTime = syncTime;
                }

                if (this.currentState == null)
                    this.currentState = new HeldState<TState> { state = this.initialState(), timestamp = syncTime };
                else
                {
                    if (syncTime > this.currentState.timestamp)
                    {
                        // Reset currentState
                        if (hasDisposableState) DisposeStateLocal();
                        this.currentState.state = this.initialState();
                        this.currentState.timestamp = syncTime;
                    }
                }

                this.currentState.state = this.accumulate(this.currentState.state, col_vsync[i], colpayload[i]);
            }

            batch.Release();
            batch.Return();
        }

        public void OnPunctuation(long syncTime)
        {
            // Handle time moving forward
            if (syncTime > this.lastSyncTime)
            {
                if (this.currentState != null)
                {
                    int c = this.batch.Count;
                    this.batch.vsync.col[c] = this.currentState.timestamp;
                    this.batch.vother.col[c] = this.currentState.timestamp + this.hop;
                    this.batch.payload.col[c] = this.computeResult(this.currentState.state);
                    this.batch.key.col[c] = Empty.Default;
                    this.batch.hash.col[c] = 0;
                    this.batch.Count++;
                    if (this.batch.Count == Config.DataBatchSize) FlushContents();

                    if (hasDisposableState) DisposeStateLocal();
                    this.currentState = null;
                }

                // Since sync time changed, set lastSyncTime
                this.lastSyncTime = syncTime;
            }
        }

        protected override void FlushContents()
        {
            if (this.batch == null || this.batch.Count == 0) return;
            this.batch.Seal();
            this.Observer.OnNext(this.batch);
            this.pool.Get(out this.batch);
            this.batch.Allocate();
        }

        public override int CurrentlyBufferedOutputCount => this.batch.Count;

        public override int CurrentlyBufferedInputCount => 0;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void DisposeStateLocal()
        {
            if (this.currentState != null)
            {
                (this.currentState.state as IDisposable)?.Dispose();
            }
        }

        protected override void DisposeState()
        {
            this.batch.Free();
            if (hasDisposableState) DisposeState();
        }
    }
}
