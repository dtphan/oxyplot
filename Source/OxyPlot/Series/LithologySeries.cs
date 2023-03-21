using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OxyPlot.Series
{
    /// <summary>
    /// Represents a lithology item.
    /// </summary>
    public class LithologyItem
    {
        /// <summary>
        /// Gets or sets a number to distinguish between lithology items.
        /// </summary>
        public int ID { get; set; }

        /// <summary>
        /// Gets or sets the color to visualize the lithology item.
        /// </summary>
        public OxyColor Color { get; set; }

        /// <summary>
        /// Gets or sets the name of the lithology item.
        /// </summary>
        public string Name { get; set; }
    }

    /// <summary>
    /// Represents a series for lithology column.
    /// </summary>
    public class LithologySeries : DataPointSeries
    {

        ///// <summary>
        ///// Gets or sets the data array containing the depth (y) and the correspondnig ID (x) of the lithologies. Lithology ID will be rounded down to the integer less than or equal to the given number.
        ///// </summary>
        //public DataPoint[] Data { get; set; }

        /// <summary>
        /// Gets or sets a list of distinct lithology items.
        /// </summary>
        public List<LithologyItem> Lithologies { get; } = new List<LithologyItem>();

        /// <summary>
        /// Gets or sets a value indicating that a color at a given y value should be copied from the nearest data point.
        /// If false, it will get the color of the point at that depth or before it.
        /// </summary>
        public bool NearestNeighbor { get; set; } = false;

        /// <summary>
        /// The rendered image of the lithology series.
        /// </summary>
        private OxyImage image;

        /// <summary>
        /// The minimum interval in the y direction between any two consecutive points.
        /// </summary>
        private double MinDeltaY;

        /// <summary>
        /// The hash code of the data when the image was last updated.
        /// </summary>
        private int DataHash;

        /// <summary>
        /// The hash code of the lithology items when the image was last updated.
        /// </summary>
        private int ColorHash;


        /// <summary>
        /// Invalidates the image that renders the heat map. The image will be regenerated the next time the <see cref="HeatMapSeries" /> is rendered.
        /// </summary>
        /// <remarks>Call <see cref="PlotModel.InvalidatePlot" /> to refresh the view.</remarks>
        public void Invalidate()
        {
            this.image = null;
        }

        /// <summary>
        /// Transforms data space coordinates to orientated screen space coordinates.
        /// </summary>
        /// <param name="x">The x coordinate.</param>
        /// <param name="y">The y coordinate.</param>
        /// <returns>The transformed point.</returns>
        public ScreenPoint Transform(double x, double y)
        {
            return this.Orientate(base.Transform(new DataPoint(x, y)));
        }

        /// <summary>
        /// Transforms data space coordinates to orientated screen space coordinates.
        /// </summary>
        /// <param name="point">The point to transform.</param>
        /// <returns>The transformed point.</returns>
        public new ScreenPoint Transform(DataPoint point)
        {
            return this.Orientate(base.Transform(point));
        }

        /// <summary>
        /// Gets the clipping rectangle, transposed if the X axis is vertically orientated.
        /// </summary>
        /// <returns>The clipping rectangle.</returns>
        protected new OxyRect GetClippingRect()
        {
            double minX = Math.Min(this.XAxis.ScreenMin.X, this.XAxis.ScreenMax.X);
            double minY = Math.Min(this.YAxis.ScreenMin.Y, this.YAxis.ScreenMax.Y);
            double maxX = Math.Max(this.XAxis.ScreenMin.X, this.XAxis.ScreenMax.X);
            double maxY = Math.Max(this.YAxis.ScreenMin.Y, this.YAxis.ScreenMax.Y);

            if (this.XAxis.IsVertical())
            {
                return new OxyRect(minY, minX, maxY - minY, maxX - minX);
            }

            return new OxyRect(minX, minY, maxX - minX, maxY - minY);
        }

        /// <summary>
        /// Transposes the ScreenPoint if the X axis is vertically orientated
        /// </summary>
        /// <param name="point">The <see cref="ScreenPoint" /> to orientate.</param>
        /// <returns>The oriented point.</returns>
        private ScreenPoint Orientate(ScreenPoint point)
        {
            if (this.XAxis.IsVertical())
            {
                point = new ScreenPoint(point.Y, point.X);
            }

            return point;
        }

        /// <summary>
        /// Tests if a <see cref="DataPoint" /> is inside the heat map
        /// </summary>
        /// <param name="p">The <see cref="DataPoint" /> to test.</param>
        /// <returns><c>True</c> if the point is inside the heat map.</returns>
        private bool IsPointInRange(DataPoint p)
        {
            this.UpdateMaxMin();
            return p.X >= this.MinX && p.X <= this.MaxX && p.Y >= this.MinY && p.Y <= this.MaxY;
        }

        /// <inheritdoc/>
        protected internal override void UpdateMaxMin()
        {
            base.UpdateMaxMin();
            if (this.MinX == this.MaxX)
            {
                this.MaxX = this.MinX + 1;
            }
            this.XAxis.Minimum = this.MinX;
            this.XAxis.Maximum = this.MaxX;
            this.MinDeltaY = double.MaxValue;
            for (int i = 1; i < this.ActualPoints.Count; i++)
            {
                var deltaY = Math.Abs(this.ActualPoints[i].Y - this.ActualPoints[i - 1].Y);
                if (this.MinDeltaY > deltaY && deltaY > 0)
                {
                    this.MinDeltaY = deltaY;
                }
            }
        }

        /// <summary>
        /// Transforms orientated screen space coordinates to data space coordinates.
        /// </summary>
        /// <param name="point">The point to inverse transform.</param>
        /// <returns>The inverse transformed point.</returns>
        public new DataPoint InverseTransform(ScreenPoint point)
        {
            return base.InverseTransform(this.Orientate(point));
        }

        private int GetColorHashCode()
        {
            double sum = this.Lithologies.Sum(li => (double)li.Color.GetHashCode());
            return sum.GetHashCode();
        }

        private int GetDataHashCode()
        {
            double sum = this.ActualPoints.Sum(dp => (double)dp.GetHashCode());
            return sum.GetHashCode();
        }

        /// <summary>
        /// Renders the series on the specified render context.
        /// </summary>
        /// <param name="rc">The rendering context.</param>
        public override void Render(IRenderContext rc)
        {
            if (this.ActualPoints == null || this.ActualPoints.Count == 0)
            {
                this.image = null;
                return;
            }

            int currentDataHash = this.GetDataHashCode();
            int currentColorHash = this.GetColorHashCode();

            if (this.image == null || currentDataHash != this.DataHash || currentColorHash != this.ColorHash)
            {
                this.UpdateImage();
                this.DataHash = this.GetDataHashCode();
                this.ColorHash = this.GetColorHashCode();
            }

            double left = this.MinX;
            double right = this.MaxX;
            double bottom = this.MinY;
            double top = this.MaxY;
            var s00 = this.Transform(left, bottom);
            var s11 = this.Transform(right, top);
            var rect = new OxyRect(s00, s11);

            var clip = this.GetClippingRect();

            if (this.image != null)
            {
                rc.DrawImage(this.image, rect.Left, rect.Top, rect.Width, rect.Height, 1, false);
            }
        }

        /// <summary>
        /// Updates the image.
        /// </summary>
        private void UpdateImage()
        {
            // The image has a height of h pixels
            // The first (top) pixel corresponds to the YMin
            // The last (bottom) pixel corresponds to the YMax
            // Determine h to cover all the points in ActualPoints
            // If all the points are evenly spaced in the y-direction, then 
            // h = number of points
            // If they are not evenly spaced, we find the smallest y-interval dy
            // between two consecutive points, then estimate h = Ceiling((YMax - YMin)/dy)

            int h = (int)Math.Ceiling((this.MaxY - this.MinY) / this.MinDeltaY); // this.ActualPoints.Count;
            if (h > 2160) h = 2160;
            // Each pixel height is equivalent to
            double pxHeight = (this.MaxY - this.MinY) / h; // in y-axis

            int n = 1;
            var buffer = new OxyColor[n, h + 1];
            int i = 0, j = 0;
            if (!this.NearestNeighbor)
            {
                while (j < this.ActualPoints.Count)
                {
                    DataPoint p1 = this.ActualPoints[j];
                    DataPoint p2 = p1;
                    while (j + 1 < this.ActualPoints.Count)
                    {
                        DataPoint p3 = this.ActualPoints[j + 1];
                        if (double.IsNaN(p2.X) && double.IsNaN(p3.X) || p2.X == p3.X)
                        {
                            j++;
                        }
                        else
                        {
                            p2 = p3;
                            break;
                        }
                    }
                    j++;
                    int lithologyID = double.IsNaN(p1.X) ? int.MinValue : (int)Math.Floor(p1.X);
                    OxyColor color = this.GetColor(lithologyID);
                    double dataY = this.MinY + i * pxHeight;
                    while (dataY < p2.Y && i < h + 1)
                    {
                        buffer[0, i++] = color;
                        dataY = this.MinY + i * pxHeight;
                    }
                    if (j >= this.ActualPoints.Count)
                    {
                        while (i < h + 1)
                        {
                            buffer[0, i++] = color;
                        }
                    }
                }
            }

            this.image = OxyImage.Create(buffer, ImageFormat.Png);
        }

        /// <summary>
        /// Gets the color at dataY.
        /// </summary>
        /// <param name="dataY">The y value to get color.</param>
        /// <returns>The color at the specified y value.</returns>
        private OxyColor GetColor(double dataY)
        {
            // Find the data point immediately before dataY.
            var p1 = this.ActualPoints.LastOrDefault(ap => ap.Y <= dataY);
            DataPoint p = p1;
            if (this.NearestNeighbor)
            {
                // Find the data point immediately after dataY.
                var p2 = this.ActualPoints.FirstOrDefault(ap => ap.Y >= dataY);
                // See whether dp is closer to p1 or p2
                if (p2.Y - dataY < dataY - p1.Y)
                {
                    p = p2;
                }
            }
            int lithologyID = double.IsNaN(p.X) ? int.MinValue : (int)Math.Floor(p.X);
            return this.GetColor(lithologyID);
        }

        /// <summary>
        /// Gets the color of a lithology.
        /// </summary>
        /// <param name="lithologyID">The ID of the lithology.</param>
        /// <returns>The color of the specified lithology ID.</returns>
        private OxyColor GetColor(int lithologyID)
        {
            if (lithologyID < 0)
            {
                return OxyColors.Transparent;
            }
            LithologyItem item = this.Lithologies.FirstOrDefault(l => l.ID == lithologyID);
            if (item == null)
            {
                var color = this.PlotModel.GetDefaultColor();
                item = new LithologyItem { ID = lithologyID, Color = color };
                this.Lithologies.Add(item);
            }
            if (item.Color == OxyColors.Automatic)
            {
                item.Color = this.PlotModel.GetDefaultColor();
            }
            return item.Color;
        }

        /// <summary>
        /// Gets the point on the series that is nearest the specified point.
        /// </summary>
        /// <param name="point">The point.</param>
        /// <param name="interpolate">Interpolate the series if this flag is set to <c>true</c>.</param>
        /// <returns>A TrackerHitResult for the current hit.</returns>
        public override TrackerHitResult GetNearestPoint(ScreenPoint point, bool interpolate)
        {
            var dp = this.InverseTransform(point);
            if (!this.IsPointInRange(dp))
            {
                return null;
            }
            // Find two points y1, y2 that satisfy y1 <= dp.y <= y2
            var p1 = this.ActualPoints.LastOrDefault(ap => ap.Y <= dp.Y);
            var p2 = this.ActualPoints.FirstOrDefault(ap => ap.Y >= dp.Y);
            // See whether dp is closer to p1 or p2
            DataPoint p = p1;
            if (p2.Y - dp.Y < dp.Y - p1.Y)
            {
                p = p2;
            }
            int itemIndex = this.ActualPoints.IndexOf(p);
            object item = this.GetItem(itemIndex);
            return new TrackerHitResult
            {
                Series = this,
                DataPoint = p,
                Position = point,
                Item = item,
                Index = itemIndex,
                Text = this.GetLithologyName((int)Math.Floor(p.X))
            };
        }

        private string GetLithologyName(int lithologyIndex)
        {
            if (this.Lithologies.Count <= lithologyIndex)
            {
                return $"{lithologyIndex}";
            }
            return this.Lithologies[lithologyIndex].Name;
        }
    }
}
