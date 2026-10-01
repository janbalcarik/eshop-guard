using EshopGuard.Core.Extract;

namespace EshopGuard.Core.Segmentation;

/// <summary>
/// A part of the main text of a page for the sieve: consecutive text blocks, at most
/// <c>maxChars</c> characters unless a single block is longer.
/// </summary>
/// <param name="Index">Position of the chunk on the page, from 0.</param>
/// <param name="FirstBlock">Index of the first main block of the chunk.</param>
/// <param name="LastBlock">Index of the last main block of the chunk.</param>
/// <param name="Text">Texts of the blocks joined by line breaks.</param>
internal sealed record SieveChunk(int Index, int FirstBlock, int LastBlock, string Text);

/// <summary>
/// Cuts the main text of a page into sieve chunks. Blocks are never split, so every sentence belongs to exactly one chunk.
/// </summary>
internal static class SieveChunker
{
    public static List<SieveChunk> Chunk(IReadOnlyList<TextBlock> blocks, int maxChars)
    {
        var chunks = new List<SieveChunk>();
        var first = 0;
        var length = 0;
        for (var i = 0; i < blocks.Count; i++)
        {
            var add = blocks[i].Text.Length + (i > first ? 1 : 0);
            if (i > first && length + add > maxChars)
            {
                chunks.Add(Create(chunks.Count, first, i - 1, blocks));
                first = i;
                length = blocks[i].Text.Length;
            }
            else
            {
                length += add;
            }
        }

        if (blocks.Count > 0)
        {
            chunks.Add(Create(chunks.Count, first, blocks.Count - 1, blocks));
        }

        return chunks;
    }

    /// <summary>Chunk index of every block.</summary>
    public static int[] ChunkOfBlock(IReadOnlyList<SieveChunk> chunks, int blockCount)
    {
        var map = new int[blockCount];
        foreach (var chunk in chunks)
        {
            for (var b = chunk.FirstBlock; b <= chunk.LastBlock; b++)
            {
                map[b] = chunk.Index;
            }
        }

        return map;
    }

    private static SieveChunk Create(int index, int first, int last, IReadOnlyList<TextBlock> blocks) =>
        new(index, first, last, string.Join("\n", blocks.Skip(first).Take(last - first + 1).Select(b => b.Text)));
}
