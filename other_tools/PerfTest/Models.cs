// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Text;

namespace SearchPerfTest
{
    public class PerfStat
    {
        public int runStatusCode { get; set; }
        public int runMS { get; set; }
        public DateTime runTime { get; set; }
        public long? resultCount { get; set; }
    }

    public class Query
    {
        public int q { get; set; }
    }

    public class Result
    {

    }
}
