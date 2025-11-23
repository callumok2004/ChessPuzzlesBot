using Discord;
using Discord.Interactions;
using Discord.WebSocket;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using PuzzlesBot.Context;

using Serilog;
using Serilog.Events;

using System.Configuration;

namespace PuzzlesBot;

public class Program
{
	public DiscordSocketClient? client;
	public ServiceProvider? services;

	public static Serilog.ILogger Logger { get; } = new LoggerConfiguration()
		.MinimumLevel.Verbose()
		.Enrich.FromLogContext()
		.WriteTo.Console()
		.CreateLogger();

	public static void Main() => new Program().MainAsync().GetAwaiter().GetResult();

	public async Task MainAsync() {
		await Log("Main", "Configuring services...");
		services = ConfigureServices();

		await Log("Main", "Loading themes...");
		BoardThemes.LoadAllThemes();

		await Log("Main", "Init Interactions...");
		await services.GetRequiredService<InteractionHandler>().InitializeAsync();

		client = services.GetRequiredService<DiscordSocketClient>();
		client.Ready += OnClientReady;
		client.Log += LogAsync;

		await Log("Main", "Starting client...");
		await client.LoginAsync(TokenType.Bot, ConfigurationManager.AppSettings["DiscordToken"]);
		await client.StartAsync();

		await Task.Delay(-1);
	}

	private async Task OnClientReady() {
		await Log("Main", $"Client ready as {client!.CurrentUser.Username}#{client.CurrentUser.Discriminator}");

		await Log("Main", "Starting hosted services...");
		foreach (var hosted in services!.GetServices<IHostedService>())
			await hosted.StartAsync(CancellationToken.None);
	}

	private ServiceProvider ConfigureServices() {
		var Intents = GatewayIntents.AllUnprivileged;
		Intents &= ~GatewayIntents.GuildScheduledEvents;
		Intents &= ~GatewayIntents.GuildInvites;

		DiscordSocketClient Client = new(
			new DiscordSocketConfig {
				GatewayIntents = Intents
			}
		);

		InteractionService Interactions = new(Client.Rest, new InteractionServiceConfig {
			LogLevel = LogSeverity.Debug,
			ThrowOnError = true,
			UseCompiledLambda = true
		});

		IServiceCollection Collection = new ServiceCollection()
			.AddSingleton(Client)
			.AddSingleton(Interactions)
			.AddSingleton<InteractionHandler>()
			.AddDbContext<PuzzlesBotContext>(ops => {
				ops.UseMySql(ConfigurationManager.AppSettings["SqlConnectionString"], ServerVersion.Parse("5.7.25-mysql"));
				ops.EnableDetailedErrors(true);
				ops.LogTo(Logger.Information, LogLevel.Warning);
			})
			.AddHostedService<DailyPuzzleService>();

		return Collection.BuildServiceProvider();
	}

	public static async Task LogAsync(LogMessage message) {
		LogEventLevel severity = message.Severity switch {
			LogSeverity.Critical => LogEventLevel.Fatal,
			LogSeverity.Error => LogEventLevel.Error,
			LogSeverity.Warning => LogEventLevel.Warning,
			LogSeverity.Info => LogEventLevel.Information,
			LogSeverity.Verbose => LogEventLevel.Verbose,
			LogSeverity.Debug => LogEventLevel.Debug,
			_ => LogEventLevel.Information
		};

		Logger.Write(severity, message.Exception, "[{Source}] {Message}", message.Source, message.Message);
		await Task.CompletedTask;
	}

	public static async Task Log(string? source, string? message, LogSeverity severity = LogSeverity.Info, Exception? exception = null) {
		var logMessage = new LogMessage(severity, source, message, exception);
		await LogAsync(logMessage);
	}
}
