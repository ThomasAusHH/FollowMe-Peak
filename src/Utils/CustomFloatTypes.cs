using System;
using System.Globalization;

namespace FollowMePeak.Utils;

/// <summary>
/// Half-precision float.
/// Note that unlike IEEE 754 half-precision, this uses 4 exponent bits and 11 mantissa bits,
/// (i.e. 1 more mantissa bit and 1 less exponent bit).
/// </summary>
public struct HalfFloat
{
    public const byte TotalNumBits = 16;
    public const byte NumExponentBits = 4;
    public const byte NumMantissaBits = TotalNumBits - NumExponentBits - 1;
    public const byte SignBitPos = TotalNumBits - 1;

    private const ushort SignBit = 1 << (NumExponentBits + NumMantissaBits);
    private const ushort ExponentMask = ((1 << NumExponentBits) - 1) << NumMantissaBits;
    private const ushort MantissaMask = (1 << NumMantissaBits) - 1;
    private const ushort ExponentInfinity = (1 << NumExponentBits) - 1;
    private const int ExponentBias = (1 << (NumExponentBits - 1)) - 1;

    private const int SingleExponentBias = 0x7F;
    private const byte SingleNumExponentBits = 8;
    private const byte SingleNumMantissaBits = 23;
    private const byte SingleSignBitPos = SingleNumExponentBits + SingleNumMantissaBits;

    private const uint SingleExponentMask = ((1 << SingleNumExponentBits) - 1) << SingleNumMantissaBits;
    private const uint SingleMantissaMask = (1 << SingleNumMantissaBits) - 1;
    private const uint SingleExponentInfinity = (1 << SingleNumExponentBits) - 1;

    public static HalfFloat NaN => new() { RawValue = ExponentMask | 1 };

    public ushort ExponentBits => (ushort)((RawValue & ExponentMask) >>> NumMantissaBits);
    public ushort MantissaBits => (ushort)(RawValue & MantissaMask);

    public ushort RawValue;

    public static HalfFloat FromFloat(float value)
    {
        uint origBits = (uint)BitConverter.SingleToInt32Bits(value);
        uint signBit = origBits >>> SingleSignBitPos;
        uint exponentBits = (origBits & SingleExponentMask) >>> SingleNumMantissaBits;
        uint mantissaBits = origBits & SingleMantissaMask;

        int exponent = (int)exponentBits - SingleExponentBias;
        int myExponent = exponent + ExponentBias;

        // Round the mantissa
        uint roundingBit = 1 << (SingleNumMantissaBits - NumMantissaBits - 1);
        if ((mantissaBits & roundingBit) != 0)
        {
            mantissaBits += roundingBit;
            if ((mantissaBits & ~SingleMantissaMask) != 0)
            {
                // Rounding overflowed mantissa, increment exponent
                mantissaBits = 0;
                myExponent += 1;
            }
        }

        if (myExponent >= ExponentInfinity)
        {
            if (mantissaBits != 0 && exponentBits == SingleExponentInfinity)
            {
                // Convert to NaN and discard payload
                return NaN;
            }

            // Overflow to infinity
            myExponent = ExponentInfinity;
            mantissaBits = 0;
        }
        else if (myExponent <= 0)
        {
            if (myExponent > -NumMantissaBits)
            {
                // Denormalized number
                mantissaBits |= 1 << SingleNumMantissaBits;
                int shift = 1 - myExponent;
                mantissaBits >>>= shift;
                myExponent = 0;
            }
            else
            {
                // Underflow to zero
                myExponent = 0;
                mantissaBits = 0;
            }
        }

        ushort mySignBit = (ushort)(signBit << (NumExponentBits + NumMantissaBits));
        ushort myExponentBits = (ushort)(myExponent << NumMantissaBits);
        ushort myMantissaBits = (ushort)(mantissaBits >>> (SingleNumMantissaBits - NumMantissaBits));

        return new HalfFloat { RawValue = (ushort)(mySignBit | myExponentBits | myMantissaBits) };
    }

