using Cronos;
using Discord;
using Microsoft.Extensions.Hosting;

namespace PuzzlesBot;

public class DailyPuzzleService : BackgroundService {
	private readonly TimeSpan checkInterval = TimeSpan.FromSeconds(10);
	private readonly CronExpression cron = CronExpression.Parse("0 14 * * *");
	private DateTime? lastRun;

	protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
		while (!stoppingToken.IsCancellationRequested) {
			var now = DateTime.UtcNow;
			var next = cron.GetNextOccurrence(lastRun ?? now.AddMinutes(-1), TimeZoneInfo.Utc);
			if (next.HasValue && now >= next.Value) {
				await RunJob();
				lastRun = now;
			}
#if DEBUG
			else
				await Program.Log("Daily Puzzle", $"Next run at {next}", LogSeverity.Debug);
#endif

			await Task.Delay(checkInterval, stoppingToken);
		}
	}

	private Task RunJob() {
		Console.WriteLine($"Job running at {DateTime.UtcNow}");
		return Task.CompletedTask;
	}
}
