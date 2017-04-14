/////////////////////////////////////////////////////////////////////////////////////////////////
//
// EaglePanelizer - EAGLE CAD artwork panelizer
// Copyright (c) 2017 Kouji Matsui (@kekyo2)
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
using System.Threading;
using System.Xml.Linq;

namespace EaglePanelizer
{
    internal sealed class Unit
    {
        private enum UnitType
        {
            mm = 0,
            mic = 1,
            mil = 2,
            inch = 3
        }

        private readonly UnitType type;

        public Unit(string unitDist)
        {
            type = (UnitType) Enum.Parse(typeof(UnitType), unitDist);
        }

        public double Value(XElement element, string attributeName, double defaultValue)
        {
            return this.Value(element.Attribute(attributeName)) ?? defaultValue;
        }

        public double? Value(XElement element, string attributeName)
        {
            return this.Value(element.Attribute(attributeName));
        }

        public double Value(XAttribute attr, double defaultValue)
        {
            return this.Value(attr) ?? defaultValue;
        }

        public double? Value(XAttribute attr)
        {
            var value = (double?)attr;
            if (value == null)
            {
                return null;
            }

            switch (type)
            {
                case UnitType.mil:
                    return value * 0.0254;
                case UnitType.inch:
                    return value * 25.4;
                case UnitType.mic:
                    return value * 0.001;
                default:
                    return value;
            }
        }
    }

    public static class Program
    {
        private static XDocument LoadEagleBoard(string path)
        {
            using (var stream = File.OpenRead(path))
            {
                return XDocument.Load(stream);
            }
        }

        private static void TrySetOffsetValue(XElement element, string attributeName, double offset)
        {
            var xattr = (double?)element.Attribute(attributeName);
            if (xattr != null)
            {
                element.Attribute(attributeName).Value = (xattr.Value + offset).ToString();
            }
        }

        private static XElement MakeOffset(XElement parent, XElement dup, double offsetX, double offsetY, int count)
        {
            var name = dup.Attribute("name");
            if (name != null)
            {
                name.Value = name.Value + "_" + count;
            }

            foreach (var elm in dup.DescendantsAndSelf())
            {
                var element = elm.Attribute("element");
                if (element != null)
                {
                    element.Value = element.Value + "_" + count;
                }

                TrySetOffsetValue(elm, "x", offsetX);
                TrySetOffsetValue(elm, "x1", offsetX);
                TrySetOffsetValue(elm, "x2", offsetX);
                TrySetOffsetValue(elm, "y", offsetY);
                TrySetOffsetValue(elm, "y1", offsetY);
                TrySetOffsetValue(elm, "y2", offsetY);
            }

            parent.Add(dup);
            return dup;
        }

        public static int Main(string[] args)
        {
            var targetWidth = double.Parse(args[0]);
            var targetHeight = double.Parse(args[1]);

            var fromPath = args[2];
            var panelizedPath = args[3];

            var document = LoadEagleBoard(fromPath);

            var unitDist =
                (from drawing in document.Root.Elements("drawing")
                 from grid in drawing.Elements("grid")
                 let ud = (string) grid.Attribute("unitdist")
                 where ud != null
                 select ud).
                First();
            var unit = new Unit(unitDist);

            var plainLists =
                (from drawing in document.Root.Elements("drawing")
                 from board in drawing.Elements("board")
                 from plain in board.Elements("plain")
                 select new { parent = plain, list = plain.Elements().ToArray() }).
                ToArray();

            var layer20Elements =
                (from entry in plainLists
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

            var width0 = maxX - minX;
            var height0 = maxY - minY;

            Console.WriteLine($"Original board: Size=({width0}, {height0}), ({minX}, {minY}) - ({maxX}, {maxY})");

            var elementLists =
                (from drawing in document.Root.Elements("drawing")
                 from board in drawing.Elements("board")
                 from elements in board.Elements("elements")
                 select new { parent = elements, list = elements.Elements().ToArray() }).
                ToArray();

            var signalLists =
                (from drawing in document.Root.Elements("drawing")
                    from board in drawing.Elements("board")
                    from signals in board.Elements("signals")
                    select new { parent = signals, list = signals.Elements().ToArray() }).
                ToArray();

            var width = 0.0;
            var height = 0.0;
            var count = 1;
            for (var yoffset = 0.0; (yoffset + height0) < targetHeight; yoffset += height0)
            {
                for (var xoffset = 0.0; (xoffset + width0) < targetWidth; xoffset += width0)
                {
                    if ((xoffset == 0) && (yoffset == 0))
                    {
                        continue;
                    }

                    var dupPlains =
                        (from entry in plainLists
                         from elements in entry.list
                         let dup = XElement.Parse(elements.ToString())
                         select MakeOffset(entry.parent, dup, xoffset, yoffset, count)).
                        ToArray();

                    var dupElements =
                        (from entry in elementLists
                         from element in entry.list
                         let dup = XElement.Parse(element.ToString())
                         select MakeOffset(entry.parent, dup, xoffset, yoffset, count)).
                        ToArray();

                    var dupSignals =
                        (from entry in signalLists
                         from element in entry.list
                         let dup = XElement.Parse(element.ToString())
                         select MakeOffset(entry.parent, dup, xoffset, yoffset, count)).
                        ToArray();

                    Console.WriteLine($"Dup[{count}]: ({xoffset}, {yoffset})");
                    count++;

                    width = Math.Max(width, xoffset);
                    height = Math.Max(height, yoffset);
                }
            }

            Console.WriteLine($"Totally panelized: Count={count}, Size=({width}, {height}), ({minX}, {minY}) - ({minX + width}, {minY + height})");

            using (var stream = File.OpenWrite(panelizedPath))
            {
                document.Save(stream);
                stream.Flush();
            }

            return 0;
        }
    }
}
