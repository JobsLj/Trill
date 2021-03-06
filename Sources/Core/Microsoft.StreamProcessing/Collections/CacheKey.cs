﻿// *********************************************************************
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License
// *********************************************************************
using System;

namespace Microsoft.StreamProcessing.Internal.Collections
{
    internal abstract class CacheKey
    {
        protected bool useMultiString;
        protected int batchSize;

        protected CacheKey()
        {
            this.useMultiString = Config.UseMultiString;
            this.batchSize = Config.DataBatchSize;
        }

        public static CacheKey Create() => new CacheKey0();
        public static CacheKey Create<T>(T keyData) => new CacheKey1<T>(keyData);
        public static CacheKey Create<T1, T2>(T1 t1, T2 t2) => new CacheKey2<T1, T2>(t1, t2);
        public static CacheKey Create<T1, T2, T3>(T1 t1, T2 t2, T3 t3) => new CacheKey3<T1, T2, T3>(t1, t2, t3);
        public static CacheKey Create<T1, T2, T3, T4, T5>(T1 t1, T2 t2, T3 t3, T4 t4, T5 t5) => new CacheKey5<T1, T2, T3, T4, T5>(t1, t2, t3, t4, t5);

        private sealed class CacheKey0 : CacheKey
        {
            public CacheKey0() : base() { }

            public override int GetHashCode() => this.batchSize ^ this.useMultiString.GetHashCode();

            public override bool Equals(object obj)
                => !(obj is CacheKey0 o)
                    ? false
                    : this.batchSize == o.batchSize && this.useMultiString == o.useMultiString;
        }

        private sealed class CacheKey1<T1> : CacheKey
        {
            private T1 keyData1;

            public CacheKey1(T1 keyData1) : base() => this.keyData1 = keyData1;

            public override int GetHashCode()
                => (this.keyData1.GetHashCode() * 23) + this.batchSize ^ this.useMultiString.GetHashCode();

            public override bool Equals(object obj)
                => !(obj is CacheKey1<T1> o)
                    ? false
                    : this.batchSize == o.batchSize && this.useMultiString == o.useMultiString && this.keyData1.Equals(o.keyData1);
        }

        private sealed class CacheKey2<T1, T2> : CacheKey
        {
            private T1 keyData1;
            private T2 keyData2;

            public CacheKey2(T1 keyData1, T2 keyData2) : base()
            {
                this.keyData1 = keyData1;
                this.keyData2 = keyData2;
            }

            public override int GetHashCode()
                => ((this.keyData1.GetHashCode() * 23
                   + this.keyData2.GetHashCode()) * 23) + this.batchSize ^ this.useMultiString.GetHashCode();

            public override bool Equals(object obj)
                => !(obj is CacheKey2<T1, T2> o)
                    ? false
                    : this.batchSize == o.batchSize && this.useMultiString == o.useMultiString
                                                    && this.keyData1.Equals(o.keyData1)
                                                    && this.keyData2.Equals(o.keyData2);
        }

        private sealed class CacheKey3<T1, T2, T3> : CacheKey
        {
            private T1 keyData1;
            private T2 keyData2;
            private T3 keyData3;

            public CacheKey3(T1 keyData1, T2 keyData2, T3 keyData3) : base()
            {
                this.keyData1 = keyData1;
                this.keyData2 = keyData2;
                this.keyData3 = keyData3;
            }

            public override int GetHashCode()
                => (((this.keyData1.GetHashCode() * 23
                    + this.keyData2.GetHashCode()) * 23
                    + this.keyData3.GetHashCode()) * 23) + this.batchSize ^ this.useMultiString.GetHashCode();

            public override bool Equals(object obj)
                => !(obj is CacheKey3<T1, T2, T3> o)
                    ? false
                    : this.batchSize == o.batchSize && this.useMultiString == o.useMultiString
                                                    && this.keyData1.Equals(o.keyData1)
                                                    && this.keyData2.Equals(o.keyData2)
                                                    && this.keyData3.Equals(o.keyData3);
        }

        private sealed class CacheKey5<T1, T2, T3, T4, T5> : CacheKey
        {
            private T1 keyData1;
            private T2 keyData2;
            private T3 keyData3;
            private T4 keyData4;
            private T5 keyData5;

            public CacheKey5(T1 keyData1, T2 keyData2, T3 keyData3, T4 keyData4, T5 keyData5) : base()
            {
                this.keyData1 = keyData1;
                this.keyData2 = keyData2;
                this.keyData3 = keyData3;
                this.keyData4 = keyData4;
                this.keyData5 = keyData5;
            }

            public override int GetHashCode()
                => (((((this.keyData1.GetHashCode() * 23
                      + this.keyData2.GetHashCode()) * 23
                      + this.keyData3.GetHashCode()) * 23
                      + this.keyData4.GetHashCode()) * 23
                      + this.keyData5.GetHashCode()) * 23) + this.batchSize ^ this.useMultiString.GetHashCode();

            public override bool Equals(object obj)
                => !(obj is CacheKey5<T1, T2, T3, T4, T5> o)
                    ? false
                    : this.batchSize == o.batchSize && this.useMultiString == o.useMultiString
                                                    && this.keyData1.Equals(o.keyData1)
                                                    && this.keyData2.Equals(o.keyData2)
                                                    && this.keyData3.Equals(o.keyData3)
                                                    && this.keyData4.Equals(o.keyData4)
                                                    && this.keyData5.Equals(o.keyData5);
        }
    }
}
