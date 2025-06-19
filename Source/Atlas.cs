// 25% ai generated
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Monocle;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Celeste.Mod.FontCustomizer
{
    class GlyphAtlasPage(int width, int height)
    {
        public int AtlasWidth = width;
        public int AtlasHeight = height;
#if DEBUG
        public const int Padding = 2;
        public const int PageSize = 512;
#else
        public const int Padding = 1;
        public const int PageSize = 2048;
#endif
        private readonly List<SkylineData> skyline = [new(0, 0, width)];
        public Lazy<RuntimeTexture> _AtlasTexture = new(() => new("AtlasPage", width, height, Color.White));
        public Lazy<bool> WriteDebugInfo = new(() =>
        {
            try
            {
                if (File.Exists(Path.Combine(Everest.PathEverest, "FontCustomizerWriteResult")))
                {
                    return true;
                }
            }
            catch { }
            return false;
        });
        public RuntimeTexture AtlasTexture => _AtlasTexture.Value;

        public bool TryAdd(int glyphWidth, int glyphHeight, out Rectangle region)
        {
            if (disposed)
            {
                region = default;
                return false;
            }
            glyphWidth += Padding * 2;
            glyphHeight += Padding * 2;

            int bestY = int.MaxValue;
            int bestX = -1;
            int bestIndex = -1;

            for (int i = 0; i < skyline.Count; i++)
            {
                int x = skyline[i].x;
                if (TryFit(i, glyphWidth, glyphHeight, out int y))
                {
                    if (y < bestY || (y == bestY && x < bestX))
                    {
                        bestY = y;
                        bestX = x;
                        bestIndex = i;
                    }
                }
            }

            if (bestIndex == -1)
            {
                region = Rectangle.Empty;
                try
                {
                    if (WriteDebugInfo.Value)
                    {
                        using var f = File.OpenWrite($"R:/temp{Random.Shared.Next()}.png");
                        AtlasTexture.Texture.SaveAsPng(f, AtlasWidth, AtlasHeight);
                    }
                }
                catch { }
                Full = true;
                return false;
            }

            UpdateSkyline(bestIndex, new(bestX, bestY + glyphHeight, glyphWidth));
            region = new Rectangle(bestX + Padding, bestY + Padding, glyphWidth - Padding * 2, glyphHeight - Padding * 2);
            return true;
        }

        private bool TryFit(int index, int width, int height, out int y)
        {
            int x = skyline[index].x;
            int maxY = skyline[index].y;
            int remaining = width;

            for (int i = index; i < skyline.Count && remaining > 0; i++)
            {
                if (skyline[i].y > maxY)
                    maxY = skyline[i].y;

                remaining -= skyline[i].width;
            }

            y = maxY;
            return (remaining <= 0) && (x + width <= AtlasWidth) && (maxY + height <= AtlasHeight);
        }

        private void UpdateSkyline(int index, SkylineData sky)
        {
            int from = index;
            int end = sky.x + sky.width;
            while (skyline[index].x + skyline[index].width < end)
            {
                index++;
            }
            int curend = skyline[index].x + skyline[index].width;
            int cury = skyline[index].y;
            index++;
            SkylineData? rest = null;
            int inserts = 1;
            if (curend > end)
            {
                rest = new(end, cury, curend - end);
                inserts++;
            }
            skyline.RemoveRangeAndReserve(from, index - from, inserts);
            skyline[from] = sky;
            if (rest is { } r)
            {
                skyline[from + 1] = r;
            }
        }
        class Collect(GlyphAtlasPage page) : IDisposable
        {
            bool disposed;
            public void Dispose()
            {
                if (!disposed && Interlocked.Decrement(ref page.RefCount) == 0 && page.Full && !page.disposed)
                {
                    page.skyline.Clear();
                    page.skyline.Add(new(0, 0, page.AtlasWidth));
                    page.AtlasTexture.Texture.SetData(Enumerable.Repeat(Color.Transparent, page.AtlasWidth * page.AtlasHeight).ToArray());
                    page.collector?.pool.Add(page);
                }
                disposed = true;
            }
        }
        internal IDisposable AddReference()
        {
            Interlocked.Increment(ref RefCount);
            return new Collect(this);
        }
        internal void Dispose()
        {
            if (disposed)
            {
                return;
            }
            AtlasTexture?.Dispose();
        }
        bool disposed;
        internal bool Full = false;
        internal int RefCount = 0;
        internal GlyphAtlas? collector = null;
    }

    public record struct AllocatedMTexture(MTexture Texture, IDisposable Dispose);

    class GlyphAtlas(int width = GlyphAtlasPage.PageSize, int height = GlyphAtlasPage.PageSize)
    {
        const int Padding = GlyphAtlasPage.Padding;
        internal readonly ConcurrentBag<GlyphAtlasPage> pool = [];
        GlyphAtlasPage current = new(width, height);

        public AllocatedMTexture Allocate(Color[] glyphTex, int glyphWidth, int glyphHeight)
        {
            bool success = false;
            AllocatedMTexture applyOn(GlyphAtlasPage cur)
            {
                success = true;
                if (cur.TryAdd(glyphWidth, glyphHeight, out Rectangle region))
                {
                    var clip = region;
#if DEBUG
                    var _padding = Padding;
                    int paddedWidth = region.Width + _padding * 2;
                    int paddedHeight = region.Height + _padding * 2;
                    Rectangle paddedRegion = new(region.X - _padding, region.Y - _padding, paddedWidth, paddedHeight);
                    _padding--;
                    Color[] paddedData = new Color[paddedWidth * paddedHeight];

                    for (int y = 0; y < paddedHeight; y++)
                    {
                        for (int x = 0; x < paddedWidth; x++)
                        {
                            if (x < _padding || x >= paddedWidth - _padding ||
                                             y < _padding || y >= paddedHeight - _padding)
                            {
                                paddedData[y * paddedWidth + x] = Color.Red;
                            }
                        }
                    }

                    for (int y = 0; y < region.Height; y++)
                    {
                        Array.Copy(
                            glyphTex, y * region.Width,
                            paddedData, (y + (_padding + 1)) * paddedWidth + _padding + 1,
                            region.Width);
                    }
                    region = paddedRegion;
                    glyphTex = paddedData;
#endif
                    cur.AtlasTexture.Texture_Safe.SetData(0, region, glyphTex, 0, glyphTex.Length);


                    return new(
                        new(new(cur.AtlasTexture), clip),
                        cur.AddReference());
                }
                success = false;
                return default;
            }
            AllocatedMTexture value = applyOn(current);
            if (success)
            {
                return value;
            }

            current.collector = this;
            if (!pool.TryTake(out var newPage))
            {
                newPage = new(width, height);
            }
            current = newPage;

            value = applyOn(newPage);
            if (success)
            {
                return value;
            }
            else
            {
                throw new Exception("Glyph too large to fit in empty atlas page.");
            }
        }
    }

    internal record struct SkylineData(int x, int y, int width);
    static class Helper
    {
        internal static void RemoveRangeAndReserve<T>(this List<T?> self, int from, int removecnt, int rescnt)
        {
            if (removecnt - rescnt < 0)
            {
                self.InsertRange(from, Enumerable.Repeat(default(T), rescnt - removecnt));
            }
            else if (removecnt - rescnt > 0)
            {
                self.RemoveRange(from, removecnt - rescnt);
            }
        }
    }
}
