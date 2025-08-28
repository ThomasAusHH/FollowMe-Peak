using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FollowMePeak.Models;
using UnityEngine;

namespace FollowMePeak.Utils;

public static class ClimbDataCrusher
{
    private static class NanCodes
    {
        internal const byte AbsoluteReposition = 1;
        internal const byte AsHalfFloats = 2;
    }

    public static void WriteClimbData(MemoryStream dataStream, ClimbData climb)
    {
        if (climb.Points.Count < 2) return;

        var arr = new byte[14];
        var currPoint = climb.Points[0];
        EncodePointAsFloats(arr, currPoint);
        dataStream.Write(arr.AsSpan(0, 12));

        foreach (var point in climb.Points.Skip(1))
        {
            var diffX = QuarterFloat.FromFloat(point.x - currPoint.x);
            var diffY = QuarterFloat.FromFloat(point.y - currPoint.y);
            var diffZ = QuarterFloat.FromFloat(point.z - currPoint.z);

            if (diffX.IsInfinity() || diffY.IsInfinity() || diffZ.IsInfinity())
            {
                // Can we do it with half floats instead?
                var diffXHalf = HalfFloat.FromFloat(point.x - currPoint.x);
                var diffYHalf = HalfFloat.FromFloat(point.y - currPoint.y);
                var diffZHalf = HalfFloat.FromFloat(point.z - currPoint.z);
                if (diffXHalf.IsInfinity() || diffYHalf.IsInfinity() || diffZHalf.IsInfinity())
                {
                    // Retry encoding as absolute floats
                    EncodeQuarterFloat(arr.AsSpan(0, 1), QuarterFloat.EncodeNaN(NanCodes.AbsoluteReposition));
                    EncodePointAsFloats(arr.AsSpan(1, 12), point);
                    currPoint = point;
                    dataStream.Write(arr.AsSpan(0, 13));
                    continue;
                }

                // Retry encoding as relative half floats
                EncodeQuarterFloat(arr.AsSpan(0, 1), QuarterFloat.EncodeNaN(NanCodes.AsHalfFloats));
                EncodeHalfFloat(arr.AsSpan(1, 2), diffXHalf);
                EncodeHalfFloat(arr.AsSpan(3, 2), diffYHalf);
                EncodeHalfFloat(arr.AsSpan(5, 2), diffZHalf);
                currPoint.x += diffXHalf.ToFloat();
                currPoint.y += diffYHalf.ToFloat();
                currPoint.z += diffZHalf.ToFloat();
                dataStream.Write(arr.AsSpan(0, 7));
                continue;
            }

            if (diffX.IsZero() && diffY.IsZero() && diffZ.IsZero())
            {
                // No movement, skip
                continue;
            }

            EncodeQuarterFloat(arr.AsSpan(0, 1), diffX);
            EncodeQuarterFloat(arr.AsSpan(1, 1), diffY);
            EncodeQuarterFloat(arr.AsSpan(2, 1), diffZ);

            currPoint.x += diffX.ToFloat();
            currPoint.y += diffY.ToFloat();
            currPoint.z += diffZ.ToFloat();
            dataStream.Write(arr.AsSpan(0, 3));
        }
    }

    public static void ReadClimbData(byte[] data, ClimbData climb)
    {
        climb.Points = new List<Vector3>();
        
        if (data == null || data.Length < 12)
        {
            // Not enough data for even one point
            return;
        }
        
        var currPoint = DecodePointFromFloats(data.AsSpan(0, 12));
        climb.Points.Add(currPoint);

        int i = 12;
        while (i < data.Length)
        {
            var first = DecodeQuarterFloat(data.AsSpan(i + 0, 1));

            if (first.IsNaN())
            {
                switch (first.DecodeNaN())
                {
                    case NanCodes.AbsoluteReposition:
                        currPoint = DecodePointFromFloats(data.AsSpan(i + 1, 12));
                        climb.Points.Add(currPoint);
                        i += 13;
                        continue;
                    case NanCodes.AsHalfFloats:
                        currPoint.x += DecodeHalfFloat(data.AsSpan(i + 1, 2)).ToFloat();
                        currPoint.y += DecodeHalfFloat(data.AsSpan(i + 3, 2)).ToFloat();
                        currPoint.z += DecodeHalfFloat(data.AsSpan(i + 5, 2)).ToFloat();
                        climb.Points.Add(currPoint);
                        i += 7;
                        continue;
                    default:
                        throw new InvalidOperationException($"Unexpected NaN code in climb data: {first.DecodeNaN()}");
                }
            }

            currPoint.x += first.ToFloat();
            currPoint.y += DecodeQuarterFloat(data.AsSpan(i + 1, 1)).ToFloat();
            currPoint.z += DecodeQuarterFloat(data.AsSpan(i + 2, 1)).ToFloat();
            climb.Points.Add(currPoint);
            i += 3;
        }
    }

    private static void EncodePointAsFloats(Span<byte> span, Vector3 firstPoint)
    {
        EncodeFloat(span.Slice(0, 4), firstPoint.x);
        EncodeFloat(span.Slice(4, 4), firstPoint.y);
        EncodeFloat(span.Slice(8, 4), firstPoint.z);
    }

    private static Vector3 DecodePointFromFloats(Span<byte> data)
    {
        return new Vector3(
            DecodeFloat(data.Slice(0, 4)),
            DecodeFloat(data.Slice(4, 4)),
            DecodeFloat(data.Slice(8, 4)));
    }

    private static void EncodeFloat(Span<byte> span, float value)
    {
        BinaryPrimitives.WriteInt32LittleEndian(span, BitConverter.SingleToInt32Bits(value));
    }

    private static float DecodeFloat(Span<byte> span)
    {
        return BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(span));
    }

    private static void EncodeHalfFloat(Span<byte> span, HalfFloat half)
    {
        BinaryPrimitives.WriteUInt16LittleEndian(span, half.RawValue);
    }

    private static HalfFloat DecodeHalfFloat(Span<byte> span)
    {
        return new HalfFloat { RawValue = BinaryPrimitives.ReadUInt16LittleEndian(span) };
    }

    private static void EncodeQuarterFloat(Span<byte> span, QuarterFloat quarter)
    {
        span[0] = quarter.RawValue;
    }

    private static QuarterFloat DecodeQuarterFloat(Span<byte> span)
    {
        return new QuarterFloat { RawValue = span[0] };
    }
}