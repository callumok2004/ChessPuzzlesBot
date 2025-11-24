using Discord;
using Discord.Interactions;

using System.ComponentModel;

namespace PuzzlesBot;

public partial class ConfigInteractions {
	[SlashCommand("settime", "Configure time for daily puzzles")]
	[RequireUserPermission(GuildPermission.Administrator)]
	public async Task SetTime(
		[Description("Time to post daily puzzles (HH:MM - 24HR)")] string time,
		[Autocomplete<TimeZoneAutocompleteHandler>] string timezone
	) {
		if (!TimeOnly.TryParse(time, out var parsedTime)) {
			await RespondAsync("Invalid time format. Please use HH:MM (24-hour format).");
			return;
		}

		try {
			var tz = TimeZoneInfo.FindSystemTimeZoneById(timezone);
		}
		catch {
			var timezones = TimeZoneInfo.GetSystemTimeZones();
			var match = timezones.FirstOrDefault(tz => tz.DisplayName.Equals(timezone, StringComparison.OrdinalIgnoreCase));
			if (match != null) {
				timezone = match.Id;
			}
			else {
				await RespondAsync("Invalid timezone. Please select a valid timezone from the autocomplete list.");
				return;
			}
		}

		var guildConfig = db.Servers.FirstOrDefault(s => s.ServerId == (long)Context.Guild.Id);
		if (guildConfig == null) {
			guildConfig = new Context.Servers {
				ServerId = (long)Context.Guild.Id,
				DailyTime = new TimeSpan(parsedTime.Hour, parsedTime.Minute, 0),
				DailyTz = timezone
			};
			db.Servers.Add(guildConfig);
		}
		else {
			guildConfig.DailyTime = new TimeSpan(parsedTime.Hour, parsedTime.Minute, 0);
			guildConfig.DailyTz = timezone;
		}

		await db.SaveChangesAsync();

		await RespondAsync($"Daily puzzles time set to `{time} ({timezone})`.");
		await Program.Log("Config", $"Set daily puzzle time for guild {Context.Guild.Id} to {time} ({timezone})", LogSeverity.Info);

		await DailyPuzzleService.RescheduleServerAsync((long)Context.Guild.Id);
	}
}

public class TimeZoneAutocompleteHandler : AutocompleteHandler {
	public override async Task<AutocompletionResult> GenerateSuggestionsAsync(IInteractionContext context, IAutocompleteInteraction autocompleteInteraction, IParameterInfo parameter, IServiceProvider services) {
		var timezones = TimeZoneInfo.GetSystemTimeZones();
		var results = timezones.Select(tz => new AutocompleteResult(tz.DisplayName, tz.Id));
		if (autocompleteInteraction.Data.Current.Value is string currentValue && !string.IsNullOrWhiteSpace(currentValue))
			results = results.Where(r => r.Name.Contains(currentValue, StringComparison.OrdinalIgnoreCase));
		return AutocompletionResult.FromSuccess(results.Take(25));
	}
}