    public float ToFloat()
    {
        uint signBit = (uint)(RawValue & SignBit) << (SingleSignBitPos - SignBitPos);
        uint exponentBits = ExponentBits;
        uint mantissaBits = MantissaBits;

        if (exponentBits == 0 && mantissaBits == 0)
        {
            // Zero
            return BitConverter.Int32BitsToSingle((int)signBit);
        }

        uint exponent = exponentBits + SingleExponentBias - ExponentBias;
        if (exponentBits == ExponentInfinity)
        {
            exponent = SingleExponentInfinity;
        }
        else if (exponentBits == 0)
        {
            // Denormalized number
            while ((mantissaBits & (1 << NumMantissaBits)) == 0)
            {
                mantissaBits <<= 1;
                exponent--;
            }

            mantissaBits &= MantissaMask;
            exponent++;
        }

        uint origBits = signBit | (exponent << SingleNumMantissaBits) |
                        (mantissaBits << (SingleNumMantissaBits - NumMantissaBits));
        return BitConverter.Int32BitsToSingle((int)origBits);
    }

    public static HalfFloat EncodeNaN(ushort value)
    {
        value += 1;
        ushort signPart = (ushort)(value >>> NumMantissaBits);
        if (signPart > 1)
            throw new ArgumentOutOfRangeException(nameof(value), "Value too large to encode as NaN");
        ushort mantissaPart = (ushort)(value & MantissaMask);
        return new HalfFloat { RawValue = (ushort)((signPart << SignBitPos) | ExponentMask | mantissaPart) };
    }

    public ushort DecodeNaN()
    {
        if (!IsNaN())
            throw new InvalidOperationException("Not a NaN value");

        ushort signPart = (ushort)((RawValue & SignBit) >>> SignBitPos);
        ushort mantissaPart = (ushort)(RawValue & MantissaMask);
        return (ushort)(((signPart << NumMantissaBits) | mantissaPart) - 1);
    }

    public bool IsNaN()
    {
        return ExponentBits == ExponentInfinity && MantissaBits != 0;
    }

    public bool IsInfinity()
    {
        return ExponentBits == ExponentInfinity && MantissaBits == 0;
    }

    public bool IsPositiveInfinity()
    {
        return IsInfinity() && (RawValue & SignBit) == 0;
    }

    public bool IsNegativeInfinity()
    {
        return IsInfinity() && (RawValue & SignBit) != 0;
    }

    public bool IsZero()
    {
        return (RawValue & ~SignBit) == 0;
    }

    public bool IsPositiveZero()
    {
        return RawValue == 0;
    }

    public bool IsNegativeZero()
    {
        return RawValue == SignBit;
    }

    public bool IsDenormalized()
    {
        return ExponentBits == 0 && MantissaBits != 0;
    }

    public override string ToString()
    {
        return ToFloat().ToString(CultureInfo.InvariantCulture);
    }
}

/// <summary>
/// Quarter-precision float. Tiny!
/// </summary>
public struct QuarterFloat
{
    public const byte TotalNumBits = 8;
    public const byte NumExponentBits = 2;
    public const byte NumMantissaBits = TotalNumBits - NumExponentBits - 1;
    public const byte SignBitPos = TotalNumBits - 1;

    private const byte SignBit = 1 << (NumExponentBits + NumMantissaBits);
    private const byte ExponentMask = ((1 << NumExponentBits) - 1) << NumMantissaBits;
    private const byte MantissaMask = (1 << NumMantissaBits) - 1;
    private const byte ExponentInfinity = (1 << NumExponentBits) - 1;
    private const int ExponentBias = (1 << (NumExponentBits - 1)) - 1;

    private const int SingleExponentBias = 0x7F;
    private const byte SingleNumExponentBits = 8;
    private const byte SingleNumMantissaBits = 23;
    private const byte SingleSignBitPos = SingleNumExponentBits + SingleNumMantissaBits;

    private const uint SingleExponentMask = ((1 << SingleNumExponentBits) - 1) << SingleNumMantissaBits;
    private const uint SingleMantissaMask = (1 << SingleNumMantissaBits) - 1;
    private const uint SingleExponentInfinity = (1 << SingleNumExponentBits) - 1;

    public static QuarterFloat NaN => new() { RawValue = ExponentMask | 1 };

    public byte ExponentBits => (byte)((RawValue & ExponentMask) >>> NumMantissaBits);
    public byte MantissaBits => (byte)(RawValue & MantissaMask);

    public byte RawValue;

