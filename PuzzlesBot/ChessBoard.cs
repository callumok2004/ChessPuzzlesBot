using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.Processing;
using SixLabors.Fonts;
using System.Text;

namespace PuzzlesBot;

public class ChessBoard
{
	private readonly string theme;
	private const string ThemesDir = "data/assets/themes";
	private readonly Font font;

	public ChessBoard(string theme) {
		this.theme = theme;

		var collection = new FontCollection();
		var family = collection.Add("data/assets/font.otf");
		font = family.CreateFont(14);
	}

	public MemoryStream Render(string fen, string firstMove, bool povWhite) {
		if (!string.IsNullOrWhiteSpace(firstMove) && firstMove.Length >= 4)
			fen = ApplyFirstMoveToFen(fen, firstMove);

		string[] ranks = fen.Split(' ')[0].Split('/');
		int size = 100;
		int margin = 25;
		Image<Rgba32> board = new(size * 8 + margin, size * 8 + margin);

		for (int rank = 0; rank < 8; rank++) {
			int file = 0;
			foreach (char c in ranks[rank]) {
				if (char.IsDigit(c)) {
					int empties = c - '0';
					for (int k = 0; k < empties; k++) {
						bool light2 = ((rank + file + k) % 2 == 0);
						string blankName = light2 ? "blank-light.png" : "blank-dark.png";
						DrawSquare(board, rank, file + k, blankName, size, povWhite);
					}
					file += empties;
					continue;
				}

				bool whitePiece = char.IsUpper(c);
				string piece = char.ToUpper(c).ToString();
				bool light = ((rank + file) % 2 == 0);

				string name =
				  (whitePiece ? "W" : "B") +
				  piece +
				  (light ? "-light.png" : "-dark.png");

				DrawSquare(board, rank, file, name, size, povWhite);
				file++;
			}

			for (; file < 8; file++) {
				bool light = ((rank + file) % 2 == 0);
				string name = light ? "blank-light.png" : "blank-dark.png";
				DrawSquare(board, rank, file, name, size, povWhite);
			}
		}

		if (!string.IsNullOrWhiteSpace(firstMove) && firstMove.Length >= 4) {
			string fromSq = firstMove[..2];
			string toSq = firstMove.Substring(2, 2);
			DrawHighlight(board, fromSq, size, povWhite);
			DrawHighlight(board, toSq, size, povWhite);
		}

		AddCoords(board, size, povWhite);

		var stream = new MemoryStream();
		board.SaveAsPng(stream);
		stream.Position = 0;
		return stream;
	}

	public static string ApplyFirstMoveToFen(string fen, string firstMove) {
		string from = firstMove[..2];
		string to = firstMove.Substring(2, 2);
		char? promoPiece = firstMove.Length >= 5 ? firstMove[4] : null;

		string[] fenParts = fen.Split(' ');
		if (fenParts.Length == 0)
			return fen;

		char[,] boardArray = FenToArray(fenParts[0]);

		(int fromRank, int fromFile) = SquareToIndices(from);
		(int toRank, int toFile) = SquareToIndices(to);

		char piece = boardArray[fromRank, fromFile];
		if (piece == '.')
			return fen;

		boardArray[fromRank, fromFile] = '.';
		if (promoPiece.HasValue) {
			bool isWhite = char.IsUpper(piece);
			char p = char.ToLower(promoPiece.Value);
			boardArray[toRank, toFile] = isWhite ? char.ToUpper(p) : p;
		} else {
			boardArray[toRank, toFile] = piece;
		}

		fenParts[0] = ArrayToFen(boardArray);

		if (fenParts.Length > 1) {
			fenParts[1] = fenParts[1] == "w" ? "b" : "w";
		}

		return string.Join(' ', fenParts);
	}

	private static (int rank, int file) SquareToIndices(string square) {
		int file = square[0] - 'a';
		int rank = 8 - (square[1] - '0');
		return (rank, file);
	}

	private static char[,] FenToArray(string placement) {
		char[,] board = new char[8, 8];
		for (int r = 0; r < 8; r++)
			for (int f = 0; f < 8; f++)
				board[r, f] = '.';

		string[] ranks = placement.Split('/');
		for (int r = 0; r < 8; r++) {
			int file = 0;
			foreach (char c in ranks[r]) {
				if (char.IsDigit(c)) {
					file += c - '0';
				} else {
					board[r, file] = c;
					file++;
				}
			}
		}
		return board;
	}

