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
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace EaglePanelizer
{
    public static class Program
    {
        private static XElement DupElement(
            XElement plains,
            XElement parent,
            XElement target,
            double offsetX,
            double offsetY,
            int suffixNumber)
        {
            // 1. Duplicate XElement.
            var dupElement = XElement.Parse(target.ToString());

            // 2. Fix all XElements.
            var name = dupElement.Attribute("name");
            foreach (var elm in dupElement.DescendantsAndSelf().ToArray())
            {
                // 2-1. Move layer 25,26 (tName, bName) to 21, 22 (tPlace, bPlace).
                var layer = elm.Attribute("layer");
                if (((int?)layer == 25) || ((int?)layer == 26))
                {
                    layer.Value = ((int)layer - 4).ToString();
                }

                // 2-2. Realize NAME attribute.
                var ename = elm.Attribute("name");
                if ((name != null) && (elm.Name == "attribute") && ((string)ename == "NAME"))
                {
                    // 2-2-1. Convert attribute to text.
                    elm.Name = "text";
                    elm.Value = name.Value;
                    ename.Remove();

                    // 2-2-2. Move to plains.
                    elm.Remove();
                    plains.Add(elm);
                }

                // 2-3. Fix element reference.
                var element = elm.Attribute("element");
                if (element != null)
                {
                    element.Value = element.Value + "_" + suffixNumber;
                }

                // 2-4. Calculate element offset.
                void TrySetOffsetValue(XElement xelm, string attributeName, double offset)
                {
                    var xattr = xelm.Attribute(attributeName);
                    if (xattr != null)
                    {
                        xattr.Value = ((double)xattr + offset).ToString();
                    }
                }

                TrySetOffsetValue(elm, "x", offsetX);
                TrySetOffsetValue(elm, "x1", offsetX);
                TrySetOffsetValue(elm, "x2", offsetX);
                TrySetOffsetValue(elm, "y", offsetY);
                TrySetOffsetValue(elm, "y1", offsetY);
                TrySetOffsetValue(elm, "y2", offsetY);
            }

            // 3. Fix duplicated name.
            if (name != null)
            {
                name.Value = name.Value + "_" + suffixNumber;
            }

            // 4. Add to parent.
            parent.Add(dupElement);

            return dupElement;
        }

        public static int Main(string[] args)
        {
            if (args.Length < 4)
            {
                Console.WriteLine("EaglePanelizer - EAGLE CAD artwork panelizer 1.1");
                Console.WriteLine("Copyright (c) 2017-2018 Kouji Matsui (@kozy_kekyo)");
                Console.WriteLine();
                Console.WriteLine("usage: EaglePanelizer <width> <height> <from-path> <panelized-path>");
                Console.WriteLine("ex: EaglePanelizer 100 100 pga44dip44.brd panelized.brd");
                return 0;
            }

            // Get arguments.
            var targetWidth = double.Parse(args[0]);
            var targetHeight = double.Parse(args[1]);

            var fromPath = args[2];
            var panelizedPath = args[3];

            // Load EAGE artwork file.
            XDocument LoadEagleBoard(string path)
            {
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.Read))
                {
                    return XDocument.Load(stream);
                }
            }

            var document = LoadEagleBoard(fromPath);

            // TODO: Get artwork unit.
            var unitDist =
                (from drawing in document.Root.Elements("drawing")
                 from grid in drawing.Elements("grid")
                 let ud = (string) grid.Attribute("unitdist")
                 where ud != null
                 select ud).
                First();
            var unit = new Unit(unitDist);

            // Aggregate board plain items.
            var plainItemLists =
                (from drawing in document.Root.Elements("drawing")
                 from board in drawing.Elements("board")
                 from plain in board.Elements("plain")
                 select new { parent = plain, list = plain.Elements().ToArray() }).
                ToArray();

            // Extract contour region from layer 20 (Dimension).
            var layer20Elements =
                (from entry in plainItemLists
                 from element in entry.list
                 where (int?)element.Attribute("layer") == 20 
                 select element).
                ToArray();

            var minX = layer20Elements.Min(
                layer20Element => unit.Value(layer20Element, "x1", 0));
            var minY = layer20Elements.Min(
                layer20Element => unit.Value(layer20Element, "y1", 0));
            var maxX = layer20Elements.Max(
                layer20Element => unit.Value(layer20Element, "x1", 0));
            var maxY = layer20Elements.Max(
                layer20Element => unit.Value(layer20Element, "y1", 0));

            // Totally size of original artwork.
            var width0 = maxX - minX;
            var height0 = maxY - minY;

            Console.WriteLine($"Original board: Size=({width0}, {height0}), ({minX}, {minY}) - ({maxX}, {maxY})");

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
            var plain0 = plainItemLists.First().parent;

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
                        (from entry in plainItemLists
                         from element in entry.list
                         select DupElement(entry.parent, entry.parent, element, xoffset, yoffset, count)).
                        ToArray();

                    // Duplicate elements.
                    var dupElements =
                        (from entry in elementLists
                         from element in entry.list
                         select DupElement(plain0, entry.parent, element, xoffset, yoffset, count)).
                        ToArray();

                    // Duplicate signals.
                    var dupSignals =
                        (from entry in signalLists
                         from element in entry.list
                         select DupElement(plain0, entry.parent, element, xoffset, yoffset, count)).
                        ToArray();

                    Console.WriteLine($"Dup[{count}]: ({xoffset}, {yoffset})");
                    count++;

                    width = Math.Max(width, xoffset);
                    height = Math.Max(height, yoffset);
                }
            }

            width += width0;
            height += height0;

            Console.WriteLine($"Totally panelized: Count={count}, Size=({width}, {height}), ({minX}, {minY}) - ({minX + width}, {minY + height})");

            // Write new artwork file.
            using (var stream = new FileStream(panelizedPath, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
            {
                document.Save(stream);
                stream.Flush();
            }

            return 0;
        }
    }
}