    public static QuarterFloat FromFloat(float value)
    {
        uint origBits = (uint)BitConverter.SingleToInt32Bits(value);
        uint signBit = origBits >>> SingleSignBitPos;
        uint exponentBits = (origBits & SingleExponentMask) >>> SingleNumMantissaBits;
        uint mantissaBits = origBits & SingleMantissaMask;

        int exponent = (int)exponentBits - SingleExponentBias;
        int myExponent = exponent + ExponentBias;

        // Round the mantissa
        uint roundingBit = 1 << (SingleNumMantissaBits - NumMantissaBits - 1);
        if ((mantissaBits & roundingBit) != 0)
        {
            mantissaBits += roundingBit;
            if ((mantissaBits & ~SingleMantissaMask) != 0)
            {
                // Rounding overflowed mantissa, increment exponent
                mantissaBits = 0;
                myExponent += 1;
            }
        }

        if (myExponent >= ExponentInfinity)
        {
            if (mantissaBits != 0 && exponentBits == SingleExponentInfinity)
            {
                // Convert to NaN and discard payload
                return NaN;
            }

            // Overflow to infinity
            myExponent = ExponentInfinity;
            mantissaBits = 0;
        }
        else if (myExponent <= 0)
        {
            if (myExponent > -NumMantissaBits)
            {
                // Denormalized number
                mantissaBits |= 1 << SingleNumMantissaBits;
                int shift = 1 - myExponent;
                mantissaBits >>>= shift;
                myExponent = 0;
            }
            else
            {
                // Underflow to zero
                myExponent = 0;
                mantissaBits = 0;
            }
        }

        byte mySignBit = (byte)(signBit << (NumExponentBits + NumMantissaBits));
        byte myExponentBits = (byte)(myExponent << NumMantissaBits);
        byte myMantissaBits = (byte)(mantissaBits >>> (SingleNumMantissaBits - NumMantissaBits));

        return new QuarterFloat { RawValue = (byte)(mySignBit | myExponentBits | myMantissaBits) };
    }

    public float ToFloat()
    {
        uint signBit = (uint)(RawValue & SignBit) << (SingleSignBitPos - SignBitPos);
        uint exponentBits = ExponentBits;
        uint mantissaBits = MantissaBits;

        if (exponentBits == 0 && mantissaBits == 0)
        {
            // Zero
            return BitConverter.Int32BitsToSingle((int)signBit);
        }

        uint exponent = exponentBits + SingleExponentBias - ExponentBias;
        if (exponentBits == ExponentInfinity)
        {
            exponent = SingleExponentInfinity;
        }
        else if (exponentBits == 0)
        {
            // Denormalized number
            while ((mantissaBits & (1 << NumMantissaBits)) == 0)
            {
                mantissaBits <<= 1;
                exponent--;
            }

            mantissaBits &= MantissaMask;
            exponent++;
        }

        uint origBits = signBit | (exponent << SingleNumMantissaBits) |
                        (mantissaBits << (SingleNumMantissaBits - NumMantissaBits));
        return BitConverter.Int32BitsToSingle((int)origBits);
    }

    public static QuarterFloat EncodeNaN(byte value)
    {
        value += 1;
        byte signPart = (byte)(value >>> NumMantissaBits);
        if (signPart > 1)
            throw new ArgumentOutOfRangeException(nameof(value), "Value too large to encode as NaN");
        byte mantissaPart = (byte)(value & MantissaMask);
        return new QuarterFloat { RawValue = (byte)((signPart << SignBitPos) | ExponentMask | mantissaPart) };
    }

    public byte DecodeNaN()
    {
        if (!IsNaN())
            throw new InvalidOperationException("Not a NaN value");

        byte signPart = (byte)((RawValue & SignBit) >>> SignBitPos);
        byte mantissaPart = (byte)(RawValue & MantissaMask);
        return (byte)(((signPart << NumMantissaBits) | mantissaPart) - 1);
    }

    public bool IsNaN()
    {
        return ExponentBits == ExponentInfinity && MantissaBits != 0;
    }

    public bool IsInfinity()
    {
        return ExponentBits == ExponentInfinity && MantissaBits == 0;
    }

    public bool IsPositiveInfinity()
    {
        return IsInfinity() && (RawValue & SignBit) == 0;
    }

    public bool IsNegativeInfinity()
    {
        return IsInfinity() && (RawValue & SignBit) != 0;
    }

    public bool IsZero()
    {
        return (RawValue & ~SignBit) == 0;
    }

    public bool IsPositiveZero()
    {
        return RawValue == 0;
    }

    public bool IsNegativeZero()
    {
        return RawValue == SignBit;
    }

    public bool IsDenormalized()
    {
        return ExponentBits == 0 && MantissaBits != 0;
    }

    public override string ToString()
    {
        return ToFloat().ToString(CultureInfo.InvariantCulture);
    }
}