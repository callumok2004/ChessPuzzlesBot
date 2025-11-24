using CsvHelper;
using CsvHelper.Configuration;

using Discord;
using Discord.Interactions;

using Microsoft.EntityFrameworkCore;

using PuzzlesBot.Context;

namespace PuzzlesBot;

public class PuzzleRecord {
#pragma warning disable CS8618
	public string PuzzleId { get; set; }
	public string FEN { get; set; }
	public string Moves { get; set; }
	public string Rating { get; set; }
	public string GameUrl { get; set; }
#pragma warning restore CS8618
}

public partial class Interactions {
	static readonly CsvConfiguration CSVConfig = new(System.Globalization.CultureInfo.InvariantCulture) {
		HasHeaderRecord = true
	};
	static StreamReader CSVStreamReader => new("data/puzzles.csv");
	static CsvReader CSVReader => new(CSVStreamReader, CSVConfig);
	public static readonly List<PuzzleRecord> records = [.. CSVReader.GetRecords<PuzzleRecord>()];

	[SlashCommand("testpuzzle", "Force a new daily puzzle")]
	[RequireUserPermission(GuildPermission.Administrator)]
	public async Task TestPuzzleAsync() {
		await DeferAsync(ephemeral: true);

		var server = await db.Servers.FindAsync((long)Context.Guild.Id);
		if (server == null) {
			server = new Servers { ServerId = (long)Context.Guild.Id };
			db.Servers.Add(server);
			await db.SaveChangesAsync();
		}

		if (server.PuzzlesChannel == null) {
			await FollowupAsync("Please set a puzzle channel first using `/setchannel`.", ephemeral: true);
			return;
		}

		await DailyPuzzleService.TriggerDailyPuzzleNow((long)Context.Guild.Id);
		await FollowupAsync("Daily puzzle triggered!", ephemeral: true);
	}

	[SlashCommand("play", "Start the daily puzzle")]
	public async Task PlayPuzzleAsync() {
		await DeferAsync(ephemeral: true);

		var server = await db.Servers.FindAsync((long)Context.Guild.Id);
		if (server == null || server.CurrentPuzzleId == null) {
			await FollowupAsync("No daily puzzle active for this server.", ephemeral: true);
			return;
		}

		var puzzle = await db.Puzzles.FindAsync(server.CurrentPuzzleId);
		if (puzzle == null) {
			await FollowupAsync("Puzzle not found.", ephemeral: true);
			return;
		}

		long userId = (long)Context.User.Id;
		var attempt = await db.PuzzleAttemps.FirstOrDefaultAsync(a => a.Id == puzzle.Id && a.UserId == userId);

		if (attempt == null) {
			attempt = new PuzzleAttemps {
				Id = puzzle.Id,
				Fen = puzzle.Fen,
				Moves = "",
				UserId = userId,
				Failed = 0
			};
			db.PuzzleAttemps.Add(attempt);
		}
		else if (attempt.Failed == 1) {
			await FollowupAsync("You have already attempted today's puzzle, come back tomorrow!", ephemeral: true);
			return;
		}
		else {
			var pMoves = puzzle.Moves.Split(' ');
			var aMoves = string.IsNullOrEmpty(attempt.Moves) ? Array.Empty<string>() : attempt.Moves.Split(' ');
			if (aMoves.Length >= pMoves.Length - 1) {
				await FollowupAsync("You have already completed today's puzzle!", ephemeral: true);
				return;
			}
		}

		var puzzleMoves = puzzle.Moves.Split(' ');
		var playedMoves = string.IsNullOrEmpty(attempt.Moves) ? Array.Empty<string>() : attempt.Moves.Split(' ');

		string currentFen = puzzle.Fen;
		ChessBoard board = new(server.Theme ?? "default");

		List<string> movesToApply = [puzzleMoves[0]];
		movesToApply.AddRange(playedMoves);

		string lastMove = movesToApply.Last();

		for (int i = 0; i < movesToApply.Count - 1; i++) {
			currentFen = board.ApplyFirstMoveToFen(currentFen, movesToApply[i]);
		}

		bool povWhite = !puzzle.Url.Contains("/black#");
		using var stream = board.Render(currentFen, lastMove, povWhite);

		var embed = new EmbedBuilder()
			.WithTitle($"Puzzle {puzzle.Id}")
			.WithDescription("Use `/move <move>` to play (e.g. `/move e2e4`)")
			.WithImageUrl("attachment://board.png")
			.WithColor(Color.Blue);

		var msg = await FollowupWithFileAsync(stream, "board.png", embed: embed.Build(), ephemeral: true);

		attempt.MessageId = (long)msg.Id;
		await db.SaveChangesAsync();
	}

