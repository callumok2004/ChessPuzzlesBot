using Discord;
using Discord.Interactions;
using Discord.WebSocket;

using Microsoft.Extensions.DependencyInjection;

using MongoDB.Driver;

using Serilog;
using Serilog.Events;

using System.Configuration;

namespace PuzzlesBot;

public class PuzzleRecord
{
#pragma warning disable CS8618
	public string PuzzleId { get; set; }
	public string FEN { get; set; }
	public string Moves { get; set; }
	public string Rating { get; set; }
	public string GameUrl { get; set; }
#pragma warning restore CS8618
}

public class Program
{
	public DiscordSocketClient? client;
	public static ILogger Logger { get; } = new LoggerConfiguration()
		.MinimumLevel.Verbose()
		.Enrich.FromLogContext()
		.WriteTo.Console()
		.CreateLogger();

	public static void Main() => new Program().MainAsync().GetAwaiter().GetResult();

	public async Task MainAsync() {
		await Log("Main", "Configuring services...", LogSeverity.Info);

		using ServiceProvider services = ConfigureServices();

		client = services.GetRequiredService<DiscordSocketClient>();
		client.Ready += OnClientReady;
		client.Log += LogAsync;

		await client.LoginAsync(TokenType.Bot, ConfigurationManager.AppSettings["DiscordToken"]);
		await client.StartAsync();

		//var config = new CsvConfiguration(System.Globalization.CultureInfo.InvariantCulture) {
		//	HasHeaderRecord = true,
		//};
		//using var reader = new StreamReader("data/puzzles.csv");
		//using var csv = new CsvReader(reader, config);
		//var records = csv.GetRecords<PuzzleRecord>().ToList();
		//var random = new Random();
		//var randomRecord = records[random.Next(records.Count)];

		await Task.Delay(-1);
	}

	private async Task OnClientReady() {
		Console.WriteLine($"Client ready as {client?.CurrentUser.Username}#{client?.CurrentUser.Discriminator}!");
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
			.AddSingleton<IMongoClient>(c => new MongoClient(ConfigurationManager.AppSettings["MongoURI"]));

		SetupMongo(Collection);

		return Collection.BuildServiceProvider();
	}

	private void SetupMongo(IServiceCollection Collection) {
		Collection.AddSingleton(sp => {
			IMongoClient dbClient = sp.GetRequiredService<IMongoClient>();
			return dbClient.GetDatabase("puzzles_bot");
		});
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
