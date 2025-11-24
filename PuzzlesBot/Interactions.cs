using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;

using PuzzlesBot.Context;

using System.Reflection;
using static PuzzlesBot.Program;

namespace PuzzlesBot;

public class InteractionHandler {
	internal readonly InteractionService interactions;
	internal readonly DiscordSocketClient client;
	internal readonly IServiceProvider services;

	public InteractionHandler(IServiceProvider _services) {
		interactions = _services.GetRequiredService<InteractionService>();
		client = _services.GetRequiredService<DiscordSocketClient>();
		services = _services;

		client.InteractionCreated += InteractionCreated;
		interactions.InteractionExecuted += InteractionExecuted;

		client.Ready += async () => await interactions.RegisterCommandsGloballyAsync(true);
	}

	public async Task InitializeAsync() {
		await interactions.AddModulesAsync(Assembly.GetEntryAssembly(), services);
	}

	public async Task InteractionCreated(SocketInteraction interaction) {
		if (interaction.IsDMInteraction) return;

		SocketInteractionContext context = new(client, interaction);

		try {
			IResult result = await interactions.ExecuteCommandAsync(context, services);
			if (!result.IsSuccess) {
				await Log("Interaction", $"Error occurred executing an interaction for {context.User.Username}: {result}", LogSeverity.Error);

				if (interaction.Type is InteractionType.ApplicationCommand) {
					if (interaction.HasResponded) await interaction.GetOriginalResponseAsync().ContinueWith(async (msg) => await msg.Result.ModifyAsync((x) => x.Content = "An error occured executing the command. Please try again later."));
					else await interaction.RespondAsync("An error occured executing the command. Please try again later.");
				}
			}
		} catch (Exception exception) {
			await Log("Interaction", $"Exception occurred executing an interaction for {context.User.Username}", LogSeverity.Error, exception);
			if (interaction.Type is InteractionType.ApplicationCommand)
				await interaction.GetOriginalResponseAsync().ContinueWith(async (msg) => await msg.Result.DeleteAsync());
		}
	}

	public async Task InteractionExecuted(ICommandInfo commandInfo, IInteractionContext context, IResult result) {
		if (result.IsSuccess || context.Interaction.HasResponded) return;

		EmbedBuilder enbed = new() {
			Color = Color.Red,
			Description = "An error has occurred."
		};

		await context.Interaction.RespondAsync(embed: enbed.Build());
	}
}

public class InteractionsBase(IServiceProvider services) : InteractionModuleBase {
	public readonly InteractionService interactions = services.GetRequiredService<InteractionService>();
	public readonly PuzzlesBotContext db = services.GetRequiredService<PuzzlesBotContext>();
	public readonly DailyPuzzleService DailyPuzzleService = services.GetRequiredService<DailyPuzzleService>();
}

public partial class Interactions(IServiceProvider services) : InteractionsBase(services) { }

[Group("config", "configuration commands")]
public partial class ConfigInteractions(IServiceProvider services) : InteractionsBase(services) { }
