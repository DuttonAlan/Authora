// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using System.Globalization;
using System.Text;

namespace Authora.Api.QrCodes;

/// <summary>
///     A QR code (ISO/IEC 18004) of text in byte mode with error correction level M, rendered on the server as an SVG
///     path, so a page can show it without JavaScript, images or inline styles.
/// </summary>
/// <remarks>
///     Follows the reference algorithm of the standard: the smallest version (1 to 40) that fits, Reed-Solomon error
///     correction in interleaved blocks, and the mask with the lowest penalty.
/// </remarks>
public sealed class QrCode
{
    /// <summary>
    ///     Width of the light border around the code in modules, as the standard requires.
    /// </summary>
    public const int QuietZone = 4;

    // Level M, indexed by version (index 0 unused).
    private static readonly int[] EccCodewordsPerBlock =
    [
        -1, 10, 16, 26, 18, 24, 16, 18, 22, 22, 26, 30, 22, 22, 24, 24, 28, 28, 26, 26, 26,
        26, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28
    ];

    private static readonly int[] NumErrorCorrectionBlocks =
    [
        -1, 1, 1, 1, 2, 2, 4, 4, 4, 5, 5, 5, 8, 9, 9, 10, 10, 11, 13, 14, 16,
        17, 17, 18, 20, 21, 23, 25, 26, 28, 29, 31, 33, 35, 37, 38, 40, 43, 45, 47, 49
    ];

    // The two format bits of level M.
    private const int FormatBitsOfLevel = 0;

    private readonly bool[,] _modules;

    private readonly bool[,] _isFunction;

    private readonly int _version;

    private QrCode(int version, byte[] dataCodewords)
    {
        _version = version;
        Size = (version * 4) + 17;
        _modules = new bool[Size, Size];
        _isFunction = new bool[Size, Size];

        DrawFunctionPatterns();
        DrawCodewords(AddEccAndInterleave(dataCodewords));

        var bestMask = 0;
        var minPenalty = int.MaxValue;

        for (var mask = 0; mask < 8; mask++)
        {
            ApplyMask(mask);
            DrawFormatBits(mask);

            var penalty = GetPenaltyScore();

            if (penalty < minPenalty)
            {
                bestMask = mask;
                minPenalty = penalty;
            }

            // The mask is an XOR, so applying it again undoes it.
            ApplyMask(mask);
        }

        ApplyMask(bestMask);
        DrawFormatBits(bestMask);
    }

    /// <summary>
    ///     Width and height of the code in modules, without the <see cref="QuietZone"/>.
    /// </summary>
    public int Size { get; }

    /// <summary>
    ///     Encodes text as UTF-8.
    /// </summary>
    /// <param name="text">The text, e.g. an <c>otpauth://</c> URI.</param>
    /// <returns>The QR code.</returns>
    /// <exception cref="ArgumentException">Thrown when the text is too long for a QR code.</exception>
    public static QrCode Encode(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var data = Encoding.UTF8.GetBytes(text);

        for (var version = 1; version <= 40; version++)
        {
            var capacityBits = GetNumDataCodewords(version) * 8;
            var countBits = version <= 9 ? 8 : 16;

            if (4 + countBits + (data.Length * 8) <= capacityBits && data.Length < (1 << countBits))
            {
                return new QrCode(version, CreateDataCodewords(data, countBits, capacityBits));
            }
        }

        throw new ArgumentException("The text is too long for a QR code.", nameof(text));
    }

    /// <summary>
    ///     Checks whether a module is dark.
    /// </summary>
    /// <param name="x">The column, from 0 at the left.</param>
    /// <param name="y">The row, from 0 at the top.</param>
    /// <returns><see langword="true"/> if the module is dark.</returns>
    public bool IsDark(int x, int y) => _modules[y, x];

    /// <summary>
    ///     Builds the <c>d</c> attribute of an SVG path that draws the dark modules, offset by the
    ///     <see cref="QuietZone"/>. With a <c>viewBox</c> of <c>0 0 n n</c> for n = <see cref="Size"/> + 2 ×
    ///     <see cref="QuietZone"/>, one module is one unit.
    /// </summary>
    /// <returns>The path data: digits, spaces and the commands <c>M</c>, <c>h</c>, <c>v</c> and <c>z</c> only.</returns>
    public string ToSvgPath()
    {
        var builder = new StringBuilder();

        for (var y = 0; y < Size; y++)
        {
            var x = 0;

            while (x < Size)
            {
                if (!_modules[y, x])
                {
                    x++;

                    continue;
                }

                // One rectangle per horizontal run of dark modules.
                var start = x;

                while (x < Size && _modules[y, x])
                {
                    x++;
                }

                builder.Append(CultureInfo.InvariantCulture, $"M{start + QuietZone} {y + QuietZone}h{x - start}v1h-{x - start}z");
            }
        }

        return builder.ToString();
    }

