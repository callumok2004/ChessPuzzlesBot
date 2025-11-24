using System;
using System.Collections.Generic;

namespace PuzzlesBot.Context;

public partial class PuzzleAttemps
{
    public int Id { get; set; }

    public string Fen { get; set; } = null!;

    public string Moves { get; set; } = null!;

    public long UserId { get; set; }

    public sbyte Failed { get; set; }

    public long MessageId { get; set; }
}
