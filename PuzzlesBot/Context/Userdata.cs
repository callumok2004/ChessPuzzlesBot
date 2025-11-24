using System;
using System.Collections.Generic;

namespace PuzzlesBot.Context;

public partial class Userdata
{
    public long UserId { get; set; }

    public long ServerId { get; set; }

    public int? LastCompleted { get; set; }

    public int Streak { get; set; }
}
