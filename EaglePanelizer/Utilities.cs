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

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace EaglePanelizer
{
    internal static class Utilities
    {
        public static XDocument LoadEagleBoard(string path)
        {
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.Read))
            {
                return XDocument.Load(stream);
            }
        }

        public static void SaveEagleBoard(string path, XDocument document)
        {
            using (var stream = new FileStream(path, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
            {
                document.Save(stream);
                stream.Flush();
            }
        }

        public static XElement DupElement(
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

        public static void AddWireElement(this XElement plain,
            double x1, double y1, double x2, double y2, double width, int layer)
        {
            plain.Add(new XElement("wire",
                new XAttribute("x1", x1),
                new XAttribute("y1", y1),
                new XAttribute("x2", x2),
                new XAttribute("y2", y2),
                new XAttribute("width", width),
                new XAttribute("layer", layer)));
        }

        public sealed class ElementSameComparer : IEqualityComparer<XElement>
        {
            private readonly XElement board;
            private readonly Unit unit;

            public ElementSameComparer(XElement board, Unit unit)
            {
                this.board = board;
                this.unit = unit;
            }

            public bool Equals(XElement lhs, XElement rhs)
            {
                var l = DimensionElement.TryCreateFrom(board, lhs, unit);
                var r = DimensionElement.TryCreateFrom(board, rhs, unit);

                if (!l.HasValue || !r.HasValue)
                {
                    return false;
                }

                var h =
                    (l.Value.X1 == r.Value.X1 && l.Value.Y1 == r.Value.Y1 && l.Value.X2 == r.Value.X2 && l.Value.Y2 == r.Value.Y2) ||
                    (l.Value.X1 == r.Value.X2 && l.Value.Y1 == r.Value.Y2 && l.Value.X2 == r.Value.X1 && l.Value.Y2 == r.Value.Y1);
                return h;
            }

            public int GetHashCode(XElement obj)
            {
                var o = DimensionElement.TryCreateFrom(board, obj, unit);

                return
                    (o?.X1.GetHashCode() ?? 0) ^
                    (o?.Y1.GetHashCode() ?? 0) ^
                    (o?.X2.GetHashCode() ?? 0) ^
                    (o?.Y2.GetHashCode() ?? 0);
            }
        }
    }
}