    private static byte[] CreateDataCodewords(byte[] data, int countBits, int capacityBits)
    {
        var bits = new List<bool>(capacityBits);

        AppendBits(bits, 0b0100, 4);
        AppendBits(bits, data.Length, countBits);

        foreach (var value in data)
        {
            AppendBits(bits, value, 8);
        }

        // Terminator, padding to a whole byte, then the alternating pad bytes.
        AppendBits(bits, 0, Math.Min(4, capacityBits - bits.Count));
        AppendBits(bits, 0, (8 - (bits.Count % 8)) % 8);

        for (var pad = 0xEC; bits.Count < capacityBits; pad ^= 0xEC ^ 0x11)
        {
            AppendBits(bits, pad, 8);
        }

        var codewords = new byte[bits.Count / 8];

        for (var index = 0; index < bits.Count; index++)
        {
            if (bits[index])
            {
                codewords[index >> 3] |= (byte)(1 << (7 - (index & 7)));
            }
        }

        return codewords;
    }

    private static void AppendBits(List<bool> bits, int value, int length)
    {
        for (var index = length - 1; index >= 0; index--)
        {
            bits.Add(((value >> index) & 1) != 0);
        }
    }

    private static int GetNumRawDataModules(int version)
    {
        var result = (((16 * version) + 128) * version) + 64;

        if (version >= 2)
        {
            var numAlign = (version / 7) + 2;
            result -= (((25 * numAlign) - 10) * numAlign) - 55;

            if (version >= 7)
            {
                result -= 36;
            }
        }

        return result;
    }

    private static int GetNumDataCodewords(int version)
    {
        return (GetNumRawDataModules(version) / 8) - (EccCodewordsPerBlock[version] * NumErrorCorrectionBlocks[version]);
    }

    private static bool GetBit(int value, int index) => ((value >> index) & 1) != 0;

    private void SetFunction(int x, int y, bool isDark)
    {
        _modules[y, x] = isDark;
        _isFunction[y, x] = true;
    }

    private void DrawFunctionPatterns()
    {
        for (var index = 0; index < Size; index++)
        {
            SetFunction(6, index, index % 2 == 0);
            SetFunction(index, 6, index % 2 == 0);
        }

        DrawFinderPattern(3, 3);
        DrawFinderPattern(Size - 4, 3);
        DrawFinderPattern(3, Size - 4);

        var positions = GetAlignmentPatternPositions();
        var count = positions.Length;

        for (var i = 0; i < count; i++)
        {
            for (var j = 0; j < count; j++)
            {
                // Not where the finder patterns are.
                if (!((i == 0 && j == 0) || (i == 0 && j == count - 1) || (i == count - 1 && j == 0)))
                {
                    DrawAlignmentPattern(positions[i], positions[j]);
                }
            }
        }

        // Reserves the area of the format bits; the real ones are drawn with the mask.
        DrawFormatBits(0);
        DrawVersion();
    }

    private void DrawFormatBits(int mask)
    {
        var data = (FormatBitsOfLevel << 3) | mask;
        var remainder = data;

        for (var index = 0; index < 10; index++)
        {
            remainder = (remainder << 1) ^ ((remainder >> 9) * 0x537);
        }

        var bits = ((data << 10) | remainder) ^ 0x5412;

        // First copy, around the top left finder pattern.
        for (var index = 0; index <= 5; index++)
        {
            SetFunction(8, index, GetBit(bits, index));
        }

        SetFunction(8, 7, GetBit(bits, 6));
        SetFunction(8, 8, GetBit(bits, 7));
        SetFunction(7, 8, GetBit(bits, 8));

        for (var index = 9; index < 15; index++)
        {
            SetFunction(14 - index, 8, GetBit(bits, index));
        }

        // Second copy, split between the other two finder patterns.
        for (var index = 0; index < 8; index++)
        {
            SetFunction(Size - 1 - index, 8, GetBit(bits, index));
        }

        for (var index = 8; index < 15; index++)
        {
            SetFunction(8, Size - 15 + index, GetBit(bits, index));
        }

        // Always dark.
        SetFunction(8, Size - 8, true);
    }

    private void DrawVersion()
    {
        if (_version < 7)
        {
            return;
        }

        var remainder = _version;

        for (var index = 0; index < 12; index++)
        {
            remainder = (remainder << 1) ^ ((remainder >> 11) * 0x1F25);
        }

        var bits = (_version << 12) | remainder;

        for (var index = 0; index < 18; index++)
        {
            var bit = GetBit(bits, index);
            var a = Size - 11 + (index % 3);
            var b = index / 3;

            SetFunction(a, b, bit);
            SetFunction(b, a, bit);
        }
    }

