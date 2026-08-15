using System.Text;
using QRCoder;
using Spectre.Console;

namespace Pluck.Cli.Utils;

public static class Utilities
{
    /// <summary>
    /// Generates a crisp, perfectly proportioned terminal QR code using Unicode half-block characters.
    /// </summary>
    public static Markup GetQrCodeMarkup(string url, int quietZone = 0)
    {
        var urlPayload = new PayloadGenerator.Url(url);
        using var qrCodeData = QRCodeGenerator.GenerateQrCode(urlPayload, QRCodeGenerator.ECCLevel.L);
        var matrix = qrCodeData.ModuleMatrix;
        int moduleSize = matrix.Count;
        int totalWidth = moduleSize + quietZone;
        int totalHeight = moduleSize + quietZone;

        bool IsDark(int x, int y)
        {
            int matrixX = x - quietZone;
            int matrixY = y - quietZone;
            if (matrixX < 0 || matrixX >= moduleSize || matrixY < 0 || matrixY >= moduleSize)
            {
                return false; // Quiet zone is light/white
            }
            return matrix[matrixY][matrixX];
        }

        var sb = new StringBuilder();

        for (int y = 0; y < totalHeight; y += 2)
        {
            sb.Append("[black on white]");
            for (int x = 0; x < totalWidth; x++)
            {
                bool topDark = IsDark(x, y);
                bool bottomDark = IsDark(x, y + 1);

                if (topDark && bottomDark)
                {
                    sb.Append('█'); // Full block
                }
                else if (topDark && !bottomDark)
                {
                    sb.Append('▀'); // Top half block
                }
                else if (!topDark && bottomDark)
                {
                    sb.Append('▄'); // Bottom half block
                }
                else
                {
                    sb.Append(' '); // Space (both modules light)
                }
            }
            sb.Append("[/]\n");
        }

        return new Markup(sb.ToString().TrimEnd());
    }
}