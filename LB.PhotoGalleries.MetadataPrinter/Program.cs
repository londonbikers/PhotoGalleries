using MetadataExtractor;
using MetadataExtractor.Formats.Xmp;
using System;
using System.IO;

namespace LB.PhotoGalleries.MetadataPrinter
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            Console.WriteLine("Metadata Printer - Prints out all metadata for an image using the same package the site does.");
            if (args.Length == 0)
            {
                Console.WriteLine("No image path specified. Quitting.");
                return;
            }

            var path = args[0];
            if (!File.Exists(path))
            {
                Console.WriteLine("File doesn't exist. Quitting.");
                return;
            }

            var directories = ImageMetadataReader.ReadMetadata(path);
            using (var outputFile = new StreamWriter(@$"c:\temp\{Path.GetFileNameWithoutExtension(path)}-metadata.txt"))
            {
                foreach (var directory in directories)
                {
                    foreach (var tag in directory.Tags)
                        outputFile.WriteLine($"{directory.Name} - {tag.Name} = {tag.Description}");

                    outputFile.WriteLine("");

                    if (directory.Name.Equals("xmp", StringComparison.CurrentCultureIgnoreCase))
                    {
                        var xmpDirectory = (XmpDirectory)directory;
                        if (xmpDirectory.XmpMeta != null)
                        {
                            foreach (var property in xmpDirectory.XmpMeta.Properties)
                                outputFile.WriteLine($"XmpMeta - {property.Namespace} : {property.Path} = {property.Value}");
                        }

                        outputFile.WriteLine("");
                    }
                }
            }

            Console.WriteLine(@"All done, check c:\temp");
        }
    }
}