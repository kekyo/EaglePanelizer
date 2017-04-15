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
using System.Xml.Linq;

namespace EaglePanelizer
{
    // Unit calculation class.
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
}
