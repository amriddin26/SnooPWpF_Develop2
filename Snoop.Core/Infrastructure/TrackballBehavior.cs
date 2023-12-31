// (c) Copyright Cory Plotts.
// This source is subject to the Microsoft Public License (Ms-PL).
// Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
// All other rights reserved.

namespace Snoop.Infrastructure;

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Media3D;

public class TrackballBehavior
{
    public TrackballBehavior(Viewport3D viewport, Point3D lookAtPoint)
    {
        if (viewport is null)
        {
            throw new ArgumentNullException(nameof(viewport));
        }

        this.viewport = viewport;
        this.lookAtPoint = lookAtPoint;

        var projectionCamera = this.viewport.Camera as ProjectionCamera;
        if (projectionCamera is not null)
        {
            var offset = projectionCamera.Position - this.lookAtPoint;
            this.distance = offset.Length;
        }

        viewport.MouseLeftButtonDown += this.Viewport_MouseLeftButtonDown;
        viewport.MouseMove += this.Viewport_MouseMove;
        viewport.MouseLeftButtonUp += this.Viewport_MouseLeftButtonUp;
        viewport.MouseWheel += this.Viewport_MouseWheel;
    }

    public void Reset()
    {
        this.orientation = Quaternion.Identity;
        this.zoom = 1;
        this.UpdateCamera();
    }

    private void Viewport_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        e.MouseDevice.Capture(this.viewport);
        var point = e.MouseDevice.GetPosition(this.viewport);
        this.mouseDirection = GetDirectionFromPoint(point, this.viewport.RenderSize);
        this.isRotating = true;
        e.Handled = true;
    }

    private void Viewport_MouseMove(object sender, MouseEventArgs e)
    {
        if (this.isRotating)
        {
            var point = e.MouseDevice.GetPosition(this.viewport);
            var newMouseDirection = GetDirectionFromPoint(point, this.viewport.RenderSize);
            var q = GetRotationFromStartAndEnd(newMouseDirection, this.mouseDirection, 2);
            this.orientation *= q;
            this.mouseDirection = newMouseDirection;

            this.UpdateCamera();
            e.Handled = true;
        }
    }

    private void Viewport_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        this.isRotating = false;
        e.MouseDevice.Capture(null);
        e.Handled = true;
    }

    private void Viewport_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        // Zoom in or out exponentially.
        this.zoom *= Math.Pow(ZoomFactor, e.Delta / 120.0);
        this.UpdateCamera();
        e.Handled = true;
    }

    private void UpdateCamera()
    {
        var projectionCamera = this.viewport.Camera as ProjectionCamera;
        if (projectionCamera is not null)
        {
            var matrix = Matrix3D.Identity;
            matrix.Rotate(this.orientation);
            projectionCamera.LookDirection = new Vector3D(0, 0, 1) * matrix;
            projectionCamera.UpDirection = new Vector3D(0, -1, 0) * matrix;
            projectionCamera.Position = this.lookAtPoint - (this.distance / this.zoom * projectionCamera.LookDirection);
        }
    }

    private static Vector3D GetDirectionFromPoint(Point point, Size size)
    {
        var rx = size.Width / 2;
        var ry = size.Height / 2;
        var r = Math.Min(rx, ry);
        var dx = (point.X - rx) / r;
        var dy = (point.Y - ry) / r;
        var rSquared = (dx * dx) + (dy * dy);
        if (rSquared <= 1)
        {
            return new Vector3D(dx, dy, -Math.Sqrt(2 - rSquared));
        }
        else
        {
            return new Vector3D(dx, dy, -1 / Math.Sqrt(rSquared));
        }
    }

    private static Quaternion GetRotationFromStartAndEnd(Vector3D start, Vector3D end, double angleMultiplier)
    {
        var factor = start.Length * end.Length;

        if (factor < 1e-6)
        {
            // One or both of the input directions is close to zero in length.
            return Quaternion.Identity;
        }
        else
        {
            // Both input directions have nonzero length.
            var axis = Vector3D.CrossProduct(start, end);
            var dotProduct = Vector3D.DotProduct(start, end) / factor;
            var angle = Math.Acos(dotProduct < -1 ? -1 : dotProduct > 1 ? 1 : dotProduct);

            if (axis.LengthSquared < 1e-12)
            {
                if (dotProduct > 0)
                {
                    // The input directions are parallel, so no rotation is needed.
                    return Quaternion.Identity;
                }
                else
                {
                    // The directions are antiparallel, and therefore a rotation
                    // of 180 degrees about any direction perpendicular to 'start'
                    // (or 'end') will rotate 'start' into 'end'.
                    //
                    // The following construction will guarantee that
                    // dot(axis, start) == 0.
                    axis = Vector3D.CrossProduct(start, new Vector3D(1, 0, 0));
                    if (axis.LengthSquared < 1e-12)
                    {
                        axis = Vector3D.CrossProduct(start, new Vector3D(0, 1, 0));
                    }
                }
            }

            return new Quaternion(axis, angleMultiplier * angle * 180 / Math.PI);
        }
    }

    private readonly Viewport3D viewport;
    private readonly Point3D lookAtPoint;
    private readonly double distance = 1;
    private double zoom = 1;
    private bool isRotating;
    private Vector3D mouseDirection;
    private Quaternion orientation = Quaternion.Identity;

    private const double ZoomFactor = 1.1;
}