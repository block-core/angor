﻿using Angor.Client.Storage;
using Angor.Shared.Models;
using Microsoft.AspNetCore.Components;

namespace Angor.Client.Shared
{
    public class FeeData
    {
        public FeeEstimations FeeEstimations { get; set; } = new();
        public FeeEstimation SelectedFeeEstimation { get; set; } = new();
        public int FeePosition { get; set; } = 1;
        public int FeeMin { get; set; } = 1;
        public int FeeMax { get; set; } = 3;
    }
}
