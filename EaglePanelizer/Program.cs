/////////////////////////////////////////////////////////////////////////////////////////////////
//
// EaglePanelizer - EAGLE CAD artwork panelizer
// Copyright (c) 2017-2018 Kouji Matsui (@kozy_kekyo)
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//	http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//
/////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Runtime.InteropServices;

namespace EaglePanelizer
{
    public static class Program
    {
        private struct EaglePanelizerOptions
        {
            [Option("Force set layer number for dimension (contour)", 'd')]
            [DefaultValue(20)]
            public int DimensionLayer;

            [Option("Force set layer number for V-Cut lines", 'v')]
            [DefaultValue(46)]
            public int VcutLayer;

            [Option("Dimension contour and V-Cut line width")]
            [DefaultValue(0.254)]
            public double LineWidth;

            [Option("V-Cut line post length", 'p')]
            [DefaultValue(5.0)]
            public double VcutPostLength;

            [Option("Draw 'V-Cut' indicator", 'i')]
            public bool VcutIndicator;

            [Option("Show this help", 'h')]
            public bool Help;

            public double TargetWidth;
            public double TargetHeight;
            [Required]
            public string FromPath;
            [Required]
            public string PanelizedPath;
        }

        //////////////////////////////////////////////

        public static int Main(string[] args)
        {
            Console.WriteLine("EaglePanelizer - EAGLE CAD artwork panelizer 1.2");
            Console.WriteLine("Copyright (c) 2017-2018 Kouji Matsui (@kozy_kekyo)");
            Console.WriteLine();

            var extractor = new CommandLineExtractor<EaglePanelizerOptions>();

            try
            {
                var options = extractor.Extract(args);
                if (options.Help)
                {
                    extractor.WriteUsages(Console.Out);
                    return 0;
                }

                return Execute(options);
            }
            catch (CommandLineArgumentException ex)
            {
                Console.WriteLine(ex.Message);
                Console.WriteLine();
                extractor.WriteUsages(Console.Out);
                Console.WriteLine("ex: EaglePanelizer 100 100 pga44dip44.brd panelized.brd");
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                return Marshal.GetHRForException(ex);
            }
        }

