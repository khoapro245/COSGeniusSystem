using System;
using System.Collections.Generic;

namespace COSGeniusSystem.Models;

public partial class GeniusSchemaMigration
{
    public string MigrationId { get; set; } = null!;

    public DateTime AppliedAtUtc { get; set; }
}
