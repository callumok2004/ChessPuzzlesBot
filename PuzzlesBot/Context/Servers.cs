using System;
using System.Collections.Generic;

namespace PuzzlesBot.Context;

public partial class Servers
{
    public long ServerId { get; set; }

    public long? PuzzlesChannel { get; set; }

    public string Theme { get; set; } = null!;

    public TimeSpan? DailyTime { get; set; }

    public string? DailyTz { get; set; }

    public DateTime? LastRun { get; set; }

    public int? CurrentPuzzleId { get; set; }

    public long? RoleId { get; set; }
}
