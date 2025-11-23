using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace PuzzlesBot;

public class ChessBoard
{
}

public static class BoardThemes
{
	private static readonly string[] pieces = ["P", "N", "B", "R", "Q", "K"];
	private static readonly List<string> ThemesAvailable = [];
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