    private void DrawFinderPattern(int x, int y)
    {
        for (var dy = -4; dy <= 4; dy++)
        {
            for (var dx = -4; dx <= 4; dx++)
            {
                var distance = Math.Max(Math.Abs(dx), Math.Abs(dy));
                var xx = x + dx;
                var yy = y + dy;

                if (xx >= 0 && xx < Size && yy >= 0 && yy < Size)
                {
                    SetFunction(xx, yy, distance != 2 && distance != 4);
                }
            }
        }
    }

    private void DrawAlignmentPattern(int x, int y)
    {
        for (var dy = -2; dy <= 2; dy++)
        {
            for (var dx = -2; dx <= 2; dx++)
            {
                SetFunction(x + dx, y + dy, Math.Max(Math.Abs(dx), Math.Abs(dy)) != 1);
            }
        }
    }

    private int[] GetAlignmentPatternPositions()
    {
        if (_version == 1)
        {
            return [];
        }

        var count = (_version / 7) + 2;
        var step = (((_version * 8) + (count * 3) + 5) / ((count * 4) - 4)) * 2;
        var result = new int[count];

        result[0] = 6;

        for (int index = count - 1, position = Size - 7; index >= 1; index--, position -= step)
        {
            result[index] = position;
        }

        return result;
    }

    private byte[] AddEccAndInterleave(byte[] data)
    {
        var numBlocks = NumErrorCorrectionBlocks[_version];
        var blockEccLength = EccCodewordsPerBlock[_version];
        var rawCodewords = GetNumRawDataModules(_version) / 8;
        var numShortBlocks = numBlocks - (rawCodewords % numBlocks);
        var shortBlockLength = rawCodewords / numBlocks;

        var divisor = ReedSolomonComputeDivisor(blockEccLength);
        var blocks = new List<byte[]>(numBlocks);

        for (int index = 0, offset = 0; index < numBlocks; index++)
        {
            var dataLength = shortBlockLength - blockEccLength + (index < numShortBlocks ? 0 : 1);
            var blockData = data.AsSpan(offset, dataLength).ToArray();
            offset += dataLength;

            var block = new byte[shortBlockLength + 1];
            blockData.CopyTo(block, 0);
            ReedSolomonComputeRemainder(blockData, divisor).CopyTo(block, block.Length - blockEccLength);

            blocks.Add(block);
        }

        var result = new byte[rawCodewords];
        var resultIndex = 0;

        for (var index = 0; index < blocks[0].Length; index++)
        {
            for (var blockIndex = 0; blockIndex < blocks.Count; blockIndex++)
            {
                // Short blocks have no data codeword at this position.
                if (index != shortBlockLength - blockEccLength || blockIndex >= numShortBlocks)
                {
                    result[resultIndex++] = blocks[blockIndex][index];
                }
            }
        }

        return result;
    }

    private void DrawCodewords(byte[] data)
    {
        var bitIndex = 0;

        // Zigzag in columns of two from the bottom right, skipping the vertical timing pattern.
        for (var right = Size - 1; right >= 1; right -= 2)
        {
            if (right == 6)
            {
                right = 5;
            }

            for (var vertical = 0; vertical < Size; vertical++)
            {
                for (var j = 0; j < 2; j++)
                {
                    var x = right - j;
                    var upward = ((right + 1) & 2) == 0;
                    var y = upward ? Size - 1 - vertical : vertical;

                    if (!_isFunction[y, x] && bitIndex < data.Length * 8)
                    {
                        _modules[y, x] = GetBit(data[bitIndex >> 3], 7 - (bitIndex & 7));
                        bitIndex++;
                    }
                }
            }
        }
    }

    private void ApplyMask(int mask)
    {
        for (var y = 0; y < Size; y++)
        {
            for (var x = 0; x < Size; x++)
            {
                var invert = mask switch
                {
                    0 => (x + y) % 2 == 0,
                    1 => y % 2 == 0,
                    2 => x % 3 == 0,
                    3 => (x + y) % 3 == 0,
                    4 => ((x / 3) + (y / 2)) % 2 == 0,
                    5 => ((x * y) % 2) + ((x * y) % 3) == 0,
                    6 => (((x * y) % 2) + ((x * y) % 3)) % 2 == 0,
                    _ => (((x + y) % 2) + ((x * y) % 3)) % 2 == 0
                };

                if (invert && !_isFunction[y, x])
                {
                    _modules[y, x] = !_modules[y, x];
                }
            }
        }
    }

