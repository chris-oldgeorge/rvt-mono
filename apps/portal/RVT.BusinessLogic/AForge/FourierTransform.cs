// File summary: Provides vendored numerical helpers used by signal-processing calculations.
// Major updates:
// - 2026-06-26 pending Removed unused private two-dimensional transform helpers for Sonar complexity cleanup.
// - 2026-06-09 pending Renamed data-access namespaces and repository types to RVT.DataAccess/Repository.
// - 2026-05-26 5f9e8ed Initial pre-release alpha SPA import.
// - 2026-06-04 pending Resolved SonarCloud blocker by making unused 2D transform helpers private.

// AForge Math Library
// AForge.NET framework
// http://www.aforgenet.com/framework/
//
// Copyright © Andrew Kirillov, 2005-2009
// <redacted contact>
//
// FFT idea from Exocortex.DSP library
// http://www.exocortex.org/dsp/
//

namespace AForge.Math
{
    using System;

    /// <summary>
    /// Fourier transformation.
    /// </summary>
    /// 
    /// <remarks>The class implements one dimensional and two dimensional
    /// Discrete and Fast Fourier Transformation.</remarks>
    /// 
    public static class FourierTransform
    {
        /// <summary>
        /// Fourier transformation direction.
        /// </summary>
        public enum Direction
        {
            /// <summary>
            /// Forward direction of Fourier transformation.
            /// </summary>
            Forward = 1,

            /// <summary>
            /// Backward direction of Fourier transformation.
            /// </summary>
            Backward = -1
        };

        /// <summary>
        /// One dimensional Discrete Fourier Transform.
        /// </summary>
        /// 
        /// <param name="data">Data to transform.</param>
        /// <param name="direction">Transformation direction.</param>
        /// 
        public static void DFT(Complex[] data, Direction direction)
        {
            int n = data.Length;
            double arg, cos, sin;
            Complex[] dst = new Complex[n];

            // for each destination element
            for (int i = 0; i < n; i++)
            {
                dst[i] = Complex.Zero;

                arg = -(int)direction * 2.0 * System.Math.PI * (double)i / (double)n;

                // sum source elements
                for (int j = 0; j < n; j++)
                {
                    cos = System.Math.Cos(j * arg);
                    sin = System.Math.Sin(j * arg);

                    dst[i].Re += (data[j].Re * cos - data[j].Im * sin);
                    dst[i].Im += (data[j].Re * sin + data[j].Im * cos);
                }
            }

            // copy elements
            if (direction == Direction.Forward)
            {
                // devide also for forward transform
                for (int i = 0; i < n; i++)
                {
                    data[i].Re = dst[i].Re / n;
                    data[i].Im = dst[i].Im / n;
                }
            }
            else
            {
                for (int i = 0; i < n; i++)
                {
                    data[i].Re = dst[i].Re;
                    data[i].Im = dst[i].Im;
                }
            }
        }


        /// <summary>
        /// One dimensional Fast Fourier Transform.
        /// </summary>
        /// 
        /// <param name="data">Data to transform.</param>
        /// <param name="direction">Transformation direction.</param>
        /// 
        /// <remarks><para><note>The method accepts <paramref name="data"/> array of 2<sup>n</sup> size
        /// only, where <b>n</b> may vary in the [1, 20] range.</note></para></remarks>
        /// 
        /// <exception cref="ArgumentException">Incorrect data length.</exception>
        /// 
        public static void FFT(Complex[] data, Direction direction)
        {
            int n = data.Length;
            int m = Tools.Log2(n);

            // reorder data first
            ReorderData(data);

            // compute FFT
            int tn = 1, tm;

            for (int k = 1; k <= m; k++)
            {
                Complex[] rotation = FourierTransform.GetComplexRotation(k, direction);

                tm = tn;
                tn <<= 1;

                for (int i = 0; i < tm; i++)
                {
                    Complex t = rotation[i];

                    for (int even = i; even < n; even += tn)
                    {
                        int odd = even + tm;
                        Complex ce = data[even];
                        Complex co = data[odd];

                        double tr = co.Re * t.Re - co.Im * t.Im;
                        double ti = co.Re * t.Im + co.Im * t.Re;

                        data[even].Re += tr;
                        data[even].Im += ti;

                        data[odd].Re = ce.Re - tr;
                        data[odd].Im = ce.Im - ti;
                    }
                }
            }

            if (direction == Direction.Forward)
            {
                for (int i = 0; i < n; i++)
                {
                    data[i].Re /= (double)n;
                    data[i].Im /= (double)n;
                }
            }
        }


        #region Private Region

        private const int minLength = 2;
        private const int maxLength = 1048576;
        private const int minBits = 1;
        private const int maxBits = 20;
        private static int[][] reversedBits = new int[maxBits][];
        private static Complex[,][] complexRotation = new Complex[maxBits, 2][];

        // Get array, indicating which data members should be swapped before FFT
        // Function summary: Retrieves reversed bits data for callers.
        private static int[] GetReversedBits(int numberOfBits)
        {
            if ((numberOfBits < minBits) || (numberOfBits > maxBits))
                throw new ArgumentOutOfRangeException(nameof(numberOfBits), numberOfBits, $"Value must be between {minBits} and {maxBits}.");

            // check if the array is already calculated
            if (reversedBits[numberOfBits - 1] == null)
            {
                int n = Tools.Pow2(numberOfBits);
                int[] rBits = new int[n];

                // calculate the array
                for (int i = 0; i < n; i++)
                {
                    int oldBits = i;
                    int newBits = 0;

                    for (int j = 0; j < numberOfBits; j++)
                    {
                        newBits = (newBits << 1) | (oldBits & 1);
                        oldBits = (oldBits >> 1);
                    }
                    rBits[i] = newBits;
                }
                reversedBits[numberOfBits - 1] = rBits;
            }
            return reversedBits[numberOfBits - 1];
        }

        // Get rotation of complex number
        // Function summary: Retrieves complex rotation data for callers.
        private static Complex[] GetComplexRotation(int numberOfBits, Direction direction)
        {
            int directionIndex = (direction == Direction.Forward) ? 0 : 1;

            // check if the array is already calculated
            if (complexRotation[numberOfBits - 1, directionIndex] == null)
            {
                int n = 1 << (numberOfBits - 1);
                double uR = 1.0;
                double uI = 0.0;
                double angle = System.Math.PI / n * (int)direction;
                double wR = System.Math.Cos(angle);
                double wI = System.Math.Sin(angle);
                double t;
                Complex[] rotation = new Complex[n];

                for (int i = 0; i < n; i++)
                {
                    rotation[i] = new Complex(uR, uI);
                    t = uR * wI + uI * wR;
                    uR = uR * wR - uI * wI;
                    uI = t;
                }

                complexRotation[numberOfBits - 1, directionIndex] = rotation;
            }
            return complexRotation[numberOfBits - 1, directionIndex];
        }

        // Reorder data for FFT using
        // Function summary: Handles the reorder data workflow for this module.
        private static void ReorderData(Complex[] data)
        {
            int len = data.Length;

            // check data length
            if ((len < minLength) || (len > maxLength) || (!Tools.IsPowerOf2(len)))
                throw new ArgumentException("Incorrect data length.");

            int[] rBits = GetReversedBits(Tools.Log2(len));

            for (int i = 0; i < len; i++)
            {
                int s = rBits[i];

                if (s > i)
                {
                    Complex t = data[i];
                    data[i] = data[s];
                    data[s] = t;
                }
            }
        }

        #endregion
    }
}
