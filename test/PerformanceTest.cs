using Microsoft.VisualStudio.TestTools.UnitTesting;
using Mozo.Fwob.Extensions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace Mozo.Fwob.UnitTest;

// Baseline test result in seconds (passes) on an i7 ThinkPad P1 Gen2 with Release build
//
// Operation        Small file   Large file   Speed     Comments
// GetAllFrames     2.434 (25)   16.770 (4)   Moderate  Binary reader with 64KB buffer, with deserialization
// CopyFile         0.578 (104)  3.824 (16)   Fast      File system copy, no deserialization/serialization
// CopyWithConcat   0.363 (166)  3.628 (17)   Fast      Block copy with 4MB buffer, no deserialization/serialization
// CopyWithSplit    0.394 (151)  3.489 (18)   Fast      Block copy with 4MB buffer, no deserialization/serialization
// CopyFramesBatch  5.285 (12)   36.976 (3)   Slow      Binary reader+writer with 64KB buffer, with deserialization+serialization
// CopyAllFrames    3.5min       >25min       Slowest   Binary reader+writer flushing every frame write, with deserialization+serialization
//
// Small file: 407,313,200 bytes, 33,942,596 frames
// Large file: 2,780,026,652 bytes, 231,668,717 frames

[TestClass]
public class PerformanceTest
{
    public class ShortTick
    {
        public uint Time;
        public uint Price;
        public int Size;
    }

