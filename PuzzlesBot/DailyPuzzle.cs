using Discord;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PuzzlesBot.Context;

namespace PuzzlesBot;

public class DailyPuzzleService(IServiceProvider services, DiscordSocketClient client) {
	private readonly IServiceProvider _services = services;
	private readonly DiscordSocketClient _client = client;
	private readonly Dictionary<long, Timer> _timers = [];

	public async Task RescheduleAllAsync() {
		var nowUtc = DateTime.UtcNow;
		using var scope = _services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<PuzzlesBotContext>();
		var servers = await db.Servers
			.Where(s => s.PuzzlesChannel != null && s.DailyTime != null)
			.ToListAsync();

		foreach (var server in servers)
			await RescheduleServerInternalAsync(server, nowUtc, db);

		await db.SaveChangesAsync();
	}

	public async Task RescheduleServerAsync(long serverId) {
		var nowUtc = DateTime.UtcNow;
		using var scope = _services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<PuzzlesBotContext>();
		var server = await db.Servers.FirstOrDefaultAsync(s => s.ServerId == serverId);
		if (server == null)
			return;

		await RescheduleServerInternalAsync(server, nowUtc, db);
		await db.SaveChangesAsync();
	}

	private async Task RescheduleServerInternalAsync(Servers server, DateTime nowUtc, PuzzlesBotContext db) {
		if (_timers.TryGetValue(server.ServerId, out var existing)) {
			await Program.Log("Daily Puzzle", $"Cancelling existing timer for guild {server.ServerId}", LogSeverity.Debug);
			existing.Dispose();
			_timers.Remove(server.ServerId);
		}

		if (server.PuzzlesChannel == null || server.DailyTime == null || string.IsNullOrWhiteSpace(server.DailyTz))
			return;

		TimeZoneInfo tz;
		try {
			tz = TimeZoneInfo.FindSystemTimeZoneById(server.DailyTz);
		}
		catch {
			Program.Log("Daily Puzzle", $"Invalid timezone '{server.DailyTz}' for guild {server.ServerId}, cannot schedule", LogSeverity.Warning).Wait();
			return;
		}

		var localNow = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, tz);
		var baseDate = localNow.Date;
		var todayLocal = baseDate + server.DailyTime!.Value;
		var nextLocal = localNow.TimeOfDay <= server.DailyTime.Value
			? todayLocal
			: todayLocal.AddDays(1);
		var nextRunUtc = TimeZoneInfo.ConvertTimeToUtc(nextLocal, tz);

		if (server.LastRun != null && (nowUtc - server.LastRun.Value).TotalHours >= 24 && nowUtc >= nextRunUtc) {
			await Program.Log("Daily Puzzle", $"Server {server.ServerId} overdue, running immediately", LogSeverity.Debug);
			await RunDailyPuzzleForServer(server, db);
			server.LastRun = nowUtc;
			nextRunUtc = nextRunUtc.AddDays(1);
		}

		var due = nextRunUtc - nowUtc;
		if (due < TimeSpan.Zero)
			due = TimeSpan.Zero;

		await Program.Log("Daily Puzzle", $"Scheduling guild {server.ServerId} in {due}", LogSeverity.Debug);
		var timer = new Timer(async _ => await OnTimerFired(server.ServerId), null, due, Timeout.InfiniteTimeSpan);
		_timers[server.ServerId] = timer;
	}

	private async Task OnTimerFired(long serverId) {
		try {
			var nowUtc = DateTime.UtcNow;
			using var scope = _services.CreateScope();
			var db = scope.ServiceProvider.GetRequiredService<PuzzlesBotContext>();
			var server = await db.Servers.FirstOrDefaultAsync(s => s.ServerId == serverId);
			if (server == null)
				return;

			await RunDailyPuzzleForServer(server, db);
			server.LastRun = nowUtc;
			await db.SaveChangesAsync();

			await RescheduleServerInternalAsync(server, nowUtc, db);
		}
		catch (Exception ex) {
			await Program.Log("Daily Puzzle", $"Error running daily puzzle: {ex.Message}", LogSeverity.Error, ex);
		}
	}

	public async Task TriggerDailyPuzzleNow(long serverId) {
		var nowUtc = DateTime.UtcNow;
		using var scope = _services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<PuzzlesBotContext>();
		var server = await db.Servers.FirstOrDefaultAsync(s => s.ServerId == serverId);
		if (server == null)
			return;

		await RunDailyPuzzleForServer(server, db);
		server.LastRun = nowUtc;
		await db.SaveChangesAsync();

		await RescheduleServerInternalAsync(server, nowUtc, db);
	}

	private async Task RunDailyPuzzleForServer(Servers server, PuzzlesBotContext db) {
		await Program.Log("Daily Puzzle", $"Running for guild {server.ServerId}", LogSeverity.Info);

		if (server.PuzzlesChannel == null) return;

		if (await _client.GetChannelAsync((ulong)server.PuzzlesChannel) is not IMessageChannel channel) {
			await Program.Log("Daily Puzzle", $"Channel {server.PuzzlesChannel} not found for guild {server.ServerId}", LogSeverity.Warning);
			return;
		}

		var records = Interactions.records;
		if (records.Count == 0) return;

		var usedPuzzleIds = await db.Puzzles
			.Select(p => p.PuzzleId)
			.ToListAsync();

		var random = new Random();
		var randomRecord = records[random.Next(records.Count)];

		while (usedPuzzleIds.Contains(randomRecord.PuzzleId)) randomRecord = records[random.Next(records.Count)];

		string theme = server.Theme ?? "default";
		ChessBoard chessBoard = new(theme);

		MemoryStream boardStream = chessBoard.Render(randomRecord.FEN, randomRecord.Moves.Split(' ')[0], randomRecord.GameUrl.Contains("/black#"));
		boardStream.Position = 0;

		long endTimestamp = DateTimeOffset.UtcNow.AddHours(24).ToUnixTimeSeconds();

		EmbedBuilder embed = new() {
			Title = "Daily Puzzle (Rating: " + randomRecord.Rating + ")",
			Description = "Use `/play` to start solving!\nTime Remaining: <t:" + endTimestamp + ":R>",
			Color = Color.Blue,
			Timestamp = DateTimeOffset.UtcNow,
			Footer = new EmbedFooterBuilder {
				Text = "Good luck!"
			},
			ImageUrl = "attachment://board.png"
		};

		string? content = null;
		if (server.RoleId != null)
			content = $"<@&{server.RoleId}>";

		IUserMessage message = await channel.SendFileAsync(boardStream, "board.png", embed: embed.Build(), text: content);

		var puzzle = new Puzzles {
			PuzzleId = randomRecord.PuzzleId,
			Fen = randomRecord.FEN,
			Moves = randomRecord.Moves,
			Rating = int.Parse(randomRecord.Rating),
			Url = randomRecord.GameUrl,
			Mesid = (long)message.Id,
			EndAt = DateTime.UtcNow.AddHours(24)
		};

		await db.Puzzles.AddAsync(puzzle);
		await db.SaveChangesAsync();

		server.CurrentPuzzleId = puzzle.Id;
	}
}
