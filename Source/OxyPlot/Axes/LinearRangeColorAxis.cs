using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OxyPlot.Axes
{
    /// <summary>
    /// Represents a color axis whose range is partitioned into sub-ranges. Each sub-range is associated with a linear color palette.
    /// </summary>
    public class LinearRangeColorAxis : LinearAxis, IColorAxis
    {
        /// <summary>
        /// The ranges
        /// </summary>
        private readonly List<PaletteRange> ranges = new List<PaletteRange>();

        /// <summary>
        /// Initializes a new instance of the <see cref="RangeColorAxis" /> class.
        /// </summary>
        public LinearRangeColorAxis()
        {
            this.Position = AxisPosition.None;
            this.AxisDistance = 20;

            this.LowColor = OxyColors.Undefined;
            this.HighColor = OxyColors.Undefined;
            this.InvalidNumberColor = OxyColors.Gray;

            this.IsPanEnabled = false;
            this.IsZoomEnabled = false;
        }

        /// <summary>
        /// Gets or sets the color used to represent NaN values.
        /// </summary>
        /// <value>A <see cref="OxyColor" /> that defines the color. The default value is <c>OxyColors.Gray</c>.</value>
        public OxyColor InvalidNumberColor { get; set; }

        /// <summary>
        /// Gets or sets the color of values above the maximum value.
        /// </summary>
        /// <value>The color of the high values.</value>
        public OxyColor HighColor { get; set; }

        /// <summary>
        /// Gets or sets the color of values below the minimum value.
        /// </summary>
        /// <value>The color of the low values.</value>
        public OxyColor LowColor { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to render the colors as an image.
        /// </summary>
        /// <value><c>true</c> if the rendering should use an image; otherwise, <c>false</c>.</value>
        public bool RenderAsImage { get; set; }

        /// <summary>
        /// Determines whether the axis is used for X/Y values.
        /// </summary>
        /// <returns><c>true</c> if it is an XY axis; otherwise, <c>false</c> .</returns>
        public override bool IsXyAxis()
        {
            return false;
        }

        /// <summary>
        /// Adds a range of data values and the palette to use when a value is within that range. Ranges should be disjoint and added in ascending order.
        /// If there is only one range, specifying any numbers for <paramref name="lowerBound"/> and <paramref name="upperBound"/> is fine. 
        /// They will be replaced with <see cref="Axis.ActualMinimum"/> and <see cref="Axis.ActualMaximum"/>, respectively.
        /// </summary>
        /// <param name="lowerBound">The inclusive lower bound of the data value range.</param>
        /// <param name="upperBound">The exclusive upper bound of the data value range.</param>
        /// <param name="palette">The palette to use when value is within the specified range.</param>
        public void AddRange(double lowerBound, double upperBound, OxyPalette palette)
        {
            this.ranges.Add(new PaletteRange { LowerBound = lowerBound, UpperBound = upperBound, Palette = palette });
        }

        /// <summary>
        /// Clears the ranges.
        /// </summary>
        public void ClearRanges()
        {
            this.ranges.Clear();
        }

        /// <summary>
        /// Gets the palette index of the specified data value.
        /// </summary>
        /// <param name="value">The data value.</param>
        /// <returns>The palette index where the high 16 bits represent the sub-range index and the low 16 bits represent the palette index in the sub-range.</returns>
        /// <remarks>If the value is less than minimum, 0 is returned. If the value is greater than maximum, int.MaxValue is returned.</remarks>
        public int GetPaletteIndex(double value)
        {
            if (!this.LowColor.IsUndefined() && value < this.ranges[0].LowerBound)
            {
                return -1;
            }

            if (!this.HighColor.IsUndefined() && value > this.ranges[this.ranges.Count - 1].UpperBound)
            {
                return int.MaxValue;
            }
            if (this.ranges.Count == 1)
            {
                this.ranges[0].LowerBound = this.ActualMinimum;
                this.ranges[0].UpperBound = this.ActualMaximum;
            }
            // TODO: change to binary search?
            for (int i = 0; i < this.ranges.Count; i++)
            {
                var range = this.ranges[i];
                if (range.LowerBound <= value && range.UpperBound > value)
                {
                    return (i << 16) | range.GetPaletteIndex(value);
                }
            }

            return int.MinValue;
        }

        /// <summary>
        /// Gets the color.
        /// </summary>
        /// <param name="paletteIndex">The color map index.</param>
        /// <returns>The color.</returns>
        public OxyColor GetColor(int paletteIndex)
        {
            if (paletteIndex == int.MinValue)
            {
                return this.InvalidNumberColor;
            }

            if (paletteIndex == -1)
            {
                return this.LowColor;
            }

            if (paletteIndex == int.MaxValue)
            {
                return this.HighColor;
            }
            int subRangeIndex = (paletteIndex >> 16) & 0xFFFF;
            int subRangePaletteIndex = paletteIndex & 0xFFFF;
            //if (subRangeIndex < 0 || subRangeIndex >= ranges.Count ||
            //    subRangePaletteIndex < 0 || subRangePaletteIndex > ranges[subRangeIndex].Palette.Colors.Count)
            //{
            //    System.Diagnostics.Debug.WriteLine("ERROR: OxyPlot.LinearRangeColorAxis: rangeIndex = {0}, paletteIndex = {1}", subRangeIndex, subRangePaletteIndex);
            //}
            return this.ranges[subRangeIndex].Palette.Colors[subRangePaletteIndex - 1];
        }

        /// <summary>
        /// Renders the axis on the specified render context.
        /// </summary>
        /// <param name="rc">The render context.</param>
        /// <param name="pass">The render pass.</param>
        public override void Render(IRenderContext rc, int pass)
        {
            if (this.Position == AxisPosition.None)
            {
                return;
            }

            if (pass == 0)
            {
                double distance = this.AxisDistance;
                double left = this.PlotModel.PlotArea.Left;
                double top = this.PlotModel.PlotArea.Top;
                double width = this.MajorTickSize - 2;
                double height = this.MajorTickSize - 2;

                const int TierShift = 0;

                switch (this.Position)
                {
                    case AxisPosition.Left:
                        left = this.PlotModel.PlotArea.Left - TierShift - width - distance;
                        top = this.PlotModel.PlotArea.Top;
                        break;
                    case AxisPosition.Right:
                        left = this.PlotModel.PlotArea.Right + TierShift + distance;
                        top = this.PlotModel.PlotArea.Top;
                        break;
                    case AxisPosition.Top:
                        left = this.PlotModel.PlotArea.Left;
                        top = this.PlotModel.PlotArea.Top - TierShift - height - distance;
                        break;
                    case AxisPosition.Bottom:
                        left = this.PlotModel.PlotArea.Left;
                        top = this.PlotModel.PlotArea.Bottom + TierShift + distance;
                        break;
                }

                Action<double, double, OxyColor> drawColorRect = (ylow, yhigh, color) =>
                {
                    double ymin = Math.Min(ylow, yhigh);
                    double ymax = Math.Max(ylow, yhigh) + 0.5;
                    rc.DrawRectangle(this.IsHorizontal()
                            ? new OxyRect(ymin, top, ymax - ymin, height)
                            : new OxyRect(left, ymin, width, ymax - ymin),
                        color, color, 0.0, EdgeRenderingMode.PreferSharpness);
                };

                // if the axis is reversed then the min and max values need to be swapped.
                double effectiveMaxY = this.Transform(this.IsReversed ? this.ActualMinimum : this.ActualMaximum);
                double effectiveMinY = this.Transform(this.IsReversed ? this.ActualMaximum : this.ActualMinimum);

                if (this.ranges.Count == 1)
                {
                    this.ranges[0].LowerBound = this.ActualMinimum;
                    this.ranges[0].UpperBound = this.ActualMaximum;
                }

                foreach (PaletteRange range in this.ranges)
                {
                    if (this.RenderAsImage)
                    {
                        
                    }
                    else
                    {
                        int n = range.Palette.Colors.Count;
                        for (int i = 0; i < n; i++)
                        {
                            double ylowData = range.GetLowValue(i);
                            double yhighData = range.GetHighValue(i);
                            if (yhighData < this.ActualMinimum || ylowData > this.ActualMaximum)
                            {
                                continue;
                            }
                            if (ylowData < this.ActualMinimum)
                            {
                                ylowData = this.ActualMinimum;
                            }
                            if (yhighData > this.ActualMaximum)
                            {
                                yhighData = this.ActualMaximum;
                            }
                            double ylow = this.Transform(ylowData);
                            double yhigh = this.Transform(yhighData);
                            drawColorRect(ylow, yhigh, range.Palette.Colors[i]);
                        }
                    }
                }

                double highLowLength = 10;
                if (this.IsHorizontal())
                {
                    highLowLength *= -1;
                }

                if (!this.LowColor.IsUndefined())
                {
                    double ylow = this.Transform(this.ActualMinimum);
                    drawColorRect(ylow, ylow + highLowLength, this.LowColor);
                }

                if (!this.HighColor.IsUndefined())
                {
                    double yhigh = this.Transform(this.ActualMaximum);
                    drawColorRect(yhigh, yhigh - highLowLength, this.HighColor);
                }
            }

            var r = new HorizontalAndVerticalAxisRenderer(rc, this.PlotModel);
            r.Render(this, pass);
        }

        /// <summary>
        /// Defines a range.
        /// </summary>
        private class PaletteRange
        {
            /// <summary>
            /// Gets or sets the palette of this range.
            /// </summary>
            public OxyPalette Palette { get; set; }

            /// <summary>
            /// Gets or sets the lower bound of this range.
            /// </summary>
            public double LowerBound { get; set; }

            /// <summary>
            /// Gets or sets the upper bound of this range.
            /// </summary>
            public double UpperBound { get; set; }

            /// <summary>
            /// Gets the palette index of the specified value.
            /// </summary>
            /// <param name="value">The value.</param>
            /// <returns>The palette index.</returns>
            /// <remarks>If the value is less than minimum, 0 is returned. If the value is greater than maximum, Palette.Colors.Count+1 is returned.</remarks>
            public int GetPaletteIndex(double value)
            {

                int index = 1 + (int)((value - this.LowerBound) / (this.UpperBound - this.LowerBound) * this.Palette.Colors.Count);

                if (index < 1)
                {
                    index = 1;
                }

                if (index > this.Palette.Colors.Count)
                {
                    index = this.Palette.Colors.Count;
                }

                return index;
            }

            /// <summary>
            /// Gets the color.
            /// </summary>
            /// <param name="paletteIndex">The color map index (less than NumberOfEntries).</param>
            /// <returns>The color.</returns>
            public OxyColor GetColor(int paletteIndex)
            {
                return this.Palette.Colors[paletteIndex - 1];
            }

            /// <summary>
            /// Gets the high value of the specified palette index.
            /// </summary>
            /// <param name="paletteIndex">Index of the palette.</param>
            /// <returns>The value.</returns>
            public double GetHighValue(int paletteIndex)
            {
                return this.GetLowValue(paletteIndex + 1);
            }

            /// <summary>
            /// Gets the low value of the specified palette index.
            /// </summary>
            /// <param name="paletteIndex">Index of the palette.</param>
            /// <returns>The value.</returns>
            public double GetLowValue(int paletteIndex)
            {
                return ((double)paletteIndex / this.Palette.Colors.Count * (this.UpperBound - this.LowerBound))
                       + this.LowerBound;
            }
        }
    }
}