    private int GetPenaltyScore()
    {
        const int PenaltyN1 = 3;
        const int PenaltyN2 = 3;
        const int PenaltyN3 = 40;
        const int PenaltyN4 = 10;

        var result = 0;

        for (var pass = 0; pass < 2; pass++)
        {
            // Rows, then columns: runs of five or more modules of one color, and patterns like finder patterns.
            for (var line = 0; line < Size; line++)
            {
                var runColor = false;
                var runLength = 0;
                var runHistory = new int[7];

                for (var position = 0; position < Size; position++)
                {
                    var isDark = pass == 0 ? _modules[line, position] : _modules[position, line];

                    if (isDark == runColor)
                    {
                        runLength++;

                        if (runLength == 5)
                        {
                            result += PenaltyN1;
                        }
                        else if (runLength > 5)
                        {
                            result++;
                        }
                    }
                    else
                    {
                        AddRunToHistory(runLength, runHistory);

                        if (!runColor)
                        {
                            result += CountFinderLikePatterns(runHistory) * PenaltyN3;
                        }

                        runColor = isDark;
                        runLength = 1;
                    }
                }

                result += TerminateRunsAndCount(runColor, runLength, runHistory) * PenaltyN3;
            }
        }

        // Blocks of 2 × 2 modules of one color.
        for (var y = 0; y < Size - 1; y++)
        {
            for (var x = 0; x < Size - 1; x++)
            {
                var color = _modules[y, x];

                if (color == _modules[y, x + 1] && color == _modules[y + 1, x] && color == _modules[y + 1, x + 1])
                {
                    result += PenaltyN2;
                }
            }
        }

        // Balance of dark and light modules.
        var dark = 0;

        foreach (var module in _modules)
        {
            if (module)
            {
                dark++;
            }
        }

        var total = Size * Size;
        var k = ((Math.Abs((dark * 20) - (total * 10)) + total - 1) / total) - 1;

        return result + (k * PenaltyN4);
    }

    private int CountFinderLikePatterns(int[] runHistory)
    {
        var n = runHistory[1];
        var core = n > 0 && runHistory[2] == n && runHistory[3] == n * 3 && runHistory[4] == n && runHistory[5] == n;

        return (core && runHistory[0] >= n * 4 && runHistory[6] >= n ? 1 : 0)
            + (core && runHistory[6] >= n * 4 && runHistory[0] >= n ? 1 : 0);
    }

    private int TerminateRunsAndCount(bool runColor, int runLength, int[] runHistory)
    {
        // The light border beyond the edge counts as part of the last light run.
        if (runColor)
        {
            AddRunToHistory(runLength, runHistory);
            runLength = 0;
        }

        runLength += Size;
        AddRunToHistory(runLength, runHistory);

        return CountFinderLikePatterns(runHistory);
    }

    private void AddRunToHistory(int runLength, int[] runHistory)
    {
        // The light border before the edge counts as part of the first light run.
        if (runHistory[0] == 0)
        {
            runLength += Size;
        }

        Array.Copy(runHistory, 0, runHistory, 1, runHistory.Length - 1);
        runHistory[0] = runLength;
    }

    private static byte[] ReedSolomonComputeDivisor(int degree)
    {
        var result = new byte[degree];
        result[degree - 1] = 1;

        var root = 1;

        for (var i = 0; i < degree; i++)
        {
            for (var j = 0; j < degree; j++)
            {
                result[j] = ReedSolomonMultiply(result[j], root);

                if (j + 1 < degree)
                {
                    result[j] ^= result[j + 1];
                }
            }

            root = ReedSolomonMultiply(root, 0x02);
        }

        return result;
    }

    private static byte[] ReedSolomonComputeRemainder(byte[] data, byte[] divisor)
    {
        var result = new byte[divisor.Length];

        foreach (var value in data)
        {
            var factor = value ^ result[0];

            Array.Copy(result, 1, result, 0, result.Length - 1);
            result[^1] = 0;

            for (var index = 0; index < result.Length; index++)
            {
                result[index] ^= ReedSolomonMultiply(divisor[index], factor);
            }
        }

        return result;
    }

    // Multiplication in GF(2^8) modulo x^8 + x^4 + x^3 + x^2 + 1.
    private static byte ReedSolomonMultiply(int x, int y)
    {
        var z = 0;

        for (var index = 7; index >= 0; index--)
        {
            z = (z << 1) ^ ((z >> 7) * 0x11D);
            z ^= ((y >> index) & 1) * x;
        }

        return (byte)z;
    }
}
