using Discord;
using Discord.Interactions;

namespace PuzzlesBot;

public partial class ConfigInteractions {
	[SlashCommand("settheme", "Configure theme for board")]
	[RequireUserPermission(GuildPermission.Administrator)]
	public async Task SetTheme([Autocomplete<ThemesAutocompleteHandler>] string theme) {
		var guildConfig = db.Servers.FirstOrDefault(s => s.ServerId == (long)Context.Guild.Id);
		if (guildConfig == null) {
			guildConfig = new Context.Servers {
				ServerId = (long)Context.Guild.Id,
				Theme = theme
			};
			db.Servers.Add(guildConfig);
		}
		else guildConfig.Theme = theme;

		await db.SaveChangesAsync();

		await RespondAsync($"Board theme set to `{theme}`.");
	}
}

public class ThemesAutocompleteHandler : AutocompleteHandler {
	public override async Task<AutocompletionResult> GenerateSuggestionsAsync(IInteractionContext context, IAutocompleteInteraction autocompleteInteraction, IParameterInfo parameter, IServiceProvider services) {
		var Themes = BoardThemes.ThemesAvailable;
		var results = Themes.Select(t => new AutocompleteResult(t, t));
		if (autocompleteInteraction.Data.Current.Value is string currentValue && !string.IsNullOrWhiteSpace(currentValue))
			results = results.Where(r => r.Name.StartsWith(currentValue, StringComparison.OrdinalIgnoreCase));

		return AutocompletionResult.FromSuccess(results.Take(25));
	}
}