using Discord;
using Discord.Interactions;

using Microsoft.EntityFrameworkCore;

using PuzzlesBot.Context;

using System.ComponentModel;
using System.Globalization;

namespace PuzzlesBot;

public partial class Interactions {
	public enum LeaderboardMetric {
		[ChoiceDisplay("Successful solves")] Successful,
		[ChoiceDisplay("Total attempts")] Attempts
	}

	public enum LeaderboardPeriod {
		[ChoiceDisplay("All time")] AllTime,
		[ChoiceDisplay("Weekly (last 7 days)")] Weekly,
		[ChoiceDisplay("Monthly (last 30 days)")] Monthly,
		[ChoiceDisplay("Custom range")] Custom
	}

	[SlashCommand("leaderboard", "Show the puzzle leaderboard for this server")]
	public async Task LeaderboardAsync(
		LeaderboardMetric metric = LeaderboardMetric.Successful,
		LeaderboardPeriod period = LeaderboardPeriod.AllTime,
		[Description("Start date for a custom range (YYYY-MM-DD)")] string? from = null,
		[Description("End date for a custom range (YYYY-MM-DD), defaults to now")] string? to = null
	) {
		await DeferAsync();

		long serverId = (long)Context.Guild.Id;
		var nowUtc = DateTime.UtcNow;
		DateTime? start = null;
		DateTime? end = null;

		switch (period) {
			case LeaderboardPeriod.Weekly:
				start = nowUtc.AddDays(-7);
				break;
			case LeaderboardPeriod.Monthly:
				start = nowUtc.AddDays(-30);
				break;
			case LeaderboardPeriod.Custom:
				if (string.IsNullOrWhiteSpace(from) || !TryParseDate(from, out var parsedFrom)) {
					await FollowupAsync("For a custom range, provide a valid `from` date (YYYY-MM-DD).", ephemeral: true);
					return;
				}
				start = parsedFrom;

				if (!string.IsNullOrWhiteSpace(to)) {
					if (!TryParseDate(to, out var parsedTo)) {
						await FollowupAsync("Invalid `to` date. Please use YYYY-MM-DD.", ephemeral: true);
						return;
					}
					end = parsedTo.AddDays(1);
				}
				break;
		}

		var query = db.PuzzleResults.Where(r => r.ServerId == serverId);

		if (metric == LeaderboardMetric.Successful)
			query = query.Where(r => r.Solved);
		if (start != null)
			query = query.Where(r => r.CreatedAt >= start);
		if (end != null)
			query = query.Where(r => r.CreatedAt < end);

		var top = await query
			.GroupBy(r => r.UserId)
			.Select(g => new { UserId = g.Key, Count = g.Count() })
			.OrderByDescending(x => x.Count)
			.ThenBy(x => x.UserId)
			.Take(10)
			.ToListAsync();

		string metricLabel = metric == LeaderboardMetric.Successful ? "Successful solves" : "Total attempts";
		string periodLabel = period switch {
			LeaderboardPeriod.Weekly => "Last 7 days",
			LeaderboardPeriod.Monthly => "Last 30 days",
			LeaderboardPeriod.Custom => end != null
				? $"{start:yyyy-MM-dd} → {end.Value.AddDays(-1):yyyy-MM-dd}"
				: $"Since {start:yyyy-MM-dd}",
			_ => "All time"
		};

		if (top.Count == 0) {
			await FollowupAsync($"No leaderboard data yet for **{metricLabel}** ({periodLabel}).");
			return;
		}

		string[] medals = ["\U0001F947", "\U0001F948", "\U0001F949"];
		var lines = top.Select((entry, i) => {
			string rank = i < medals.Length ? medals[i] : $"`#{i + 1}`";
			return $"{rank} <@{(ulong)entry.UserId}> - **{entry.Count}**";
		});

		var embed = new EmbedBuilder()
			.WithTitle($"\U0001F3C6 Leaderboard - {metricLabel}")
			.WithDescription(string.Join('\n', lines))
			.WithFooter(periodLabel)
			.WithColor(Color.Gold)
			.WithCurrentTimestamp();

		await FollowupAsync(embed: embed.Build());
	}

	private static bool TryParseDate(string input, out DateTime utcDate) {
		if (DateTime.TryParse(input, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed)) {
			utcDate = parsed.Date;
			return true;
		}
		utcDate = default;
		return false;
	}
}