        private static int Execute(EaglePanelizerOptions options)
        {
            // Load EAGLE artwork file.
            var document = Utilities.LoadEagleBoard(options.FromPath);

            // EAGLE format is milli-meter.
            var unit = new Unit("mm");

            // Aggregate board items.
            var boardLists =
                (from drawing in document.Root.Elements("drawing")
                 from board in drawing.Elements("board")
                 select board).
                ToArray();

            // Construct library package map.
            var packageElementsList =
                (from board in boardLists
                 from libraries in board.Elements("libraries")
                 from library in libraries.Elements("library")
                 let libraryName = (string)library.Attribute("name")
                 where !string.IsNullOrWhiteSpace(libraryName)
                 from packages in library.Elements("packages")
                 from package in packages.Elements("package")
                 let packageName = (string)package.Attribute("name")
                 where !string.IsNullOrWhiteSpace(packageName)
                 let name = string.Format("{0}/{1}", libraryName, packageName)
                 let packageElements = package.Elements().ToArray()
                 select new
                 {
                     name,
                     board,
                     packageElements
                 }).
                ToDictionary(
                    entry => entry.name,
                    entry => (from packageElement in entry.packageElements
                              let de = DimensionElement.TryCreateFrom(entry.board, packageElement, unit)
                              where de.HasValue
                              select de.Value).
                             ToArray());

            // Aggregate board element items using packages.
            var elementItemLists =
                (from board in boardLists
                 from elements in board.Elements("elements")
                 from element in elements.Elements("element")
                 let libraryName = (string)element.Attribute("library")
                 let packageName = (string)element.Attribute("package")
                 where !string.IsNullOrWhiteSpace(libraryName) && !string.IsNullOrWhiteSpace(packageName)
                 let name = string.Format("{0}/{1}", libraryName, packageName)
                 from packageElement in packageElementsList[name]
                 let oe = packageElement.TryOffsetOfOrigin(element, true)
                 where oe.HasValue
                 select oe.Value).
                ToArray();

            // Aggregate board plain items.
            var plainItemLists =
                (from board in boardLists
                 from plainElement in board.Elements("plain")
                 from element in plainElement.Elements()
                 let de = DimensionElement.TryCreateFrom(board, element, unit)
                 where de.HasValue
                 select de.Value).
                ToArray();

            // Extract contour region from dimension layer.
            var dimensionLayerElements =
                (from element in elementItemLists.Concat(plainItemLists)
                 where element.Layer == options.DimensionLayer
                 select element).
                ToArray();

            var minX = dimensionLayerElements.Min(element => element.MinX());
            var minY = dimensionLayerElements.Min(element => element.MinY());
            var maxX = dimensionLayerElements.Max(element => element.MaxX());
            var maxY = dimensionLayerElements.Max(element => element.MaxY());

            // Totally size of original artwork.
            var width0 = maxX - minX;
            var height0 = maxY - minY;

            Console.WriteLine(
                $"Original board: Size=({width0}, {height0}), ({minX}, {minY}) - ({maxX}, {maxY})");

            // Aggregate board elements.
            var elementLists =
                (from drawing in document.Root.Elements("drawing")
                 from board in drawing.Elements("board")
                 from elements in board.Elements("elements")
                 select new { parent = elements, list = elements.Elements().ToArray() }).
                ToArray();

            // Aggregate board signals.
            var signalLists =
                (from drawing in document.Root.Elements("drawing")
                 from board in drawing.Elements("board")
                 from signals in board.Elements("signals")
                 select new { parent = signals, list = signals.Elements().ToArray() }).
                ToArray();

            // Remove dimension layer elements.
            foreach (var element in elementItemLists
                .Concat(plainItemLists)
                .Where(element => element.Layer == options.DimensionLayer))
            {
                element.Original.Remove();
            }

            // Reconstruct plain items without dimension layer.
            plainItemLists =
                plainItemLists
                .Where(element => element.Layer != options.DimensionLayer)
                .ToArray();

            // Extract first plain (Maybe only one plain).
            var plain0 = plainItemLists.First().Element.Parent;

            // Main block of duplicate.
            var width = 0.0;
            var height = 0.0;
            var count = 1;

            // Limit duplicates from arguments (targetWidth, targetHeight)
            for (var yoffset = 0.0; (yoffset + height0) < options.TargetHeight; yoffset += height0)
            {
                for (var xoffset = 0.0; (xoffset + width0) < options.TargetWidth; xoffset += width0)
                {
                    // Ignore original artwork position.
                    if ((xoffset == 0) && (yoffset == 0))
                    {
                        continue;
                    }

                    // Duplicate plain dupPlains
                    var dupPlains =
                        (from de in plainItemLists
                         select Utilities.DupElement(
                            de.Element.Parent, de.Element.Parent, de.Element, xoffset, yoffset, count)).
                        ToArray();

                    // Duplicate elements.
                    var dupElements =
                        (from entry in elementLists
                         from element in entry.list
                         select Utilities.DupElement(
                             plain0, entry.parent, element, xoffset, yoffset, count)).
                        ToArray();

                    // Duplicate signals.
                    var dupSignals =
                        (from entry in signalLists
                         from element in entry.list
                         select Utilities.DupElement(
                             plain0, entry.parent, element, xoffset, yoffset, count)).
                        ToArray();

                    Console.WriteLine($"Dup[{count}]: ({xoffset}, {yoffset})");
                    count++;

                    width = Math.Max(width, xoffset);
                    height = Math.Max(height, yoffset);
                }
            }

            width += width0;
            height += height0;

            // Apply contour lines (counter-clock wise)
            plain0.AddWireElement(
                minX, minY, minX + width, minY,
                options.LineWidth, options.DimensionLayer);
            plain0.AddWireElement(
                minX + width, minY, minX + width, minY + height,
                options.LineWidth, options.DimensionLayer);
            plain0.AddWireElement(
                minX + width, minY + height, minX, minY + height,
                options.LineWidth, options.DimensionLayer);
            plain0.AddWireElement(
                minX, minY + height, minX, minY,
                options.LineWidth, options.DimensionLayer);

            // Apply calculated vcut lines.
            for (var yoffset = height0; (yoffset + height0) < options.TargetHeight; yoffset += height0)
            {
                plain0.AddWireElement(
                    minX - options.VcutPostLength,
                    minY + yoffset,
                    minX + width + options.VcutPostLength,
                    minY + yoffset,
                    options.LineWidth,
                    options.VcutLayer);
            }
            for (var xoffset = width0; (xoffset + width0) < options.TargetWidth; xoffset += width0)
            {
                plain0.AddWireElement(
                    minX + xoffset,
                    minY - options.VcutPostLength,
                    minX + xoffset,
                    minY + height + options.VcutPostLength,
                    options.LineWidth,
                    options.VcutLayer);
            }

            // Apply vcut indicators
            if (options.VcutIndicator)
            {
                for (var yoffset = height0; (yoffset + height0) < options.TargetHeight; yoffset += height0)
                {
                    plain0.AddVectorTextElement(
                        minX + width + 2.0,
                        minY + yoffset + 1.0,
                        1.0,
                        10,
                        options.VcutLayer,
                        "vcut");
                }
                for (var xoffset = width0; (xoffset + width0) < options.TargetWidth; xoffset += width0)
                {
                    plain0.AddVectorTextElement(
                        minX + xoffset + 1.0,
                        minY + height + 2.0,
                        1.0,
                        10,
                        options.VcutLayer,
                        "vcut");
                }
            }

            Console.WriteLine(
                $"Totally panelized: Count={count}, Size=({width}, {height}), ({minX}, {minY}) - ({minX + width}, {minY + height})");

            // Write new artwork file.
            Utilities.SaveEagleBoard(options.PanelizedPath, document);

            return 0;
        }
    }
}
