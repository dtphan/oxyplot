﻿namespace OxyPlot.Series
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using Axes;
    using OxyPlot;

    /// <summary>
    /// Implements a polar heat map series.
    /// </summary>
    public class PolarHeatMapSeries : XYAxisSeries
    {
        /// <summary>
        /// The image
        /// </summary>
        private OxyImage image;

        /// <summary>
        /// Gets or sets a rotation angle in degree to rotate the image.
        /// </summary>
        public double RotationAngle { get; set; }

        /// <summary>
        /// The pixels
        /// </summary>
        private OxyColor[,] pixels;

        /// <summary>
        /// Initializes a new instance of the <see cref="PolarHeatMapSeries" /> class.
        /// </summary>
        public PolarHeatMapSeries()
        {
            this.Interpolate = true;
            this.TrackerFormatString = "{0}\n{1}: {2}\n{3}: {4}\n{5}: {6}";
        }

        /// <summary>
        /// Gets or sets the size of the image - if set to 0, the image will be generated at every update.
        /// </summary>
        /// <value>The size of the image.</value>
        public int ImageSize { get; set; }

        /// <summary>
        /// Gets or sets the x-coordinate of the left column mid point. Unit: deg.
        /// </summary>
        public double Angle0 { get; set; }

        /// <summary>
        /// Gets or sets the x-coordinate of the right column mid point. Unit: deg.
        /// </summary>
        public double Angle1 { get; set; }

        /// <summary>
        /// Gets or sets the y-coordinate of the top row mid point.
        /// </summary>
        public double Magnitude0 { get; set; }

        /// <summary>
        /// Gets or sets the y-coordinate of the bottom row mid point.
        /// </summary>
        public double Magnitude1 { get; set; }

        /// <summary>
        /// Gets or sets the data array.
        /// </summary>
        /// <remarks>Note that the indices of the data array refer to [Magnitude,Angle].</remarks>
        public double[,] Data { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to interpolate when rendering.
        /// </summary>
        /// <remarks>This property is not supported on all platforms.</remarks>
        public bool Interpolate { get; set; }

        /// <summary>
        /// Gets or sets the minimum value of the dataset.
        /// </summary>
        public double MinValue { get; protected set; }

        /// <summary>
        /// Gets or sets the maximum value of the dataset.
        /// </summary>
        public double MaxValue { get; protected set; }

        /// <summary>
        /// Gets or sets the color axis.
        /// </summary>
        /// <value>The color axis.</value>
        public IColorAxis ColorAxis { get; protected set; }

        /// <summary>
        /// Gets or sets the color axis key.
        /// </summary>
        /// <value>The color axis key.</value>
        public string ColorAxisKey { get; set; }

        /// <summary>
        /// Renders the series on the specified render context.
        /// </summary>
        /// <param name="rc">The rendering context.</param>
        public override void Render(IRenderContext rc)
        {
            if (this.Data == null)
            {
                this.image = null;
                return;
            }

            if (this.ImageSize > 0)
            {
                this.RenderFixed(rc, this.PlotModel);
            }
            else
            {
                this.RenderDynamic(rc, this.PlotModel);
            }
        }

        /// <summary>
        /// Renders by an image sized from the available plot area.
        /// </summary>
        /// <param name="rc">The rc.</param>
        /// <param name="model">The model.</param>
        public void RenderDynamic(IRenderContext rc, PlotModel model)
        {
            int m = this.Data.GetLength(1);
            int n = this.Data.GetLength(0);

            // get the available plot area
            var dest = model.PlotArea;
            int width = (int)dest.Width;
            int height = (int)dest.Height;
            if (width == 0 || height == 0)
            {
                return;
            }

            if (this.pixels == null || this.pixels.GetLength(0) != width || this.pixels.GetLength(1) != height)
            {
                this.pixels = new OxyColor[width, height];
            }

            var p = this.pixels;
            for (int y = 0; y < height; y++)
            //Parallel.For(0, height, (y) =>
            {
                for (int x = 0; x < width; x++)
                {
                    // transform from screen to magnitude/angle
                    var sp = new ScreenPoint(dest.Left + x, dest.Top + y);
                    var xy = this.InverseTransform(sp);
                    double angle;
                    double magnitude;
                    if (this.PlotModel.PlotType != PlotType.Polar)
                    {
                        angle = Math.Atan2(xy.Y, xy.X) / Math.PI * 180;
                        magnitude = Math.Sqrt((xy.X * xy.X) + (xy.Y * xy.Y));
                    }
                    else
                    {
                        angle = xy.Y;// / Math.PI * 180;
                        magnitude = xy.X;
                        if (angle < 0)
                        {
                            angle += 360;
                        }
                    }
                    angle -= this.RotationAngle;
                    if (angle < 0)
                    {
                        angle += 360;
                    }
                    //if (angle < this.Angle0)
                    //{
                    //    angle = this.Angle1 - this.Angle0 + angle;
                    //}
                    // transform to indices in the Data array
                    var jj = (angle - this.Angle0) / (this.Angle1 - this.Angle0) * m;
                    var ii = (magnitude - this.Magnitude0) / (this.Magnitude1 - this.Magnitude0) * n;
                    if (ii >= 0 && ii < n && jj >= 0 && jj < m)
                    {
                        // get the (interpolated) value
                        var value = this.GetValue(ii, jj);

                        // use the color axis to get the color
                        p[x, y] = OxyColor.FromAColor(210, this.ColorAxis.GetColor(value));
                    }
                    else
                    {
                        // outside the range of the Data array
                        p[x, y] = OxyColors.Transparent;
                    }
                }
                //});
            }

            // Create the PNG image
            this.image = OxyImage.Create(p, ImageFormat.Png);

            // Render the image
            var clip = this.GetClippingRect();
            rc.DrawImage(this.image, dest.Left, dest.Top, dest.Width, dest.Height, 1.0, false);
            //rc.DrawClippedImage(clip, this.image, dest.Left, dest.Top, dest.Width, dest.Height, 1, false);
        }

        /// <summary>
        /// Refreshes the image next time the series is rendered.
        /// </summary>
        public void Refresh()
        {
            this.image = null;
        }

        /// <summary>
        /// Renders by scaling a fixed image.
        /// </summary>
        /// <param name="rc">The render context.</param>
        /// <param name="model">The model.</param>
        public void RenderFixed(IRenderContext rc, PlotModel model)
        {
            if (this.image == null)
            {
                int m = this.Data.GetLength(1);
                int n = this.Data.GetLength(0);

                int width = this.ImageSize;
                int height = this.ImageSize;
                if (this.pixels == null || this.pixels.GetLength(0) != width || this.pixels.GetLength(1) != height)
                {
                    this.pixels = new OxyColor[width, height];
                }

                var p = this.pixels;
                for (int yi = 0; yi < height; yi++)
                //Parallel.For(0, height, (yi) =>
                {
                    for (int xi = 0; xi < width; xi++)
                    {
                        double x = (xi - width * 0.5) / (width * 0.5) * this.Magnitude1;
                        double y = -(yi - height * 0.5) / (height * 0.5) * this.Magnitude1;

                        double angle = Math.Atan2(y, x) / Math.PI * 180;
                        double magnitude = Math.Sqrt(x * x + y * y);

                        angle -= this.RotationAngle;
                        if (angle < 0)
                        {
                            angle += 360;
                        }
                        //if (angle < this.Angle0)
                        //{
                        //    angle = this.Angle1 - this.Angle0 + angle;
                        //}
                        // transform to indices in the Data array
                        var jj = (angle - this.Angle0) / (this.Angle1 - this.Angle0) * m;
                        var ii = (magnitude - this.Magnitude0) / (this.Magnitude1 - this.Magnitude0) * n;
                        if (ii >= 0 && ii < n && jj >= 0 && jj < m)
                        {
                            // get the (interpolated) value
                            var value = this.GetValue(ii, jj);

                            // use the color axis to get the color
                            p[xi, yi] = OxyColor.FromAColor(210, this.ColorAxis.GetColor(value));
                        }
                        else
                        {
                            // outside the range of the Data array
                            p[xi, yi] = OxyColors.Transparent;
                        }
                    }
                }//});

                // Create the PNG image
                this.image = OxyImage.Create(p, ImageFormat.Png);
            }

            OxyRect dest;
            if (this.PlotModel.PlotType != PlotType.Polar)
            {
                var topleft = this.Transform(-this.Magnitude1, this.Magnitude1);
                var bottomright = this.Transform(this.Magnitude1, -this.Magnitude1);
                dest = new OxyRect(topleft.X, topleft.Y, bottomright.X - topleft.X, bottomright.Y - topleft.Y);
            }
            else
            {
                var top = this.Transform(this.Magnitude1, 90);
                var bottom = this.Transform(this.Magnitude1, 270);
                var left = this.Transform(this.Magnitude1, 180);
                var right = this.Transform(this.Magnitude1, 0);
                dest = new OxyRect(left.X, top.Y, right.X - left.X, bottom.Y - top.Y);
            }

            // Render the image
            var clip = this.GetClippingRect();
            rc.DrawImage(this.image, dest.Left, dest.Top, dest.Width, dest.Height, 1, false);
            //rc.DrawClippedImage(clip, this.image, dest.Left, dest.Top, dest.Width, dest.Height, 1, false);
        }

        /// <summary>
        /// Gets the value at the specified data indices.
        /// </summary>
        /// <param name="ii">The magnitude index in the Data array.</param>
        /// <param name="jj">The angle index in the Data array.</param>
        /// <returns>The value.</returns>
        protected virtual double GetValue(double ii, double jj)
        {
            if (!this.Interpolate || (this.MaxValue - this.MinValue < 1E-30))
            {
                var i = (int)Math.Floor(ii);
                var j = (int)Math.Floor(jj);
                return this.Data[i, j];
            }

            ii -= 0.5;
            jj -= 0.5;

            // bi-linear interpolation http://en.wikipedia.org/wiki/Bilinear_interpolation
            var r = (int)Math.Floor(ii);
            var c = (int)Math.Floor(jj);

            int r0 = r > 0 ? r : 0;
            int r1 = r + 1 < this.Data.GetLength(0) ? r + 1 : r;
            int c0 = c > 0 ? c : 0;
            int c1 = c + 1 < this.Data.GetLength(1) ? c + 1 : c;

            double v00 = this.Data[r0, c0];
            double v01 = this.Data[r0, c1];
            double v10 = this.Data[r1, c0];
            double v11 = this.Data[r1, c1];

            double di = ii - r;
            double dj = jj - c;

            double v0 = (v00 * (1 - dj)) + (v01 * dj);
            double v1 = (v10 * (1 - dj)) + (v11 * dj);

            return (v0 * (1 - di)) + (v1 * di);
        }

        /// <summary>
        /// Gets the point on the series that is nearest the specified point.
        /// </summary>
        /// <param name="point">The point.</param>
        /// <param name="interpolate">Interpolate the series if this flag is set to <c>true</c>.</param>
        /// <returns>A TrackerHitResult for the current hit.</returns>
        public override TrackerHitResult GetNearestPoint(ScreenPoint point, bool interpolate)
        {
            var xy = this.InverseTransform(point);
            double angle;
            double magnitude;
            if (this.PlotModel.PlotType != PlotType.Polar)
            {
                angle = Math.Atan2(xy.Y, xy.X) / Math.PI * 180;
                magnitude = Math.Sqrt((xy.X * xy.X) + (xy.Y * xy.Y));
            }
            else
            {
                angle = xy.Y; // / Math.PI * 180;
                magnitude = xy.X;
                if (angle < 0)
                {
                    angle += 360;
                }
            }

            var angleInData = angle - this.RotationAngle;
            if (angleInData < 0)
            {
                angleInData += 360;
            }

            //if (angle < this.Angle0)
            //{
            //    angle = this.Angle1 - this.Angle0 + angle;
            //}

            var m = this.Data.GetLength(1);
            var n = this.Data.GetLength(0);
            // transform to indices in the Data array
            var jj = (angleInData - this.Angle0) / (this.Angle1 - this.Angle0) * m;
            var ii = (magnitude - this.Magnitude0) / (this.Magnitude1 - this.Magnitude0) * n;
            if (ii >= 0 && ii < n && jj >= 0 && jj < m)
            {
                // get the (interpolated) value
                var value = this.GetValue(ii, jj);
                return new TrackerHitResult()
                {
                    Position = point,
                    Item = new Tuple<double, double, double>(magnitude, angle, value),
                    Text = string.Format(this.TrackerFormatString, this.Title, this.XAxis.Title, magnitude, this.YAxis.Title, angle, ((Axis)this.ColorAxis).Title, value)
                };
            }
            return null;
        }

        /// <summary>
        /// Ensures that the axes of the series is defined.
        /// </summary>
        protected internal override void EnsureAxes()
        {
            base.EnsureAxes();

            this.ColorAxis = this.PlotModel.GetAxisOrDefault(this.ColorAxisKey, (Axis)this.PlotModel.DefaultColorAxis) as IColorAxis;
        }

        /// <summary>
        /// Updates the maximum and minimum values of the series.
        /// </summary>
        protected internal override void UpdateMaxMin()
        {
            base.UpdateMaxMin();

            this.MinValue = this.GetData().Min();
            this.MaxValue = this.GetData().Max();

            //this.XAxis.Include(this.MinX);
            //this.XAxis.Include(this.MaxX);

            //this.YAxis.Include(this.MinY);
            //this.YAxis.Include(this.MaxY);

            var colorAxis = this.ColorAxis as Axis;
            if (colorAxis != null)
            {
                colorAxis.Include(this.MinValue);
                colorAxis.Include(this.MaxValue);
            }
        }

        /// <summary> 
        /// Transforms from a screen point to a data point by the axes of this series. 
        /// </summary> 
        /// <param name="p"> 
        /// The screen point. 
        /// </param> 
        /// <returns> 
        /// A data point. 
        /// </returns> 
        public new DataPoint InverseTransform(ScreenPoint p)
        {
            return this.XAxis.InverseTransform(p.X, p.Y, this.YAxis);
        }


        /// <summary>
        /// Gets the data as a sequence (LINQ-friendly).
        /// </summary>
        /// <returns>The sequence of data.</returns>
        protected IEnumerable<double> GetData()
        {
            int m = this.Data.GetLength(0);
            int n = this.Data.GetLength(1);
            for (int i = 0; i < m; i++)
            {
                for (int j = 0; j < n; j++)
                {
                    yield return this.Data[i, j];
                }
            }
        }

        /// <summary>
        /// Cleans up arrays.
        /// </summary>
        public void Dispose()
        {
            this.Data = null;
            this.image = null;
            this.pixels = null;
        }
    }
}
