using System;
using System.Collections.Generic;

namespace COSGeniusSystem.Models;

public partial class ExaminationQueueCounter
{
    public DateOnly BusinessDate { get; set; }

    public int LastNumber { get; set; }
}
