using Discord;
using Discord.Interactions;
using Discord.WebSocket;

using Microsoft.EntityFrameworkCore;

using PuzzlesBot.Context;

using System.ComponentModel;
using System.Globalization;

namespace PuzzlesBot;

public partial class Interactions {
	private const int LeaderboardPageSize = 10;

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

		var (ok, error, start, end) = ResolvePeriod(period, from, to);
		if (!ok) {
			await FollowupAsync(error, ephemeral: true);
			return;
		}

		var (embed, components) = await BuildLeaderboardPageAsync((long)Context.Guild.Id, metric, period, start, end, 0);
		await FollowupAsync(embed: embed, components: components);
	}

	[ComponentInteraction("lb:*:*:*:*:*", ignoreGroupNames: true)]
	public async Task LeaderboardPageAsync(string metricStr, string periodStr, string startStr, string endStr, string pageStr) {
		var metric = (LeaderboardMetric)int.Parse(metricStr);
		var period = (LeaderboardPeriod)int.Parse(periodStr);
		DateTime? start = DecodeTicks(startStr);
		DateTime? end = DecodeTicks(endStr);
		int page = int.Parse(pageStr);

		var (embed, components) = await BuildLeaderboardPageAsync((long)Context.Guild.Id, metric, period, start, end, page);

		await ((SocketMessageComponent)Context.Interaction).UpdateAsync(m => {
			m.Embed = embed;
			m.Components = components;
		});
	}

	[SlashCommand("raffle", "Pick a weighted random winner (admin only)")]
	[RequireUserPermission(GuildPermission.Administrator)]
	public async Task RaffleAsync(
		LeaderboardMetric metric = LeaderboardMetric.Successful,
		LeaderboardPeriod period = LeaderboardPeriod.AllTime,
		[Description("Start date for a custom range (YYYY-MM-DD)")] string? from = null,
		[Description("End date for a custom range (YYYY-MM-DD), defaults to now")] string? to = null
	) {
		if (Context.User is not SocketGuildUser user || !user.GuildPermissions.Administrator) {
			await FollowupAsync("You do not have permission to run this command.", ephemeral: true);
			return;
		}

		await DeferAsync();

		var (ok, error, start, end) = ResolvePeriod(period, from, to);
		if (!ok) {
			await FollowupAsync(error, ephemeral: true);
			return;
		}

		var entries = await FilterResults((long)Context.Guild.Id, metric, start, end)
			.GroupBy(r => r.UserId)
			.Select(g => new { UserId = g.Key, Count = g.Count() })
			.ToListAsync();

		string metricLabel = MetricLabel(metric);
		string entryWord = metric == LeaderboardMetric.Successful ? "solve" : "attempt";
		string periodLabel = PeriodLabel(period, start, end);

		if (entries.Count == 0) {
			await FollowupAsync($"No raffle entries for **{metricLabel}** ({periodLabel}).");
			return;
		}

		long total = entries.Sum(e => (long)e.Count);
		long roll = (long)(Random.Shared.NextDouble() * total);
		long cumulative = 0;
		var winner = entries[^1];
		foreach (var entry in entries) {
			cumulative += entry.Count;
			if (roll < cumulative) {
				winner = entry;
				break;
			}
		}

		double odds = total > 0 ? (double)winner.Count / total * 100 : 0;

		var embed = new EmbedBuilder()
			.WithTitle("\U0001F39F️ Raffle Winner")
			.WithDescription(
				$"Congratulations <@{(ulong)winner.UserId}>! \U0001F389\n\n" +
				$"Won with **{winner.Count}** {entryWord}{(winner.Count == 1 ? "" : "s")} " +
				$"out of **{total}** total entries (**{odds:F1}%** chance).")
			.WithFooter($"{metricLabel} • {periodLabel} • {entries.Count} entrants")
			.WithColor(Color.Magenta)
			.WithCurrentTimestamp();

		await FollowupAsync(embed: embed.Build());
	}

	private async Task<(Embed embed, MessageComponent components)> BuildLeaderboardPageAsync(
		long serverId, LeaderboardMetric metric, LeaderboardPeriod period, DateTime? start, DateTime? end, int page) {
		var ranking = await FilterResults(serverId, metric, start, end)
			.GroupBy(r => r.UserId)
			.Select(g => new { UserId = g.Key, Count = g.Count() })
			.OrderByDescending(x => x.Count)
			.ThenBy(x => x.UserId)
			.ToListAsync();

		string metricLabel = MetricLabel(metric);
		string periodLabel = PeriodLabel(period, start, end);

		int totalPages = Math.Max(1, (ranking.Count + LeaderboardPageSize - 1) / LeaderboardPageSize);
		page = Math.Clamp(page, 0, totalPages - 1);

		var embed = new EmbedBuilder()
			.WithTitle($"\U0001F3C6 Leaderboard - {metricLabel}")
			.WithColor(Color.Gold)
			.WithCurrentTimestamp();

		if (ranking.Count == 0) {
			embed.WithDescription($"No leaderboard data yet for **{metricLabel}** ({periodLabel}).");
			embed.WithFooter(periodLabel);
		}
		else {
			string[] medals = ["\U0001F947", "\U0001F948", "\U0001F949"];
			var pageItems = ranking.Skip(page * LeaderboardPageSize).Take(LeaderboardPageSize);
			var lines = pageItems.Select((entry, i) => {
				int globalRank = page * LeaderboardPageSize + i;
				string rank = globalRank < medals.Length ? medals[globalRank] : $"`#{globalRank + 1}`";
				return $"{rank} <@{(ulong)entry.UserId}> - **{entry.Count}**";
			});
			embed.WithDescription(string.Join('\n', lines));
			embed.WithFooter($"{periodLabel} • Page {page + 1}/{totalPages} • {ranking.Count} players");
		}

		var components = new ComponentBuilder()
			.WithButton("Previous", PageCustomId(metric, period, start, end, page - 1), ButtonStyle.Secondary, disabled: page <= 0)
			.WithButton("Next", PageCustomId(metric, period, start, end, page + 1), ButtonStyle.Secondary, disabled: page >= totalPages - 1)
			.Build();

		return (embed.Build(), components);
	}

	private IQueryable<PuzzleResults> FilterResults(long serverId, LeaderboardMetric metric, DateTime? start, DateTime? end) {
		var query = db.PuzzleResults.Where(r => r.ServerId == serverId);
		if (metric == LeaderboardMetric.Successful)
			query = query.Where(r => r.Solved);
		if (start != null)
			query = query.Where(r => r.CreatedAt >= start);
		if (end != null)
			query = query.Where(r => r.CreatedAt < end);
		return query;
	}

	private static (bool ok, string? error, DateTime? start, DateTime? end) ResolvePeriod(LeaderboardPeriod period, string? from, string? to) {
		var nowUtc = DateTime.UtcNow;
		switch (period) {
			case LeaderboardPeriod.Weekly:
				return (true, null, nowUtc.AddDays(-7), null);
			case LeaderboardPeriod.Monthly:
				return (true, null, nowUtc.AddDays(-30), null);
			case LeaderboardPeriod.Custom:
				if (string.IsNullOrWhiteSpace(from) || !TryParseDate(from, out var parsedFrom))
					return (false, "For a custom range, provide a valid `from` date (YYYY-MM-DD).", null, null);
				DateTime? end = null;
				if (!string.IsNullOrWhiteSpace(to)) {
					if (!TryParseDate(to, out var parsedTo))
						return (false, "Invalid `to` date. Please use YYYY-MM-DD.", null, null);
					end = parsedTo.AddDays(1); // inclusive of the whole `to` day
				}
				return (true, null, parsedFrom, end);
			default:
				return (true, null, null, null);
		}
	}

	private static string MetricLabel(LeaderboardMetric metric) =>
		metric == LeaderboardMetric.Successful ? "Successful solves" : "Total attempts";

	private static string PeriodLabel(LeaderboardPeriod period, DateTime? start, DateTime? end) => period switch {
		LeaderboardPeriod.Weekly => "Last 7 days",
		LeaderboardPeriod.Monthly => "Last 30 days",
		LeaderboardPeriod.Custom => end != null
			? $"{start:yyyy-MM-dd} → {end.Value.AddDays(-1):yyyy-MM-dd}"
			: $"Since {start:yyyy-MM-dd}",
		_ => "All time"
	};

	private static string PageCustomId(LeaderboardMetric metric, LeaderboardPeriod period, DateTime? start, DateTime? end, int page) =>
		$"lb:{(int)metric}:{(int)period}:{EncodeTicks(start)}:{EncodeTicks(end)}:{page}";

	private static string EncodeTicks(DateTime? value) => value.HasValue ? value.Value.Ticks.ToString() : "n";

	private static DateTime? DecodeTicks(string value) =>
		value == "n" ? null : new DateTime(long.Parse(value), DateTimeKind.Utc);

	private static bool TryParseDate(string input, out DateTime utcDate) {
		if (DateTime.TryParse(input, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed)) {
			utcDate = parsed.Date;
			return true;
		}
		utcDate = default;
		return false;
	}
}
