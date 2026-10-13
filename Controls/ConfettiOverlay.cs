using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;

namespace TodoListApp.Linux.Controls
{
    public class ConfettiOverlay : Control
    {
        private static readonly string[] ConfettiColorHexes =
        {
            "#4F46E5", "#10B981", "#F59E0B", "#EF4444", "#8B5CF6", "#06B6D4", "#EC4899"
        };

        private static readonly IBrush[] ConfettiBrushes;

        static ConfettiOverlay()
        {
            ConfettiBrushes = new IBrush[ConfettiColorHexes.Length];
            for (int i = 0; i < ConfettiColorHexes.Length; i++)
            {
                ConfettiBrushes[i] = new SolidColorBrush(Color.Parse(ConfettiColorHexes[i]));
            }
        }

        private class RainParticle
        {
            public IBrush Brush = null!;
            public double StartX;
            public double StartY;
            public double FallSpeed;
            public double SwayFreq;
            public double SwayAmp;
            public double SwayPhase;
            public double Width;
            public double Height;
            public bool IsCircle;
            public double RotationSpeed;
            public double LifetimeSec;
            public double ElapsedSec;
        }

        private class BurstParticle
        {
            public IBrush Brush = null!;
            public double Angle;
            public double MaxDistance;
            public double Size;
            public bool IsCircle;
            public double RotationSpeed;
            public Point Origin;
            public double LifetimeSec;
            public double ElapsedSec;
        }

        private readonly List<RainParticle> _rainParticles = new();
        private readonly List<BurstParticle> _burstParticles = new();
        private readonly DispatcherTimer _timer;
        private DateTime _lastTick = DateTime.UtcNow;
        private readonly Random _rand = new();

        public ConfettiOverlay()
        {
            IsHitTestVisible = false;
            ClipToBounds = true;

            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(16)
            };
            _timer.Tick += OnTick;
        }

        public void LaunchMainTaskConfetti()
        {
            double w = Bounds.Width > 0 ? Bounds.Width : 440;
            double h = Bounds.Height > 0 ? Bounds.Height : 680;

            int count = 65;
            for (int i = 0; i < count; i++)
            {
                double size = _rand.Next(6, 12);
                var p = new RainParticle
                {
                    Brush = ConfettiBrushes[_rand.Next(ConfettiBrushes.Length)],
                    StartX = _rand.NextDouble() * (w - 20) + 10,
                    StartY = -15 - _rand.NextDouble() * 50,
                    FallSpeed = 160 + _rand.NextDouble() * 180,
                    SwayFreq = 2.5 + _rand.NextDouble() * 3.5,
                    SwayAmp = 25 + _rand.NextDouble() * 45,
                    SwayPhase = _rand.NextDouble() * Math.PI * 2,
                    Width = size,
                    Height = size * (_rand.Next(2) == 0 ? 1.0 : 1.6),
                    IsCircle = _rand.Next(3) == 0,
                    RotationSpeed = (_rand.NextDouble() - 0.5) * 600,
                    LifetimeSec = 2.0 + _rand.NextDouble() * 0.8,
                    ElapsedSec = 0
                };
                _rainParticles.Add(p);
            }

            if (!_timer.IsEnabled)
            {
                _lastTick = DateTime.UtcNow;
                _timer.Start();
            }
            InvalidateVisual();
        }

        public void LaunchSubtaskFireworks(Point center)
        {
            int count = 26;
            double baseAngle = _rand.NextDouble() * Math.PI * 2.0;

            for (int i = 0; i < count; i++)
            {
                double angle = baseAngle + (i * 2.0 * Math.PI / count) + ((_rand.NextDouble() - 0.5) * 0.25);
                double distance = 35.0 + _rand.NextDouble() * 35.0;
                double size = 5.0 + _rand.NextDouble() * 4.5;

                var p = new BurstParticle
                {
                    Brush = ConfettiBrushes[_rand.Next(ConfettiBrushes.Length)],
                    Angle = angle,
                    MaxDistance = distance,
                    Size = size,
                    IsCircle = _rand.Next(4) != 0,
                    RotationSpeed = (_rand.NextDouble() - 0.5) * 450,
                    Origin = center,
                    LifetimeSec = 0.52,
                    ElapsedSec = 0
                };
                _burstParticles.Add(p);
            }

            if (!_timer.IsEnabled)
            {
                _lastTick = DateTime.UtcNow;
                _timer.Start();
            }
            InvalidateVisual();
        }