	private static string ArrayToFen(char[,] board) {
		StringBuilder sb = new();
		for (int r = 0; r < 8; r++) {
			if (r > 0)
				sb.Append('/');
			int empty = 0;
			for (int f = 0; f < 8; f++) {
				char c = board[r, f];
				if (c == '.') {
					empty++;
				} else {
					if (empty > 0) {
						sb.Append(empty);
						empty = 0;
					}
					sb.Append(c);
				}
			}
			if (empty > 0)
				sb.Append(empty);
		}
		return sb.ToString();
	}

	private void DrawSquare(Image<Rgba32> canvas, int rank, int file, string fileName, int size, bool povWhite) {
		string path = Path.Combine(ThemesDir, theme, fileName);
		using Image<Rgba32> square = Image.Load<Rgba32>(path);
		square.Mutate(x => x.Resize(size, size));

		int viewRank = povWhite ? rank : 7 - rank;
		int viewFile = povWhite ? file : 7 - file;

		int x = viewFile * size;
		int y = viewRank * size;

		canvas.Mutate(c => c.DrawImage(square, new Point(x, y), 1f));
	}

	private void AddCoords(Image<Rgba32> img, int size, bool povWhite) {
		string files = povWhite ? "abcdefgh" : "hgfedcba";
		string ranks = povWhite ? "87654321" : "12345678";
		var mainColor = new Rgba32(255, 255, 255);
		var shadowColor = new Rgba32(0, 0, 0);
		int margin = size / 8;

		for (int i = 0; i < 8; i++) {
			float xFile = i * size + margin;
			float yFile = size * 8 + margin / 2f;

			float xRank = margin / 2f;
			float yRank = i * size + margin;

			img.Mutate(c =>
			{
				c.DrawText(files[i].ToString(), font, shadowColor, new PointF(xFile + 1, yFile + 1));
				c.DrawText(files[i].ToString(), font, mainColor, new PointF(xFile, yFile));
				c.DrawText(ranks[i].ToString(), font, shadowColor, new PointF(xRank + 1, yRank + 1));
				c.DrawText(ranks[i].ToString(), font, mainColor, new PointF(xRank, yRank));
			});
		}
	}


	private static void DrawHighlight(Image<Rgba32> canvas, string square, int size, bool povWhite) {
		int file = square[0] - 'a';
		int rank = 8 - (square[1] - '0');
		int viewRank = povWhite ? rank : 7 - rank;
		int viewFile = povWhite ? file : 7 - file;
		int x = viewFile * size;
		int y = viewRank * size;
		var highlightColor = new Rgba32(255, 255, 0, 255);
		int thickness = Math.Max(4, size / 20);
		var rect = new Rectangle(x + thickness / 2, y + thickness / 2, size - thickness, size - thickness);
		canvas.Mutate(c => c.Draw(highlightColor, thickness, rect));
	}
}

public static class BoardThemes
{
	private static readonly string[] pieces = ["P", "N", "B", "R", "Q", "K"];
	public static readonly List<string> ThemesAvailable = [];
	private const string ThemesDir = "data/assets/themes";

	public static void LoadTheme(string theme) {
		ThemesAvailable.Add(theme);

		if (Directory.Exists(Path.Combine(ThemesDir, theme)))
			return;

		using Image im = Image.Load<Rgba32>(Path.Combine(ThemesDir, $"{theme}.png"));
		Directory.CreateDirectory(Path.Combine(ThemesDir, theme));
		int squareLen = im.Width / 8;

		for (int p = 0; p < 6; p++) {
			int row = 6 - (p / 4) * 2;
			int col = (2 * p) % 8;
			var squares = new (int, int)[] { (row + 1, col), (row + 1, col + 1), (row, col), (row, col + 1) };

			for (int s = 0; s < 4; s++) {
				string name = (s < 2 ? "W" : "B") + pieces[p] + ((squares[s].Item1 + squares[s].Item2) % 2 == 0 ? "-light.png" : "-dark.png");
				var crop = im.Clone(ctx => ctx.Crop(new Rectangle(squares[s].Item2 * squareLen, squares[s].Item1 * squareLen, squareLen, squareLen)));
				crop.Save(Path.Combine(ThemesDir, theme, name));
			}
		}

		Image blankLight = im.Clone(ctx => ctx.Crop(new Rectangle(0, 0, squareLen, squareLen)));
		Image blankDark = im.Clone(ctx => ctx.Crop(new Rectangle(0, squareLen, squareLen, squareLen)));
		blankLight.Save(Path.Combine(ThemesDir, theme, "blank-light.png"));
		blankDark.Save(Path.Combine(ThemesDir, theme, "blank-dark.png"));
	}

	public static void LoadAllThemes() {
		foreach (var file in Directory.GetFiles(ThemesDir, "*.png"))
			LoadTheme(Path.GetFileNameWithoutExtension(file));
	}
}
