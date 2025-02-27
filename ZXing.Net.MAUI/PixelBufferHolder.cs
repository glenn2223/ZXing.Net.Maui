﻿using System;
using System.Linq;
using System.IO;
using Microsoft.Maui.Graphics;
using System.Collections.Generic;
using System.Runtime.InteropServices;
#if WINDOWS
using System.Diagnostics;
using System.Drawing.Imaging;
using System.Threading;
#endif

namespace ZXing.Net.Maui.Readers;

public record PixelBufferHolder
{
	public Size Size { get; }

	public IEnumerable<byte> Data { get; }

    public PixelBufferHolder(Size size, IEnumerable<byte> data)
    {
        Size = size;
        Data = data;
    }

#if ANDROID

    public PixelBufferHolder(Size size, Java.Nio.ByteBuffer buffer)
    {
        buffer.Position(0);

        var data = new byte[buffer.Remaining()];
        
        buffer.Get(data);

        Size = size;
        Data = data;
    }

#elif IOS || MACCATALYST

    public CoreVideo.CVPixelBuffer? PixelBuffer { get; }

    public PixelBufferHolder(Size size, CoreVideo.CVPixelBuffer pixelBuffer)
    {
        Size = size;
        Data = null!;
        PixelBuffer = pixelBuffer;
    }

#endif

    /// <summary>
    /// Create the necessary <see cref="PixelBufferHolder"/> from a stream
    /// </summary>
    /// <param name="stream">The stream to pick pixel data from</param>
    /// <returns></returns>
    /// <exception cref="NullReferenceException"></exception>
    public static PixelBufferHolder FromStream(Stream stream)
    {
#if WINDOWS

        var image = new Microsoft.Maui.Graphics.Skia.SkiaImageLoadingService().FromStream(stream) as Microsoft.Maui.Graphics.Skia.SkiaImage;

        var dataList = new List<byte> { };

        var mySpan = CollectionsMarshal.AsSpan(image!.PlatformRepresentation.Pixels.ToList());
        for (var i = 0; i < mySpan.Length; i++)
        {
            dataList.Add(mySpan[i].Red);
            dataList.Add(mySpan[i].Green);
            dataList.Add(mySpan[i].Blue);
        }

        var data = dataList.ToArray();

#else

        var image = 
            (Microsoft.Maui.Graphics.Platform.PlatformImage.FromStream(stream) 
                as Microsoft.Maui.Graphics.Platform.PlatformImage)!;

#if IOS || MACCATALYST
        
        var uiImage = image.PlatformRepresentation;

        var pixelBuffer = uiImage.CIImage?.PixelBuffer;

        if (pixelBuffer != null)
            return new PixelBufferHolder(new(image.Width, image.Height), pixelBuffer);
        
        var data = uiImage.CGImage?.DataProvider.CopyData()?.ToArray();

        if (data == null)
            throw new NullReferenceException("Could not convert stream to native bytes");

#elif ANDROID

        var pixelArr = new int[(int)(image.Width * image.Height)];

        image!.PlatformRepresentation.GetPixels(pixelArr, 0, (int)image.Width, 0, 0, (int)image.Width, (int)image.Height);
        image!.PlatformRepresentation.Recycle();

        var dataList = new List<byte> { };

        var mySpan = CollectionsMarshal.AsSpan(pixelArr.ToList());
        
        for (var i = 0; i < mySpan.Length; i++)
        {
            var intValue = mySpan[i];

            dataList.Add(
                (byte)((
                    (intValue >> 24) +
                    (intValue >> 16) +
                    (intValue >> 8) +
                    (byte)intValue
                    ) / 4)
            );
        }

        var data = dataList.ToArray();

#else

        throw new PlatformNotSupportedException();

#endif

#endif

        return new PixelBufferHolder(new(image.Width, image.Height), data);
    }
}