        private void OnTick(object? sender, EventArgs e)
        {
            var now = DateTime.UtcNow;
            double dt = (now - _lastTick).TotalSeconds;
            _lastTick = now;

            if (dt > 0.1) dt = 0.016;

            for (int i = _rainParticles.Count - 1; i >= 0; i--)
            {
                var p = _rainParticles[i];
                p.ElapsedSec += dt;
                if (p.ElapsedSec >= p.LifetimeSec)
                {
                    _rainParticles.RemoveAt(i);
                }
            }

            for (int i = _burstParticles.Count - 1; i >= 0; i--)
            {
                var p = _burstParticles[i];
                p.ElapsedSec += dt;
                if (p.ElapsedSec >= p.LifetimeSec)
                {
                    _burstParticles.RemoveAt(i);
                }
            }

            if (_rainParticles.Count == 0 && _burstParticles.Count == 0)
            {
                _timer.Stop();
            }

            InvalidateVisual();
        }

        public override void Render(DrawingContext context)
        {
            base.Render(context);

            // 1. Chuva de Confetes
            foreach (var p in _rainParticles)
            {
                double t = p.ElapsedSec / p.LifetimeSec;
                double currentY = p.StartY + (p.FallSpeed * p.ElapsedSec) + (140.0 * p.ElapsedSec * p.ElapsedSec);
                double driftX = p.StartX + Math.Sin(p.ElapsedSec * p.SwayFreq + p.SwayPhase) * p.SwayAmp;
                double rot = p.RotationSpeed * p.ElapsedSec;

                double alpha = t > 0.75 ? Math.Clamp((1.0 - t) / 0.25, 0.0, 1.0) : 1.0;
                double tumbleScale = Math.Max(0.2, Math.Abs(Math.Cos(p.ElapsedSec * p.SwayFreq * 1.5 + p.SwayPhase)));

                using (context.PushOpacity(alpha))
                {
                    var center = new Point(driftX, currentY);
                    var matrix = Matrix.CreateTranslation(-center.X, -center.Y)
                               * Matrix.CreateRotation(rot * Math.PI / 180.0)
                               * Matrix.CreateTranslation(center.X, center.Y);

                    using (context.PushTransform(matrix))
                    {
                        if (p.IsCircle)
                        {
                            context.DrawEllipse(p.Brush, null, center, p.Width / 2.0, p.Width / 2.0);
                        }
                        else
                        {
                            double renderW = p.Width * tumbleScale;
                            double renderH = p.Height;
                            var rect = new Rect(driftX - renderW / 2.0, currentY - renderH / 2.0, renderW, renderH);
                            context.DrawRectangle(p.Brush, null, rect, 2, 2);
                        }
                    }
                }
            }

            // 2. Fogos Radiais de Subtarefa
            foreach (var p in _burstParticles)
            {
                double t = Math.Clamp(p.ElapsedSec / p.LifetimeSec, 0.0, 1.0);
                double ease = 1.0 - Math.Pow(1.0 - t, 3);
                double currentDist = p.MaxDistance * ease;

                double posX = p.Origin.X + Math.Cos(p.Angle) * currentDist;
                double posY = p.Origin.Y + Math.Sin(p.Angle) * currentDist;

                double alpha = t > 0.65 ? Math.Clamp((1.0 - t) / 0.35, 0.0, 1.0) : 1.0;
                double scale = Math.Max(0.4, 1.0 - (t * 0.5));
                double currentSize = p.Size * scale;

                using (context.PushOpacity(alpha))
                {
                    var center = new Point(posX, posY);
                    if (p.IsCircle)
                    {
                        context.DrawEllipse(p.Brush, null, center, currentSize / 2.0, currentSize / 2.0);
                    }
                    else
                    {
                        double rot = p.RotationSpeed * t;
                        var matrix = Matrix.CreateTranslation(-center.X, -center.Y)
                                   * Matrix.CreateRotation(rot * Math.PI / 180.0)
                                   * Matrix.CreateTranslation(center.X, center.Y);

                        using (context.PushTransform(matrix))
                        {
                            var rect = new Rect(posX - currentSize / 2.0, posY - currentSize / 2.0, currentSize, currentSize);
                            context.DrawRectangle(p.Brush, null, rect, 2, 2);
                        }
                    }
                }
            }
        }
    }
}
