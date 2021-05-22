using CommandLine;
using Imageflow.Fluent;
using LB.PhotoGalleries.Models;
using LB.PhotoGalleries.Models.Enums;
using Spectre.Console;
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
        #region members
        private static double OverallProgressIncrementAmount { get; set; }
        #endregion

        public class Options
        {
            [Option('i', "inputpath", Required = true, HelpText = "Where to find input images for processing.")]
            public string InputPath { get; set; }
        }

        private static void Main(string[] args)
        {
            // needs to:
            // know where to find source files
            // know where to output generates files to
            // generate a selection of output images using different encoders and settings

            // stats we want:
            // Images
            // >>> File Specs
            //     >>> Generation time
            //     >>> Byte size
            // Overall tool run time

            // Input Image
            // ------------------------------
            // File Spec | time       | size
            // ------------------------------
            // spec3840  | 0m 0s 50ms | 34kb
            // ------------------------------

            var inputPath = string.Empty;
            Parser.Default.ParseArguments<Options>(args).WithParsed(o => { inputPath = o.InputPath; });

            if (!Directory.Exists(inputPath))
            {
                Console.WriteLine($"Input path \"{inputPath}\" does not exist. Cannot continue.");
                return;
            }

            // start an overall timer
            var overallStopwatch = new Stopwatch();
            overallStopwatch.Start();

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
            var imagesToGenerate = specs.Count * files.Length;
            OverallProgressIncrementAmount = 100d / imagesToGenerate;
            Console.WriteLine($"Generating {imagesToGenerate} images...");

            AnsiConsole.Progress().Start(ctx =>
            {
                // define progress tasks
                var overallProgress = ctx.AddTask("[green]Generating images[/]");

                Parallel.ForEach(files, file =>
                {
                    var table = new Table();
                    table.AddColumn("Image File Spec");
                    table.AddColumn("Generation Time (ms)");
                    table.AddColumn("Byte Size");

                    ProcessInputFileAsync(file, specs, outputPath, table, overallProgress).GetAwaiter().GetResult();
                    AnsiConsole.Render(table);
                });
            });

            overallStopwatch.Stop();
            Console.WriteLine($"Tool took {overallStopwatch.Elapsed.Minutes}m {overallStopwatch.Elapsed.Seconds}s {overallStopwatch.Elapsed.Milliseconds}ms to complete.");
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
                new ImageFileSpec(FileSpec.SpecLowRes, FileSpecFormat.Jpeg, 400, 80, 0f, FileSpec.SpecLowRes.ToString()),
                new ImageFileSpec(FileSpec.Spec800, FileSpecFormat.Jpeg, 800, 90, 0f, FileSpec.Spec800.ToString()),
                new ImageFileSpec(FileSpec.Spec1920, FileSpecFormat.Jpeg, 1920, 90, 0f, FileSpec.Spec1920.ToString()),
                new ImageFileSpec(FileSpec.Spec2560, FileSpecFormat.Jpeg, 2560, 90, 0f, FileSpec.Spec2560.ToString()),
                new ImageFileSpec(FileSpec.Spec3840, FileSpecFormat.Jpeg, 3840, 90, 0f, FileSpec.Spec3840.ToString()),

                // very high quality, low efficiency, max support
                new ImageFileSpec(FileSpec.SpecLowRes, FileSpecFormat.Jpeg, 400, 90, 0f, FileSpec.SpecLowRes.ToString()),
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

        private static async Task ProcessInputFileAsync(string file, IEnumerable<ImageFileSpec> specs, string outputPath, Table table, ProgressTask overallProgress)
        {
            var inputImageStopwatch = new Stopwatch();
            inputImageStopwatch.Start();
            var fileBytes = await File.ReadAllBytesAsync(file);

            // each input file should have it's own folder to make navigation and comparison easier
            var inputFilenameWithoutExtension = Path.GetFileNameWithoutExtension(file);
            var inputImageOutputDirectory = Path.Combine(outputPath, inputFilenameWithoutExtension);
            Directory.CreateDirectory(inputImageOutputDirectory);

            Parallel.ForEach(specs, spec =>
            {
                ProcessImageFileSpecAsync(fileBytes, spec, inputImageOutputDirectory, table, overallProgress).GetAwaiter().GetResult();
            });

            inputImageStopwatch.Stop();
        }

        private static async Task ProcessImageFileSpecAsync(byte[] fileBytes, ImageFileSpec spec, string inputImageOutputDirectory, Table table, ProgressTask overallProgress)
        {
            var extension = spec.FileSpecFormat == FileSpecFormat.Jpeg ? "jpg" : "webp";
            var filename = $"{spec.PixelLength}pl-{spec.Quality}q-{spec.SharpeningAmount}s.{extension}";
            var filePath = Path.Combine(inputImageOutputDirectory, filename);

            await using var resizedImageStream = await GenerateImageAsync(fileBytes, spec, table);
            await using var fileStream = File.OpenWrite(filePath);
            await resizedImageStream.CopyToAsync(fileStream);

            overallProgress.Increment(OverallProgressIncrementAmount);
        }

        private static async Task<Stream> GenerateImageAsync(byte[] originalImage, ImageFileSpec imageFileSpec, Table table)
        {
            var individualImageStopwatch = new Stopwatch();
            individualImageStopwatch.Start();

            using (var job = new ImageJob())
            {
                var buildNode = job.Decode(originalImage);
                var resampleHints = new ResampleHints();

                if (imageFileSpec.SharpeningAmount > 0)
                    resampleHints.SetSharpen(25.0f, SharpenWhen.Downscaling).SetResampleFilters(InterpolationFilter.Robidoux, null);

                buildNode = buildNode.ConstrainWithin((uint?)imageFileSpec.PixelLength, (uint?)imageFileSpec.PixelLength, resampleHints);
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

                    individualImageStopwatch.Stop();
                    var kb = newImageBytes.Value.Count / 1024;
                    var size = $"{kb} kb";
                    table.AddRow(imageFileSpec.ToString(), individualImageStopwatch.ElapsedMilliseconds.ToString(), size);

                    return newStream;
                }
            }

            individualImageStopwatch.Stop();
            AnsiConsole.Render(new Markup("[bold red]Something went wrong with an image generation.[/]"));
            return null;
        }
    }
}