    private readonly string largeFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "NIO_20230428.fwob");
    private readonly string smallFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "SPOT_20230428.fwob");

    private static (TimeSpan, int) RunPerformanceTest(Action<string> tester, string filepath, string title)
    {
        List<TimeSpan> times = new();
        TimeSpan timeTotal = TimeSpan.Zero;

        int pass = 1;
        for (; ; pass++)
        {
            var swpass = Stopwatch.StartNew();
            tester(filepath);
            swpass.Stop();

            times.Add(swpass.Elapsed);
            timeTotal += swpass.Elapsed;

            if (timeTotal > TimeSpan.FromMinutes(1) && pass >= 3)
                break;
        }

        TimeSpan timeAvg = times.Order().Skip(1).SkipLast(1).Aggregate((a, b) => a + b) / (pass - 2);

        return (timeAvg, pass);
    }

    [TestMethod]
    public void TestAllPerformance()
    {
        Trace.WriteLine($"{"Operation",-18}{"Small file",-13}{"Large file",-13}");

        foreach ((string profile, Action<string> tester) in new (string, Action<string>)[] {
            ("GetAllFrames", TestGetAllFrames),
            ("CopyFile", TestCopyFile),
            ("CopyWithConcat", TestCopyWithConcat),
            ("CopyWithSplit", TestCopyWithSplit),
            ("CopyFramesBatch", TestCopyFramesBatch),
            //("CopyFrameByFrame", TestCopyFrameByFrame),
        })
        {
            (TimeSpan timeSmall, int passSmall) = RunPerformanceTest(tester, smallFilePath, $"[Small] {profile}");
            (TimeSpan timeLarge, int passLarge) = RunPerformanceTest(tester, largeFilePath, $"[Large] {profile}");
            Trace.WriteLine($"{profile,-18}{$"{timeSmall.TotalSeconds:f3} ({passSmall})",-13}{$"{timeLarge.TotalSeconds:f3} ({passLarge})",-13}");
        }
    }

    private static void TestGetAllFrames(string filepath)
    {
        using FwobFile<ShortTick, uint> fwobfile = new(filepath, FileAccess.Read, FileShare.None);
        foreach (ShortTick _ in fwobfile) { }
    }

    private static void TestCopyFile(string filepath)
    {
        string temppath = Path.GetTempFileName();
        File.Copy(filepath, temppath, true);
        File.Delete(temppath);
    }

    private static void TestCopyWithConcat(string filepath)
    {
        string temppath = Path.GetTempFileName();
        FwobFile<ShortTick, uint>.Concat(temppath, filepath);
        File.Delete(temppath);
    }

    private static void TestCopyWithSplit(string filepath)
    {
        string temppath = Path.GetTempFileName();
        File.Delete(temppath);
        Directory.CreateDirectory(temppath);
        FwobFile<ShortTick, uint>.Split(filepath, temppath, 0);
        Directory.Delete(temppath, true);
    }

    private static void TestCopyFramesBatch(string filepath)
    {
        using FwobFile<ShortTick, uint> fwobfile = new(filepath, FileAccess.Read, FileShare.None);

        string temppath = Path.GetTempFileName();
        using (FwobFile<ShortTick, uint> newfile = new(temppath, fwobfile.Title))
        {
            newfile.AppendFrames(fwobfile);
        }

        File.Delete(temppath);
    }

    private static void TestCopyFrameByFrame(string filepath)
    {
        using FwobFile<ShortTick, uint> fwobfile = new(filepath, FileAccess.Read, FileShare.None);

        string temppath = Path.GetTempFileName();
        using (FwobFile<ShortTick, uint> newfile = new(temppath, fwobfile.Title))
        {
            foreach (ShortTick frame in fwobfile)
                newfile.AppendFrames(frame);
        }

        File.Delete(temppath);
    }

    [TestMethod]
    public void TestCompressSmall()
    {
        TestCompress(smallFilePath, "Small");
    }

    [TestMethod]
    public void TestCompressLarge()
    {
        TestCompress(largeFilePath, "Large");
    }

    // Design Decisions
    // [Algorithms]
    //   Options: Gzip, Deflate
    //   Experiments: Deflate is faster and results in smaller file, Gzip is a wrapper of Deflate.
    //   Result: Deflate
    // [Compression Level]
    //   Options: Smallest, Optimal, Fastest
    //   Experiments:
    //     a) Smallest speeds down as frames/block grows while Fastest speed up as frames/block grows.
    //     b) Optimal significantly slows down when frames/block is smaller than 128, but does not change the speed much otherwise.
    //     c) Smallest is the slowest while Fastest generates the largest files.
    //     d) Smallest generates merely 1% smaller files than Optimal while takes at least 20% more time.
    //     e) Fastest generates 4% larger files than Optimal but takes around 20% more time.
    //   Result: Optimal
    // [Block Length]
    //   Options: Fixed, Variable (fixed frames/block)
    //   Experiments:
    //     a) Fixed frames/block generates various block length, which follows normal distribution.
    //     b) The largest block length can be 20 times or more than the smallest block length.
    //     c) Variable block length is difficult in reading and seeking.
    //   Result: Fixed
    // [Frames/block]
    //   Options: 32-1048576 with x2 step up, 256-1024 with +256 step up
    //   Experiments: As frames/block grows, the generated file size decreases decelerating.
    //   Result: 256-1024

    // [Small,Deflate,Fast] - 256 frames/block: 00:00:22.2350289 - 135,809,027 (33.343%)
    // [Small,Deflate,Fast] - 512 frames/block: 00:00:19.9369354 - 129,950,769 (31.904%)
    // [Small,Deflate,Fast] - 768 frames/block: 00:00:19.1264280 - 127,682,218 (31.347%)
    // [Small,Deflate,Fast] - 1024 frames/block: 00:00:18.9951872 - 126,454,630 (31.046%)
    // [Small,Deflate,Opti] - 256 frames/block: 00:00:26.3643439 - 119,675,736 (29.382%)
    // [Small,Deflate,Opti] - 512 frames/block: 00:00:25.2028677 - 114,682,699 (28.156%)
    // [Small,Deflate,Opti] - 768 frames/block: 00:00:24.8940924 - 113,223,083 (27.798%)
    // [Small,Deflate,Opti] - 1024 frames/block: 00:00:25.0171872 - 112,343,679 (27.582%)
    // [Small,Deflate,Smal] - 256 frames/block: 00:00:32.4706980 - 115,465,577 (28.348%)
    // [Small,Deflate,Smal] - 512 frames/block: 00:00:36.7746432 - 110,198,379 (27.055%)
    // [Small,Deflate,Smal] - 768 frames/block: 00:00:42.0911821 - 108,465,398 (26.629%)
    // [Small,Deflate,Smal] - 1024 frames/block: 00:00:46.5270443 - 107,385,172 (26.364%)

    // [Small,Fast,512] - 00:00:42.2540306 - 146,837,206 (36.050%) - 3.847 zips/blk (2-37) - 509.97 bytes/blk (492-512,0.397%) - 118.35 frames/blk (62-1226)
    // [Small,Fast,1024] - 00:00:35.9141801 - 136,284,374 (33.459%) - 4.049 zips/blk (2-46) - 1021.97 bytes/blk (996-1024,0.198%) - 255.03 frames/blk (137-2319)
    // [Small,Fast,1536] - 00:00:29.8287926 - 131,710,678 (32.336%) - 4.136 zips/blk (2-39) - 1534.07 bytes/blk (1517-1536,0.126%) - 395.84 frames/blk (214-3433)
    // [Small,Fast,2048] - 00:00:28.7538390 - 129,896,662 (31.891%) - 4.269 zips/blk (2-47) - 2045.89 bytes/blk (2021-2048,0.103%) - 535.14 frames/blk (307-4608)
    // [Small,Fast,2560] - 00:00:28.1903709 - 128,501,974 (31.549%) - 4.295 zips/blk (2-54) - 2558.04 bytes/blk (2537-2560,0.076%) - 676.20 frames/blk (367-5453)
    // [Small,Fast,3072] - 00:00:27.7276622 - 127,445,206 (31.289%) - 4.348 zips/blk (2-23) - 3070.04 bytes/blk (3053-3072,0.064%) - 818.16 frames/blk (483-5497)
    // [Small,Fast,3584] - 00:00:26.4544603 - 126,734,038 (31.115%) - 4.402 zips/blk (2-46) - 3581.97 bytes/blk (3563-3584,0.057%) - 959.86 frames/blk (670-6516)
    // [Small,Fast,4096] - 00:00:24.9904908 - 126,275,798 (31.002%) - 4.459 zips/blk (2-33) - 4093.91 bytes/blk (4077-4096,0.051%) - 1100.99 frames/blk (711-7707)
    // [Small,Opti,512] - 00:00:58.0204862 - 127,213,270 (31.232%) - 4.092 zips/blk (2-61) - 509.51 bytes/blk (485-512,0.486%) - 136.61 frames/blk (70-1840)
    // [Small,Opti,1024] - 00:00:54.1803820 - 117,528,790 (28.855%) - 4.301 zips/blk (2-62) - 1021.56 bytes/blk (998-1024,0.238%) - 295.73 frames/blk (158-3502)
    // [Small,Opti,1536] - 00:00:50.3939781 - 114,086,614 (28.010%) - 4.282 zips/blk (2-50) - 1534.35 bytes/blk (1526-1536,0.108%) - 456.98 frames/blk (245-5348)
    // [Small,Opti,2048] - 00:00:51.1709987 - 112,484,566 (27.616%) - 4.425 zips/blk (2-21) - 2046.01 bytes/blk (2028-2048,0.097%) - 617.99 frames/blk (337-5484)
    // [Small,Opti,2560] - 00:00:50.5536046 - 111,808,214 (27.450%) - 4.442 zips/blk (2-55) - 2558.31 bytes/blk (2547-2560,0.066%) - 777.14 frames/blk (423-6412)
    // [Small,Opti,3072] - 00:00:51.5474337 - 111,065,302 (27.268%) - 4.491 zips/blk (2-36) - 3070.27 bytes/blk (3061-3072,0.056%) - 938.81 frames/blk (617-8738)
    // [Small,Opti,3584] - 00:00:50.1575410 - 110,573,782 (27.147%) - 4.533 zips/blk (2-28) - 3582.26 bytes/blk (3570-3584,0.048%) - 1100.13 frames/blk (649-8876)
    // [Small,Opti,4096] - 00:00:51.0580056 - 110,280,918 (27.075%) - 4.592 zips/blk (2-30) - 4094.12 bytes/blk (4078-4096,0.046%) - 1260.66 frames/blk (816-8270)
    // [Small,Smal,512] - 00:01:07.2774104 - 123,964,630 (30.435%) - 4.138 zips/blk (2-47) - 509.72 bytes/blk (489-512,0.446%) - 140.19 frames/blk (70-2251)
    // [Small,Smal,1024] - 00:01:21.2238332 - 114,448,598 (28.098%) - 4.371 zips/blk (2-33) - 1021.69 bytes/blk (1000-1024,0.226%) - 303.69 frames/blk (159-4501)
    // [Small,Smal,1536] - 00:01:34.2127388 - 110,811,862 (27.206%) - 4.378 zips/blk (2-58) - 1534.36 bytes/blk (1526-1536,0.107%) - 470.49 frames/blk (248-5127)
    // [Small,Smal,2048] - 00:01:50.7829509 - 109,375,702 (26.853%) - 4.549 zips/blk (2-30) - 2045.98 bytes/blk (2026-2048,0.098%) - 635.55 frames/blk (342-7988)
    // [Small,Smal,2560] - 00:02:05.6751077 - 108,385,494 (26.610%) - 4.552 zips/blk (2-80) - 2558.33 bytes/blk (2550-2560,0.065%) - 801.69 frames/blk (474-5241)
    // [Small,Smal,3072] - 00:02:20.8187751 - 107,624,662 (26.423%) - 4.617 zips/blk (2-29) - 3070.29 bytes/blk (3062-3072,0.056%) - 968.84 frames/blk (570-6435)
    // [Small,Smal,3584] - 00:02:36.2953091 - 107,108,054 (26.296%) - 4.651 zips/blk (2-25) - 3582.25 bytes/blk (3570-3584,0.049%) - 1135.76 frames/blk (655-9007)
    // [Small,Smal,4096] - 00:02:52.7179652 - 106,909,910 (26.248%) - 4.730 zips/blk (2-56) - 4094.10 bytes/blk (4080-4096,0.046%) - 1300.41 frames/blk (905-8449)
    // [Small,RAR5,32MB] - 00:00:18.5000000 - 78,322,051 (19.229%)
    // [Large,Fast,512] - 00:03:35.2182951 - 796,549,334 (28.653%) - 4.047 zips/blk (2-62) - 510.18 bytes/blk (491-512,0.356%) - 148.91 frames/blk (82-1590)
    // [Large,Fast,1024] - 00:03:01.0071754 - 742,106,326 (26.694%) - 4.240 zips/blk (2-60) - 1022.12 bytes/blk (993-1024,0.184%) - 319.67 frames/blk (188-2437)
    // [Large,Fast,1536] - 00:02:42.5651378 - 721,643,734 (25.958%) - 4.274 zips/blk (2-53) - 1534.44 bytes/blk (1513-1536,0.101%) - 493.10 frames/blk (296-2744)
    // [Large,Fast,2048] - 00:02:36.0192024 - 711,801,046 (25.604%) - 4.387 zips/blk (2-68) - 2046.24 bytes/blk (2018-2048,0.086%) - 666.56 frames/blk (409-3091)
    // [Large,Fast,2560] - 00:02:29.9508758 - 706,690,774 (25.420%) - 4.425 zips/blk (2-37) - 2558.35 bytes/blk (2532-2560,0.065%) - 839.22 frames/blk (534-3271)
    // [Large,Fast,3072] - 00:02:25.1091646 - 701,912,278 (25.248%) - 4.449 zips/blk (2-45) - 3070.43 bytes/blk (3048-3072,0.051%) - 1013.92 frames/blk (659-3503)
    // [Large,Fast,3584] - 00:02:21.6172621 - 698,485,974 (25.125%) - 4.498 zips/blk (2-39) - 3582.40 bytes/blk (3558-3584,0.045%) - 1188.71 frames/blk (775-3777)
    // [Large,Fast,4096] - 00:02:21.2166011 - 696,131,798 (25.040%) - 4.539 zips/blk (2-60) - 4094.36 bytes/blk (4070-4096,0.040%) - 1363.12 frames/blk (888-3946)
    // [Large,Opti,512] - 00:05:49.2221421 - 678,628,054 (24.411%) - 4.329 zips/blk (2-54) - 509.87 bytes/blk (488-512,0.417%) - 174.79 frames/blk (86-1880)
    // [Large,Opti,1024] - 00:05:47.6142503 - 633,876,694 (22.801%) - 4.509 zips/blk (2-56) - 1022.05 bytes/blk (999-1024,0.191%) - 374.25 frames/blk (202-2211)
    // [Large,Opti,1536] - 00:05:40.3609826 - 619,661,014 (22.290%) - 4.493 zips/blk (2-63) - 1534.69 bytes/blk (1524-1536,0.085%) - 574.25 frames/blk (321-2732)
    // [Large,Opti,2048] - 00:05:46.5811884 - 612,036,822 (22.016%) - 4.628 zips/blk (2-38) - 2046.46 bytes/blk (2025-2048,0.075%) - 775.21 frames/blk (447-3316)
    // [Large,Opti,2560] - 00:05:46.3308433 - 609,817,814 (21.936%) - 4.654 zips/blk (2-56) - 2558.66 bytes/blk (2541-2560,0.052%) - 972.54 frames/blk (581-3676)
    // [Large,Opti,3072] - 00:05:49.7498864 - 606,523,606 (21.817%) - 4.698 zips/blk (2-74) - 3070.66 bytes/blk (3061-3072,0.044%) - 1173.39 frames/blk (718-3730)
    // [Large,Opti,3584] - 00:05:47.2522992 - 604,445,398 (21.742%) - 4.735 zips/blk (2-38) - 3582.68 bytes/blk (3574-3584,0.037%) - 1373.66 frames/blk (847-3993)
    // [Large,Opti,4096] - 00:05:53.2197509 - 603,123,926 (21.695%) - 4.791 zips/blk (2-39) - 4094.63 bytes/blk (4078-4096,0.033%) - 1573.33 frames/blk (980-4134)
    // [Large,Smal,512] - 00:07:36.5995775 - 658,850,518 (23.699%) - 4.374 zips/blk (2-66) - 509.95 bytes/blk (488-512,0.400%) - 180.03 frames/blk (86-2014)
    // [Large,Smal,1024] - 00:10:04.5241974 - 615,232,726 (22.130%) - 4.568 zips/blk (2-63) - 1022.10 bytes/blk (999-1024,0.185%) - 385.59 frames/blk (203-2663)
    // [Large,Smal,1536] - 00:12:10.3288646 - 601,275,094 (21.628%) - 4.574 zips/blk (2-59) - 1534.71 bytes/blk (1525-1536,0.084%) - 591.81 frames/blk (333-3166)
    // [Large,Smal,2048] - 00:14:34.6768308 - 594,188,502 (21.373%) - 4.714 zips/blk (2-33) - 2046.46 bytes/blk (2025-2048,0.075%) - 798.50 frames/blk (458-2874)
    // [Large,Smal,2560] - 00:16:49.4864606 - 591,511,254 (21.277%) - 4.734 zips/blk (2-97) - 2558.71 bytes/blk (2543-2560,0.050%) - 1002.64 frames/blk (586-3537)
    // [Large,Smal,3072] - 00:19:17.1329432 - 588,235,990 (21.159%) - 4.785 zips/blk (2-56) - 3070.69 bytes/blk (3061-3072,0.043%) - 1209.86 frames/blk (709-3767)
    // [Large,Smal,3584] - 00:21:46.0251212 - 586,213,590 (21.087%) - 4.837 zips/blk (2-53) - 3582.70 bytes/blk (3568-3584,0.036%) - 1416.38 frames/blk (854-4155)
    // [Large,Smal,4096] - 00:24:16.7017574 - 585,130,198 (21.048%) - 4.890 zips/blk (2-52) - 4094.61 bytes/blk (4080-4096,0.034%) - 1621.71 frames/blk (985-4450)
    // [Small,RAR5,32MB] - 00:01:33.5000000 - 430,993,687 (15.503%)

    private static void TestCompress(string filename, string filesize)
    {
        long fileorilen = new FileInfo(filename).Length;

        foreach (CompressionLevel level in new[] {
            CompressionLevel.Fastest,
            CompressionLevel.Optimal,
            CompressionLevel.SmallestSize,
        })
        {
            string levelStr = level.ToString()[0..4];

            for (int blockLength = 512; blockLength <= 4096; blockLength += 512)
            {
                string outpath = Path.ChangeExtension(filename, $"{levelStr}.{blockLength}.fwobc");

                var stopwatch = Stopwatch.StartNew();
                TestCompressFixedLengthBlock(filename, outpath, blockLength, level,
                    out List<(int FrameCount, int ActualBlockLength, int Compressions, List<(int Value, int Outcome)> Estimates)>? logData);
                stopwatch.Stop();

                string logpath = Path.ChangeExtension(outpath, ".log");
                IEnumerable<string> logLines = logData
                    .Select(o => $"{o.FrameCount} {o.ActualBlockLength} {o.Compressions} {string.Join(",", o.Estimates.Select(p => $"{p.Value}({p.Outcome})"))}");
                File.WriteAllLines(logpath, logLines.ToArray());

                long totalCompressions = 0, totalActualBlockLen = 0, totalFrameCount = 0;
                int minCompressions = int.MaxValue, maxCompressions = 0;
                int minActualBlockLen = int.MaxValue, maxActualBlockLen = 0;
                int minFrameCount = int.MaxValue, maxFrameCount = 0;

                foreach ((int frameCount, int actualBlockLength, int compressions, _) in logData.SkipLast(1))
                {
                    totalCompressions += compressions;
                    minCompressions = Math.Min(minCompressions, compressions);
                    maxCompressions = Math.Max(maxCompressions, compressions);

                    totalActualBlockLen += actualBlockLength;
                    minActualBlockLen = Math.Min(minActualBlockLen, actualBlockLength);
                    maxActualBlockLen = Math.Max(maxActualBlockLen, actualBlockLength);

                    totalFrameCount += frameCount;
                    minFrameCount = Math.Min(minFrameCount, frameCount);
                    maxFrameCount = Math.Max(maxFrameCount, frameCount);
                }

                long filelen = new FileInfo(outpath).Length;
                double repeatRate = (double)totalCompressions / logData.Count;
                double avgBlockLen = (double)totalActualBlockLen / logData.Count;
                double avgFrameCnt = (double)totalFrameCount / logData.Count;

                Trace.WriteLine($"[{filesize},{levelStr},{blockLength}]" +
                    $" - {stopwatch.Elapsed}" +
                    $" - {filelen:n0} ({(double)filelen / fileorilen:p3})" +
                    $" - {repeatRate:f3} zips/blk ({minCompressions}-{maxCompressions})" +
                    $" - {avgBlockLen:f2} bytes/blk ({minActualBlockLen}-{maxActualBlockLen},{1 - avgBlockLen / blockLength:p3})" +
                    $" - {avgFrameCnt:f2} frames/blk ({minFrameCount}-{maxFrameCount})");
            }
        }
    }

    [TestMethod]
    public void TestMemoryStreamBuffer()
    {
        using MemoryStream sourceStream = new();
        for (int i = 0; i < 400_000_000; i++)
        {
            sourceStream.WriteByte((byte)(Random.Shared.Next() % 128));
        }

        using MemoryStream targetStream = new();

        var sw = Stopwatch.StartNew();
        int seg = 200;
        int n = (int)sourceStream.Length / seg;
        for (int i = 0; i < n; i++)
        {
            using MemoryStream tmpStream = new();
            using DeflateStream deflateStream = new(tmpStream, CompressionLevel.Optimal);
            //*
            byte[] buffer = sourceStream.GetBuffer();
            deflateStream.Write(buffer, i * seg, seg);
            deflateStream.Flush();
            /*/
            memStream.WriteTo(newStream);
            //*/
            tmpStream.WriteTo(targetStream);
            //var arr = tmpStream.ToArray();
            //targetStream.Write(arr);
        }

        Trace.WriteLine(sw.Elapsed.TotalSeconds);
        Trace.WriteLine((double)targetStream.Length / sourceStream.Length * 100);

        //Assert.AreEqual(targetStream.Length, sourceStream.Length);
        //Assert.IsTrue(targetStream.ToArray().SequenceEqual(sourceStream.ToArray()));
    }

    private static void TestCompressFixedLengthBlock(
        string filename,
        string outpath,
        int blockLength,
        CompressionLevel compressionLevel,
        out List<(int FrameCount, int ActualBlockLength, int Compressions, List<(int Value, int Outcome)> Estimates)> logData)
    {
        using FwobFile<ShortTick, uint> fwobfile = new(filename, FileAccess.Read, FileShare.None);

        using FileStream outputStream = new(outpath, FileMode.Create, FileAccess.Write, FileShare.None, 64 * 1024);
        using BinaryWriter outputWriter = new(outputStream);
        outputWriter.WriteHeader(fwobfile.Header);

        using MemoryStream uncompressedFrames = new();
        using BinaryWriter uncompressedWriter = new(uncompressedFrames);
        int uncompressedFrameCount = 0;
        int numCompressions = 0;
        List<(int Value, int Outcome)> estimates = new();

        byte[] GetCompressedBytes(int numFrames)
        {
            numCompressions++;

            using MemoryStream memStream = new();
            using DeflateStream deflateStream = new(memStream, compressionLevel);

            uncompressedWriter.Flush();
            byte[] buffer = uncompressedFrames.GetBuffer();
            deflateStream.Write(buffer, 0, numFrames * fwobfile.FrameInfo.FrameLength);
            deflateStream.Flush();

            estimates.Add((numFrames, (int)memStream.Length));

            return memStream.ToArray();
        }

        logData = new();

        void WriteCompressedBytes(byte[] compressedBytes, int numFrames)
        {
            outputWriter.Write(compressedBytes);

            if (compressedBytes.Length < blockLength)
                outputWriter.Write(new byte[blockLength - compressedBytes.Length]);

            byte[] buffer = uncompressedFrames.GetBuffer();
            int usedLength = numFrames * fwobfile.FrameInfo.FrameLength;
            int tailLength = (int)uncompressedFrames.Length - usedLength;

            uncompressedFrames.Position = 0;
            uncompressedFrames.Write(buffer, usedLength, tailLength);
            uncompressedFrames.SetLength(tailLength);

            numCompressions = 0;
            uncompressedFrameCount -= numFrames;
        }

        // Left closed right open: [lowerbound, upperbound)
        int estimate = (int)Math.Round(blockLength * (1.03779050920473 + 0.335439684558888 * Math.Log(blockLength)) / fwobfile.FrameInfo.FrameLength);
        int lastValue = estimate;
        int lowerbound = 1;
        int upperbound = int.MaxValue;
        int lowerboundOutcome = 0;
        int upperboundOutcome = 0;
        int firstLowerbound = 1;
        int firstUpperbound = int.MaxValue;
        int firstLowerboundOutcome = 0;
        int firstUpperboundOutcome = 0;
        byte[]? lowerboundBytes = null;

        int Estimate()
        {
            Debug.Assert(lowerboundOutcome != 0 || upperboundOutcome != 0);

            //*
            int lbest = 0, ubest = 0;

            if (lowerboundOutcome != 0)
                lbest = Math.Clamp((blockLength * lowerbound + lowerboundOutcome - 1) / lowerboundOutcome, lowerbound + 1, upperbound);
            if (upperboundOutcome != 0)
                ubest = Math.Clamp((blockLength * upperbound + upperboundOutcome - 1) / upperboundOutcome, lowerbound + 1, upperbound);

            if (lowerboundOutcome == 0)
                return ubest;
            if (upperboundOutcome == 0)
                return lbest;

            int lbweight = upperboundOutcome - blockLength - 1, ubweight = blockLength + 1 - lowerboundOutcome;
            return (lbest * lbweight + ubest * ubweight + lbweight + ubweight - 1) / (lbweight + ubweight);
            /*/
            int est = 0;
            if (upperboundOutcome == 0)
            {
                if (lowerbound == firstLowerbound || lowerboundOutcome - firstLowerboundOutcome < 32)
                    est = (blockLength * lowerbound + lowerboundOutcome - 1) / lowerboundOutcome;
                else
                    est = ((blockLength - firstLowerboundOutcome) * (lowerbound - firstLowerbound) + lowerboundOutcome - firstLowerboundOutcome - 1) / (lowerboundOutcome - firstLowerboundOutcome) + firstLowerbound;
                est = Math.Clamp(est, lowerbound + 1, lowerbound * 3 / 2);
            }
            else if (lowerboundOutcome == 0)
            {
                if (upperbound == firstUpperbound || firstUpperboundOutcome - upperboundOutcome < 32)
                    est = (blockLength * upperbound + upperboundOutcome - 1) / upperboundOutcome;
                else
                    est = firstUpperbound - ((firstUpperboundOutcome - blockLength) * (firstUpperbound - upperbound) + firstUpperboundOutcome - upperboundOutcome - 1) / (firstUpperboundOutcome - upperboundOutcome);
                est = Math.Clamp(est, (upperbound + 1) * 2 / 3, upperbound);
            }
            else
            {
                int lbest = 0, ubest = 0;

                if (lowerboundOutcome != 0)
                    lbest = Math.Clamp((blockLength * lowerbound + lowerboundOutcome - 1) / lowerboundOutcome, lowerbound + 1, upperbound);
                if (upperboundOutcome != 0)
                    ubest = Math.Clamp((blockLength * upperbound + upperboundOutcome - 1) / upperboundOutcome, lowerbound + 1, upperbound);

                if (lowerboundOutcome == 0)
                    return ubest;
                if (upperboundOutcome == 0)
                    return lbest;

                int lbweight = upperboundOutcome - blockLength - 1, ubweight = blockLength + 1 - lowerboundOutcome;
                est = (lbest * lbweight + ubest * ubweight + lbweight + ubweight - 1) / (lbweight + ubweight);
            }
            return est;
            //*/
        }

        // Use gradient descent algorithm to plan blocks with fixed length
        IEnumerator<ShortTick> enumerator = fwobfile.GetEnumerator();
        while (true)
        {
            bool hasTick = enumerator.MoveNext();

            if (hasTick)
            {
                FwobFile<ShortTick, uint>.WriteFrame(uncompressedWriter, enumerator.Current);
                uncompressedFrameCount++;

                // Wait until the the number of cached frames reaches estimate
                if (uncompressedFrameCount < estimate)
                    continue;
            }
            else
            {
                if (uncompressedFrameCount == 0)
                    break;

                estimate = uncompressedFrameCount;
            }

            // Try to compress all cached frames
            byte[] compressedBytes = GetCompressedBytes(estimate);
            double invslope = (double)estimate / compressedBytes.Length;

            if (compressedBytes.Length <= blockLength)
            {
                lowerbound = estimate;
                lowerboundOutcome = compressedBytes.Length;
                if (firstLowerboundOutcome == 0)
                {
                    firstLowerbound = lowerbound;
                    firstLowerboundOutcome = lowerboundOutcome;
                }
                lowerboundBytes = compressedBytes;

                if (lowerbound < upperbound && hasTick)
                {
                    estimate = Estimate();
                    continue;
                }
            }
            else
            {
                // By far we have enough data to compress
                upperbound = estimate - 1;
                upperboundOutcome = compressedBytes.Length;
                if (firstUpperboundOutcome == 0)
                {
                    firstUpperbound = upperbound;
                    firstUpperboundOutcome = upperboundOutcome;
                }

                while (lowerbound < upperbound)
                {
                    estimate = Estimate();
                    compressedBytes = GetCompressedBytes(estimate);
                    invslope = (double)estimate / compressedBytes.Length;

                    if (compressedBytes.Length <= blockLength)
                    {
                        lowerbound = estimate;
                        lowerboundOutcome = compressedBytes.Length;
                        if (firstLowerboundOutcome == 0)
                        {
                            firstLowerbound = lowerbound;
                            firstLowerboundOutcome = lowerboundOutcome;
                        }
                        lowerboundBytes = compressedBytes;
                    }
                    else
                    {
                        upperbound = estimate - 1;
                        upperboundOutcome = compressedBytes.Length;
                        if (firstUpperboundOutcome == 0)
                        {
                            firstUpperbound = upperbound;
                            firstUpperboundOutcome = upperboundOutcome;
                        }

                        if (lowerbound == upperbound)
                            break;
                    }
                }
            }

            Debug.Assert(lowerboundBytes != null);
            logData.Add((lowerbound, lowerboundBytes.Length, numCompressions, estimates));
            estimates = new();

            // Write a block
            WriteCompressedBytes(lowerboundBytes, lowerbound);

            estimate = (lowerbound + lastValue) / 2;
            lastValue = lowerbound;
            lowerbound = 1;
            upperbound = int.MaxValue;
            lowerboundOutcome = 0;
            upperboundOutcome = 0;
            firstLowerbound = 1;
            firstUpperbound = int.MaxValue;
            firstLowerboundOutcome = 0;
            firstUpperboundOutcome = 0;
            lowerboundBytes = null;
        }
    }

    //private static void TestDecompress(string filename)
    //{
    //    var temppath = Path.GetTempPath();

    //    using FileStream instream = new(filename, FileMode.Open, FileAccess.Read, FileShare.None, 64 * 1024);
    //    using DeflateStream deflateStream = new(instream, CompressionMode.Decompress);

    //    using BinaryReader inbr = new(instream);
    //    inbr.BaseStream.Seek(2048, SeekOrigin.Begin);
    //    MemoryStream memStream = new();
    //    BinaryWriter membw = new(deflateStream);


    //    long count = 0;

    //    while (deflateStream.Read()
    //    {
    //        if (++count == framesPerBlock)
    //        {
    //            membw.Flush();
    //            inbr.Write(memStream.ToArray());
    //            File.AppendAllText(logpath, membw.BaseStream.Length.ToString() + '\n');

    //            membw.Dispose();
    //            deflateStream.Dispose();
    //            memStream.Dispose();

    //            memStream = new();
    //            deflateStream = new(memStream, compressionLevel);
    //            membw = new(deflateStream);

    //            count = 0;
    //        }
    //    }

    //    if (count > 0)
    //    {
    //        membw.Flush();
    //        inbr.Write(memStream.ToArray());
    //    }

    //    membw.Dispose();
    //    deflateStream.Dispose();
    //    memStream.Dispose();
    //}
}
