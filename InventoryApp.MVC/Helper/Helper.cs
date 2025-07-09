using Microsoft.EntityFrameworkCore;
using SkiaSharp;
using ZXing;
using ZXing.SkiaSharp.Rendering;
using ZXing.SkiaSharp;
using System.IO;

namespace InventoryApp.Helper
{
    public class Helper
    {
        public static byte[] GenerateQrCode(string content, int width = 350, int height = 350)
        {
            var writer = new BarcodeWriter<SKBitmap>
            {
                Format = BarcodeFormat.QR_CODE,
                Options = new ZXing.Common.EncodingOptions
                {
                    Height = height,
                    Width = width,
                    Margin = 0
                },
                Renderer = new SKBitmapRenderer()
            };

            using var bitmap = writer.Write(content);
            using var image = SKImage.FromBitmap(bitmap);
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            return data.ToArray();
        }

        public class PaginatedList<T> : List<T>
        {
            public int PageIndex { get; private set; }
            public int TotalPages { get; private set; }

            public PaginatedList(List<T> items, int count, int pageIndex, int pageSize)
            {
                PageIndex = pageIndex;
                TotalPages = (int)Math.Ceiling(count / (double)pageSize);
                AddRange(items);
            }

            public bool HasPreviousPage => PageIndex > 1;
            public bool HasNextPage => PageIndex < TotalPages;

            public static async Task<PaginatedList<T>> CreateAsync(IQueryable<T> source, int pageIndex, int pageSize)
            {
                var count = await source.CountAsync();
                var items = await source.Skip((pageIndex - 1) * pageSize).Take(pageSize).ToListAsync();
                return new PaginatedList<T>(items, count, pageIndex, pageSize);
            }
        }
    }
}
