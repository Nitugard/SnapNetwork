
using System;
using System.Collections.Generic;

namespace Ibc.Survival
{

    public interface ISortable 
    {
        public int Priority { get; }
    }
    
    public static class ListExtensions
    {
        private static int GetMax<T>(T[] input, int n) where T : ISortable
        {
            int max = input[0].Priority;
            for (int i = 1; i < n; i++)
                if (input[i].Priority > max)
                    max = input[i].Priority;
            return max;
        }

        private static int FindIndex<T>(T[] input, T target, int n) where T : IEquatable<T>
        {
            for(int i=0; i<input.Length; ++i)
                if (input[i].Equals(target))
                    return i;
            return -1;
        }

        private static void CountSort<T>(T[] input, T[] tempArray, int[] count, int n, int exp) where T : ISortable
        {
            int i;
  
            for (i = 0; i < 10; i++)
                count[i] = 0;

            try
            {
                for (i = 0; i < n; i++)
                    count[(input[i].Priority / exp) % 10]++;
            }
            catch (Exception ex)
            {
                throw ex;
            }

            for (i = 1; i < 10; i++)
                count[i] += count[i - 1];
  
            for (i = n - 1; i >= 0; i--) {
                tempArray[count[(input[i].Priority / exp) % 10] - 1] = input[i];
                count[(input[i].Priority / exp) % 10]--;
            }
            
            for (i = 0; i < n; i++)
                input[i] = tempArray[i];
        }
        
        public static void RadixSort<T>(T[] input, T[] tempArray, int[] count, int n) where T : ISortable
        {
            if (n <= 1)
                return;
            
            int m = GetMax(input, n);
            for (int exp = 1; m / exp > 0; exp *= 10)
                CountSort(input, tempArray, count, n, exp);
        }
        
        
        public static void AddSorted<T>(List<T> samples, T item) where T : IComparable<T>
        {
            if (samples.Count == 0)
            {
                samples.Add(item);
                return;
            }

            if (samples[^1].CompareTo(item) <= 0)
            {
                samples.Add(item);
                return;
            }

            if (samples[0].CompareTo(item) >= 0)
            {
                samples.Insert(0, item);
                return;
            }

            int index = samples.BinarySearch(item);
            if (index < 0) index = ~index;
            samples.Insert(index, item);
        }

        public static double GetMedian(List<long> sortedSamples)
        {
            if (sortedSamples.Count == 0)
                return 0;
            int size = sortedSamples.Count;
            int mid = size / 2;
            double median = (size % 2 != 0) ? sortedSamples[mid] : (sortedSamples[mid] + sortedSamples[mid - 1]) / 2.0;
            return median;
        }


        public static double GetStd(List<long> samples)
        {
            double avg = GetAverage(samples);
            double sumOfSquares = SumOfSquares(samples);
            double diff = sumOfSquares - samples.Count * avg * avg;
            return Math.Sqrt(diff / (samples.Count - 1));
        }

        public static double SumOfSquares(List<long> samples)
        {
            double sum = 0;
            foreach (var sample in samples)
                sum += sample * sample;

            return sum;
        }

        public static double GetAverage(List<long> samples)
        {
            if (samples.Count == 0)
                return 0;
            double sum = 0;
            foreach (long sample in samples)
                sum += sample;
            double avg = sum / samples.Count;
            return avg;
        }

        public static double GetAverageWithRemovedOutliers(List<long> samples, double median, double stdDev)
        {
            int count = 0;
            double sum = 0;
            foreach (long sample in samples)
            {
                //only samples that fall within one standard deviation are used to calculate the mean
                if (IsOutlier(median, stdDev, sample)) continue;
                sum += sample;
                count++;
            }

            if (count == 0)
                return (long)median;
            return (long)(sum / count);
        }


        public static bool IsOutlier(double median, double stdDev, long sample)
        {
            return !(sample <= median + stdDev && sample >= median - stdDev);
        }
    }
}