using System;
using System.Collections.Generic;

namespace PuzzlesBot.Context;

public partial class PuzzleResults
{
    public long ServerId { get; set; }

    public long UserId { get; set; }

    public int PuzzleId { get; set; }

    public bool Solved { get; set; }

    public DateTime CreatedAt { get; set; }
}
