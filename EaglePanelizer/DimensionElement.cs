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

using System.Xml.Linq;

namespace EaglePanelizer
{
    internal struct DimensionElement
    {
        public readonly double X1;
        public readonly double Y1;
        public readonly double X2;
        public readonly double Y2;
        public readonly int? Layer;
        public readonly XElement Element;

        private DimensionElement(double x1, double y1, double x2, double y2, int? layer, XElement element)
        {
            this.X1 = x1;
            this.Y1 = y1;
            this.X2 = x2;
            this.Y2 = y2;
            this.Layer = layer;
            this.Element = element;
        }

        public DimensionElement? TryOffsetOf(XElement element, Unit unit)
        {
            var x = unit.Value(element, "x");
            var y = unit.Value(element, "y");

            if (x.HasValue && y.HasValue)
            {
                return new DimensionElement(
                    this.X1 + x.Value, this.Y1 + y.Value, this.X2 + x.Value, this.Y2 + y.Value,
                    this.Layer, this.Element);
            }
            else
            {
                return null;
            }
        }

        public static DimensionElement? TryCreateFrom(XElement element, Unit unit)
        {
            var x = unit.Value(element, "x");
            var y = unit.Value(element, "y");
            var x1 = unit.Value(element, "x1");
            var y1 = unit.Value(element, "y1");
            var x2 = unit.Value(element, "x2");
            var y2 = unit.Value(element, "y2");
            var layer = (int?)element.Attribute("layer");

            if (x.HasValue && y.HasValue)
            {
                return new DimensionElement(
                    x.Value, y.Value, x.Value, y.Value, layer, element);
            }
            else if (x1.HasValue && y1.HasValue && x2.HasValue && y2.HasValue)
            {
                return new DimensionElement(
                    x1.Value, y1.Value, x2.Value, y2.Value, layer, element);
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
            return string.Format("({0},{1})-({2},{3})", this.X1, this.Y1, this.X2, this.Y2);
        }
    }
}
