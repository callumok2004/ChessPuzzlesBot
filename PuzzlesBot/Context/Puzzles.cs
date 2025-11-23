using System;
using System.Collections.Generic;

namespace PuzzlesBot.Context;

public partial class Puzzles
{
    public int Id { get; set; }

    public string PuzzleId { get; set; } = null!;

    public string Fen { get; set; } = null!;

    public string Moves { get; set; } = null!;

    public int Rating { get; set; }

    public string Url { get; set; } = null!;

    public long Mesid { get; set; }

    public DateTime EndAt { get; set; }
}
