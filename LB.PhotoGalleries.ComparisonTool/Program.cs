using CommandLine;
using Imageflow.Fluent;
using LB.PhotoGalleries.Models;
using LB.PhotoGalleries.Models.Enums;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace LB.PhotoGalleries.ComparisonTool
{
    /// <summary>
    /// Tool to test out image processing options, i.e. to help find a balance of quality/size/supportability.
    /// </summary>
    internal class Program
    {
        public class Options
        {
            [Option('i', "inputpath", Required = true, HelpText = "Where to find input images for processing.")]
            public string InputPath { get; set; }
        }

        private static async Task Main(string[] args)
        {
            // needs to:
            // know where to find source files
            // know where to output generates files to
            // generate a selection of output images using different encoders and settings

            var inputPath = string.Empty;
            Parser.Default.ParseArguments<Options>(args).WithParsed(o => { inputPath = o.InputPath; });

            if (!Directory.Exists(inputPath))
            {
                Console.WriteLine($"Input path \"{inputPath}\" does not exist. Cannot continue.");
                return;
            }

            // start an overall timer
            var stopwatch = new Stopwatch();
            stopwatch.Start();

            // get a list of all images in the input folder
            var files = Directory.GetFiles(inputPath, "*.jp*g", SearchOption.TopDirectoryOnly);
            if (files.Length == 0)
            {
                Console.WriteLine($"Input path \"{inputPath}\" doesn't contain any jpg/jpeg files. Cannot continue.");
                return;
            }
            
            // create the output folder if necessary
            // under this we will create a timestamped folder for each run of the program to allow for comparisons over time
            var outputPath = Path.Combine(inputPath, "output", DateTime.Now.Ticks.ToString());
            Directory.CreateDirectory(outputPath);
            Console.WriteLine($"Created the output folder: {outputPath}");

            // get the list of specifications for images we want to produce and compare with each other
            var specs = ProduceImageFileSpecs();

            Parallel.ForEach(files, file =>
            {
                ProcessInputFileAsync(file, specs, outputPath).GetAwaiter().GetResult();
            });

            //foreach (var file in files)
            //{
            //    Console.WriteLine($"Processing file: {file}...");
            //    var fileBytes = await File.ReadAllBytesAsync(file);

            //    foreach (var spec in specs)
            //    {
            //        var extension = spec.FileSpecFormat == FileSpecFormat.Jpeg ? "jpg" : "webp";
            //        var filename = $"file-{spec.PixelLength}pl-{spec.Quality}q-{spec.SharpeningAmount}s.{extension}";
            //        var filePath = Path.Combine(outputPath, filename);
            //        Console.WriteLine($"\tCreating image: {filePath}...");

            //        await using var resizedImageStream = await GenerateImageAsync(fileBytes, spec);
            //        await using var fileStream = File.OpenWrite(filePath);
            //        await resizedImageStream.CopyToAsync(fileStream);
            //    }
            //}

            stopwatch.Stop();
            Console.WriteLine($"Tool took {stopwatch.Elapsed.Minutes}m {stopwatch.Elapsed.Seconds}s {stopwatch.Elapsed.Milliseconds}ms to complete.");
        }

        private static List<ImageFileSpec> ProduceImageFileSpecs()
        {
            return new()
            {
                // lower quality, higher efficiency, max support
                new ImageFileSpec(FileSpec.SpecLowRes, FileSpecFormat.Jpeg, 400, 25, 0f, FileSpec.SpecLowRes.ToString()),
                new ImageFileSpec(FileSpec.Spec800, FileSpecFormat.Jpeg, 800, 50, 0f, FileSpec.Spec800.ToString()),
                new ImageFileSpec(FileSpec.Spec1920, FileSpecFormat.Jpeg, 1920, 50, 0f, FileSpec.Spec1920.ToString()),
                new ImageFileSpec(FileSpec.Spec2560, FileSpecFormat.Jpeg, 2560, 50, 0f, FileSpec.Spec2560.ToString()),
                new ImageFileSpec(FileSpec.Spec3840, FileSpecFormat.Jpeg, 3840, 50, 0f, FileSpec.Spec3840.ToString()),

                // low quality, high efficiency, max support
                new ImageFileSpec(FileSpec.SpecLowRes, FileSpecFormat.Jpeg, 400, 50, 0f, FileSpec.SpecLowRes.ToString()),
                new ImageFileSpec(FileSpec.Spec800, FileSpecFormat.Jpeg, 800, 70, 0f, FileSpec.Spec800.ToString()),
                new ImageFileSpec(FileSpec.Spec1920, FileSpecFormat.Jpeg, 1920, 70, 0f, FileSpec.Spec1920.ToString()),
                new ImageFileSpec(FileSpec.Spec2560, FileSpecFormat.Jpeg, 2560, 70, 0f, FileSpec.Spec2560.ToString()),
                new ImageFileSpec(FileSpec.Spec3840, FileSpecFormat.Jpeg, 3840, 70, 0f, FileSpec.Spec3840.ToString()),

                // medium quality, medium efficiency, max support
                new ImageFileSpec(FileSpec.SpecLowRes, FileSpecFormat.Jpeg, 400, 75, 0f, FileSpec.SpecLowRes.ToString()),
                new ImageFileSpec(FileSpec.Spec800, FileSpecFormat.Jpeg, 800, 80, 0f, FileSpec.Spec800.ToString()),
                new ImageFileSpec(FileSpec.Spec1920, FileSpecFormat.Jpeg, 1920, 80, 0f, FileSpec.Spec1920.ToString()),
                new ImageFileSpec(FileSpec.Spec2560, FileSpecFormat.Jpeg, 2560, 80, 0f, FileSpec.Spec2560.ToString()),
                new ImageFileSpec(FileSpec.Spec3840, FileSpecFormat.Jpeg, 3840, 80, 0f, FileSpec.Spec3840.ToString()),

                // high quality, medium efficiency, max support
                new ImageFileSpec(FileSpec.SpecLowRes, FileSpecFormat.Jpeg, 400, 75, 0f, FileSpec.SpecLowRes.ToString()),
                new ImageFileSpec(FileSpec.Spec800, FileSpecFormat.Jpeg, 800, 90, 0f, FileSpec.Spec800.ToString()),
                new ImageFileSpec(FileSpec.Spec1920, FileSpecFormat.Jpeg, 1920, 90, 0f, FileSpec.Spec1920.ToString()),
                new ImageFileSpec(FileSpec.Spec2560, FileSpecFormat.Jpeg, 2560, 90, 0f, FileSpec.Spec2560.ToString()),
                new ImageFileSpec(FileSpec.Spec3840, FileSpecFormat.Jpeg, 3840, 90, 0f, FileSpec.Spec3840.ToString()),

                // very high quality, low efficiency, max support
                new ImageFileSpec(FileSpec.SpecLowRes, FileSpecFormat.Jpeg, 400, 75, 0f, FileSpec.SpecLowRes.ToString()),
                new ImageFileSpec(FileSpec.Spec800, FileSpecFormat.Jpeg, 800, 100, 0f, FileSpec.Spec800.ToString()),
                new ImageFileSpec(FileSpec.Spec1920, FileSpecFormat.Jpeg, 1920, 100, 0f, FileSpec.Spec1920.ToString()),
                new ImageFileSpec(FileSpec.Spec2560, FileSpecFormat.Jpeg, 2560, 100, 0f, FileSpec.Spec2560.ToString()),
                new ImageFileSpec(FileSpec.Spec3840, FileSpecFormat.Jpeg, 3840, 100, 0f, FileSpec.Spec3840.ToString()),

                // high quality, high efficiency, lower support
                new ImageFileSpec(FileSpec.SpecLowRes, FileSpecFormat.WebP, 400, 75, 0f, FileSpec.SpecLowRes.ToString()),
                new ImageFileSpec(FileSpec.Spec800, FileSpecFormat.WebP, 800, 90, 0f, FileSpec.Spec800.ToString()),
                new ImageFileSpec(FileSpec.Spec1920, FileSpecFormat.WebP, 1920, 90, 0f, FileSpec.Spec1920.ToString()),
                new ImageFileSpec(FileSpec.Spec2560, FileSpecFormat.WebP, 2560, 90, 0f, FileSpec.Spec2560.ToString()),
                new ImageFileSpec(FileSpec.Spec3840, FileSpecFormat.WebP, 3840, 90, 0f, FileSpec.Spec3840.ToString())
            };
        }

        private static async Task<Stream> GenerateImageAsync(byte[] originalImage, ImageFileSpec imageFileSpec)
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();

            using (var job = new ImageJob())
            {
                var buildNode = job.Decode(originalImage);
                var resampleHints = new ResampleHints();

                if (imageFileSpec.SharpeningAmount > 0)
                    resampleHints.SetSharpen(25.0f, SharpenWhen.Downscaling).SetResampleFilters(InterpolationFilter.Robidoux, null);

                buildNode = buildNode.ConstrainWithin((uint?) imageFileSpec.PixelLength, (uint?) imageFileSpec.PixelLength, resampleHints);
                IEncoderPreset encoderPreset;

                if (imageFileSpec.FileSpecFormat == FileSpecFormat.WebP)
                    encoderPreset = new WebPLosslessEncoder();
                else
                    encoderPreset = new MozJpegEncoder(imageFileSpec.Quality, true);

                var result = await buildNode
                    .EncodeToBytes(encoderPreset)
                    .Finish()
                    .SetSecurityOptions(new SecurityOptions()
                        .SetMaxDecodeSize(new FrameSizeLimit(12000, 12000, 100))
                        .SetMaxFrameSize(new FrameSizeLimit(12000, 12000, 100))
                        .SetMaxEncodeSize(new FrameSizeLimit(12000, 12000, 30)))
                    .InProcessAsync();

                var newImageBytes = result.First.TryGetBytes();
                if (newImageBytes.HasValue)
                {
                    var newStream = new MemoryStream(newImageBytes.Value.ToArray());

                    stopwatch.Stop();
                    //_log.Information($"LB.PhotoGalleries.Worker.Program.GenerateImageAsync() - Image {image.Id} and spec {imageFileSpec.FileSpec} done. Elapsed time: {stopwatch.ElapsedMilliseconds}ms");
                    return newStream;
                }
            }

            stopwatch.Stop();
            return null;
        }

        private static async Task ProcessInputFileAsync(string file, List<ImageFileSpec> specs, string outputPath)
        {
            Console.WriteLine($"Processing file: {file}...");
            var fileBytes = await File.ReadAllBytesAsync(file);

            foreach (var spec in specs)
            {
                var inputFilenameWithoutExtension = Path.GetFileNameWithoutExtension(file);
                var extension = spec.FileSpecFormat == FileSpecFormat.Jpeg ? "jpg" : "webp";
                var filename = $"{inputFilenameWithoutExtension}-{spec.PixelLength}pl-{spec.Quality}q-{spec.SharpeningAmount}s.{extension}";
                var filePath = Path.Combine(outputPath, filename);
                Console.WriteLine($"\tCreating image: {filePath}...");

                await using var resizedImageStream = await GenerateImageAsync(fileBytes, spec);
                await using var fileStream = File.OpenWrite(filePath);
                await resizedImageStream.CopyToAsync(fileStream);
            }
        }
    }
}