	[SlashCommand("move", "Make a move in the daily puzzle")]
	public async Task MovePuzzleAsync(string move) {
		await DeferAsync(ephemeral: true);

		var server = await db.Servers.FindAsync((long)Context.Guild.Id);
		if (server == null || server.CurrentPuzzleId == null) {
			await FollowupAsync("No daily puzzle active.", ephemeral: true);
			return;
		}

		var puzzle = await db.Puzzles.FindAsync(server.CurrentPuzzleId);
		if (puzzle == null) {
			await FollowupAsync("Puzzle not found.", ephemeral: true);
			return;
		}

		long userId = (long)Context.User.Id;
		var attempt = await db.PuzzleAttemps.FirstOrDefaultAsync(a => a.Id == puzzle.Id && a.UserId == userId);

		if (attempt == null) {
			await FollowupAsync("You haven't started the puzzle yet. Use `/play` first.", ephemeral: true);
			return;
		}

		if (attempt.Failed == 1) {
			await FollowupAsync("You have already attempted today's puzzle, come back tomorrow!", ephemeral: true);
			return;
		}

		var puzzleMoves = puzzle.Moves.Split(' ');
		var playedMoves = string.IsNullOrEmpty(attempt.Moves) ? Array.Empty<string>() : attempt.Moves.Split(' ');

		int expectedIndex = 1 + playedMoves.Length;
		if (expectedIndex >= puzzleMoves.Length) {
			await FollowupAsync("Puzzle already completed!", ephemeral: true);
			return;
		}

		string expectedMove = puzzleMoves[expectedIndex];
		string moveInput = move.Trim();

		string currentFen = puzzle.Fen;
		ChessBoard board = new("default");
		List<string> movesToApply = [puzzleMoves[0]];
		movesToApply.AddRange(playedMoves);

		foreach (var m in movesToApply) {
			currentFen = board.ApplyFirstMoveToFen(currentFen, m);
		}

		string? uciMove = ChessUtils.SanToUci(moveInput, currentFen, expectedMove);

		if (uciMove == null) {
			await FollowupAsync($"Invalid move: `{moveInput}`. Please check your notation or if the move is legal.", ephemeral: true);
			return;
		}

		moveInput = uciMove;

		if (moveInput != expectedMove) {
			attempt.Failed = 1;
			await db.SaveChangesAsync();
			await FollowupAsync($"Incorrect move! You played {moveInput}, but failed.", ephemeral: true);
			await SendPuzzleStateAsync(puzzle, attempt);
			return;
		}

		string newMoves = string.IsNullOrEmpty(attempt.Moves) ? moveInput : attempt.Moves + " " + moveInput;

		int responseIndex = expectedIndex + 1;
		if (responseIndex < puzzleMoves.Length) {
			string responseMove = puzzleMoves[responseIndex];
			newMoves += " " + responseMove;
		}

		attempt.Moves = newMoves;
		await db.SaveChangesAsync();

		bool solved = (responseIndex + 1 >= puzzleMoves.Length);

		if (solved) {
			var userData = await db.Userdata.FindAsync((long)Context.Guild.Id, userId);
			if (userData == null) {
				userData = new Userdata {
					ServerId = (long)Context.Guild.Id,
					UserId = userId,
					Streak = 0,
					LastCompleted = 0
				};
				db.Userdata.Add(userData);
			}

			userData.Streak++;
			userData.LastCompleted = puzzle.Id;
			await db.SaveChangesAsync();

			await FollowupAsync("Puzzle Solved! Great job!", ephemeral: true);

			var channel = await Context.Client.GetChannelAsync((ulong)server.PuzzlesChannel!) as IMessageChannel;
			if (channel != null)
				await channel.SendMessageAsync($"{Context.User.Mention} has just solved today's puzzle! 🎉");

		}
		else {
			await FollowupAsync("Correct! Keep going.", ephemeral: true);
		}

		await SendPuzzleStateAsync(puzzle, attempt);
	}

	private async Task SendPuzzleStateAsync(Puzzles puzzle, PuzzleAttemps attempt) {
		var server = await db.Servers.FindAsync((long)Context.Guild.Id);
		var puzzleMoves = puzzle.Moves.Split(' ');
		var playedMoves = string.IsNullOrEmpty(attempt.Moves) ? Array.Empty<string>() : attempt.Moves.Split(' ');

		string currentFen = puzzle.Fen;
		ChessBoard board = new(server?.Theme ?? "default");

		List<string> movesToApply = [puzzleMoves[0]];
		movesToApply.AddRange(playedMoves);

		string lastMove = movesToApply.Last();

		for (int i = 0; i < movesToApply.Count - 1; i++) {
			currentFen = board.ApplyFirstMoveToFen(currentFen, movesToApply[i]);
		}

		bool povWhite = !puzzle.Url.Contains("/black#");
		using var stream = board.Render(currentFen, lastMove, povWhite);

		var embed = new EmbedBuilder()
			.WithImageUrl("attachment://board.png")
			.WithColor(attempt.Failed == 1 ? Color.Red : Color.Blue);

		if (attempt.Failed == 1) {
			embed.WithDescription("Oh no! You have failed today's puzzle, come back tomorrow for a new one!\nGame URL: " + puzzle.Url);
		}
		else if (playedMoves.Length + 1 >= puzzleMoves.Length) {
			embed.WithDescription("Puzzle Solved!");
			embed.WithColor(Color.Green);
		}
		else {
			embed.WithDescription("Use `/move <move>`, e.g. `/move e2e4` or `/move Nf3`.");
		}

		await FollowupWithFileAsync(stream, "board.png", embed: embed.Build(), ephemeral: true);
	}
}


