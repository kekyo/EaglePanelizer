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
using System.IO;
using System.Linq;

namespace EaglePanelizer
{
    public static class Program
    {
        public static int Main(string[] args)
        {
            Console.WriteLine("EaglePanelizer - EAGLE CAD artwork panelizer 1.1");
            Console.WriteLine("Copyright (c) 2017-2018 Kouji Matsui (@kozy_kekyo)");
            Console.WriteLine();

            if (args.Length < 4)
            {
                Console.WriteLine("usage: EaglePanelizer <width> <height> <from-path> <panelized-path>");
                Console.WriteLine("ex: EaglePanelizer 100 100 pga44dip44.brd panelized.brd");
                return 0;
            }

            // Get arguments.
            var targetWidth = double.Parse(args[0]);
            var targetHeight = double.Parse(args[1]);

            var fromPath = Path.GetFullPath(args[2]);
            var panelizedPath = Path.GetFullPath(args[3]);

            // Load EAGLE artwork file.
            var document = Utilities.LoadEagleBoard(fromPath);

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
                 let list = package.Elements().ToArray()
                 select new
                 {
                     name,
                     parent = package,
                     list = list
                 }).
                ToDictionary(
                    entry => entry.name,
                    entry => (from element in entry.list
                              let de = DimensionElement.TryCreateFrom(element, unit)
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
                 let oe = packageElement.TryOffsetOf(element, unit)
                 where oe.HasValue
                 select oe.Value).
                ToArray();

            // Aggregate board plain items.
            var plainItemLists =
                (from board in boardLists
                 from plainElement in board.Elements("plain")
                 from element in plainElement.Elements()
                 let de = DimensionElement.TryCreateFrom(element, unit)
                 where de.HasValue
                 select de.Value).
                ToArray();

            // Extract contour region from layer 20 (Dimension).
            var layer20Elements =
                (from element in elementItemLists.Concat(plainItemLists)
                 where element.Layer == 20
                 select element).
                ToArray();

            var minX = layer20Elements.Min(layer20Element => layer20Element.MinX());
            var minY = layer20Elements.Min(layer20Element => layer20Element.MinY());
            var maxX = layer20Elements.Max(layer20Element => layer20Element.MaxX());
            var maxY = layer20Elements.Max(layer20Element => layer20Element.MaxY());

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

            // Extract first plain (Maybe only one plain).
            var plain0 = plainItemLists.First().Element.Parent;

            // Main block of duplicate.
            var width = 0.0;
            var height = 0.0;
            var count = 1;

            // Limit duplicates from arguments (targetWidth, targetHeight)
            for (var yoffset = 0.0; (yoffset + height0) < targetHeight; yoffset += height0)
            {
                for (var xoffset = 0.0; (xoffset + width0) < targetWidth; xoffset += width0)
                {
                    // Ignore original artwork position.
                    if ((xoffset == 0) && (yoffset == 0))
                    {
                        continue;
                    }

                    // Duplicate plain items.
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

            Console.WriteLine(
                $"Totally panelized: Count={count}, Size=({width}, {height}), ({minX}, {minY}) - ({minX + width}, {minY + height})");

            // Write new artwork file.
            Utilities.SaveEagleBoard(panelizedPath, document);

            return 0;
        }
    }
}
