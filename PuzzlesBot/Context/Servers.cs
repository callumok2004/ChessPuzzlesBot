using System;
using System.Collections.Generic;

namespace PuzzlesBot.Context;

public partial class Servers
{
    public long ServerId { get; set; }

    public long PuzzlesChannel { get; set; }

    public string Theme { get; set; } = null!;
}
