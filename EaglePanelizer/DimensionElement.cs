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
using System.Xml.Linq;

namespace EaglePanelizer
{
    internal struct DimensionElement
    {
        private readonly Unit unit;

        public readonly XElement Board;
        public readonly XElement Element;
        public readonly XElement Original;
        public readonly XElement OffsetOf;

        private DimensionElement(XElement board, XElement element, XElement original, XElement offsetOf, Unit unit)
        {
            this.unit = unit;
            this.Board = board;
            this.Element = element;
            this.Original = original;
            this.OffsetOf = offsetOf;
        }

        public string Name => (string)this.Element.Attribute("name") ?? this.Element.Name.ToString();
        public string ParentName => (string)this.Element.Parent?.Attribute("name") ?? this.Element.Parent?.Name.ToString() ?? "unknown";

        public double X1 => unit.Value(this.Element.Attribute("x1")) ?? unit.Value(this.Element.Attribute("x")).Value;
        public double Y1 => unit.Value(this.Element.Attribute("y1")) ?? unit.Value(this.Element.Attribute("y")).Value;
        public double X2 => unit.Value(this.Element.Attribute("x2")) ?? unit.Value(this.Element.Attribute("x")).Value;
        public double Y2 => unit.Value(this.Element.Attribute("y2")) ?? unit.Value(this.Element.Attribute("y")).Value;

        public int? Layer => (int?)this.Element.Attribute("layer");

        public DimensionElement? TryOffsetOfOrigin(XElement offsetOf, bool positive)
        {
            var ox = unit.Value(offsetOf, "x");
            var oy = unit.Value(offsetOf, "y");

            if (!ox.HasValue || !oy.HasValue)
            {
                return null;
            }

            var x1 = unit.Value(this.Original.Attribute("x1"));
            var y1 = unit.Value(this.Original.Attribute("y1"));
            var x2 = unit.Value(this.Original.Attribute("x2"));
            var y2 = unit.Value(this.Original.Attribute("y2"));

            var op = positive ? 1 : -1;

            if (x1.HasValue && y1.HasValue && x2.HasValue && y2.HasValue)
            {
                var newElement = new XElement(this.Original);

                newElement.Attribute("x1").Value = (x1.Value + ox * op).ToString();
                newElement.Attribute("y1").Value = (y1.Value + oy * op).ToString();
                newElement.Attribute("x2").Value = (x2.Value + ox * op).ToString();
                newElement.Attribute("y2").Value = (y2.Value + oy * op).ToString();

                return new DimensionElement(this.Board, newElement, this.Original, offsetOf, unit);
            }

            var x = unit.Value(this.Original.Attribute("x"));
            var y = unit.Value(this.Original.Attribute("y"));

            if (x.HasValue && y.HasValue)
            {
                var newElement = new XElement(this.Original);

                newElement.Attribute("x").Value = (x.Value + ox * op).ToString();
                newElement.Attribute("y").Value = (y.Value + oy * op).ToString();

                return new DimensionElement(this.Board, newElement, this.Original, offsetOf, unit);
            }
            else
            {
                return null;
            }
        }

        public static DimensionElement? TryCreateFrom(XElement board, XElement element, Unit unit)
        {
            var x1 = unit.Value(element.Attribute("x1"));
            var y1 = unit.Value(element.Attribute("y1"));
            var x2 = unit.Value(element.Attribute("x2"));
            var y2 = unit.Value(element.Attribute("y2"));

            if (x1.HasValue && y1.HasValue && x2.HasValue && y2.HasValue)
            {
                return new DimensionElement(board, element, element, null, unit);
            }

            var x = unit.Value(element.Attribute("x"));
            var y = unit.Value(element.Attribute("y"));

            if (x.HasValue && y.HasValue)
            {
                return new DimensionElement(board, element, element, null, unit);
            }
            else
            {
                return null;
            }
        }

        public double MinX()
        {
            return (this.X1 <= this.X2) ? this.X1 : this.X2;
        }

        public double MinY()
        {
            return (this.Y1 <= this.Y2) ? this.Y1 : this.Y2;
        }

        public double MaxX()
        {
            return (this.X1 >= this.X2) ? this.X1 : this.X2;
        }

        public double MaxY()
        {
            return (this.Y1 >= this.Y2) ? this.Y1 : this.Y2;
        }

        public override string ToString()
        {
            return string.Format("{0}/{1}{2}: ({3},{4})-({5},{6})",
                this.ParentName, this.Name, this.Layer.HasValue ? $"[{this.Layer}]" : string.Empty,
                this.X1, this.Y1, this.X2, this.Y2);
        }
    }
}
