using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;

namespace MoggadoSim
{
    public class SimulatedMeltForm : Form
    {
        // Durations (ms) conforme solicitado:
        // 10s, 15s, 20s, 25s, 30s, 35s
        private readonly int[] phaseDurationsMs = { 10000, 15000, 20000, 25000, 30000, 35000 };

        private int currentPhase = 0;
        private DateTime phaseStart = DateTime.MinValue;
        private readonly System.Windows.Forms.Timer frameTimer;
        private Bitmap? sourceBmp;   // captura da tela ou imagem carregada
        private Bitmap? workingBmp;  // buffer redimensionado à janela
        private readonly Random rnd = new();

        // Melt effect
        private int columnWidth = 4;
        private int[]? offsets;
        private int maxDropSpeed = 18;

        // rotating duplicates
        private float phase3Angle = 0f;
        private int duplicateCount = 28;

        // floating text
        private readonly List<PointF> texts = new();
        private readonly List<float> textSpeeds = new();

        // red X movers
        private readonly List<PointF> xPos = new();
        private readonly List<PointF> xVel = new();

        // UI
        private readonly Button btnRecapture;
        private readonly Button btnSkip;
        private readonly Button btnPause;
        private readonly Label lblPhase;
        private bool paused = false;

        public SimulatedMeltForm()
        {
            Text = "MOGGADO — Simulated Melt (safe)";
            WindowState = FormWindowState.Maximized;
            DoubleBuffered = true;
            KeyPreview = true;
            FormBorderStyle = FormBorderStyle.Sizable;
            MinimumSize = new Size(800, 600);

            // UI controls
            btnRecapture = new Button { Text = "Recapturar", Left = 10, Top = 10, AutoSize = true };
            btnSkip = new Button { Text = "Próximo (/)", Left = 120, Top = 10, AutoSize = true };
            btnPause = new Button { Text = "Pausar (Space)", Left = 230, Top = 10, AutoSize = true };
            lblPhase = new Label { Text = "Fase: 0", Left = 360, Top = 15, AutoSize = true };

            Controls.AddRange(new Control[] { btnRecapture, btnSkip, btnPause, lblPhase });

            btnRecapture.Click += (s, e) => { CaptureScreenToSourceBmp(); };
            btnSkip.Click += (s, e) => StartPhase(currentPhase + 1);
            btnPause.Click += (s, e) => TogglePause();

            KeyDown += OnKeyDownHandler;

            // frame timer (~30 FPS)
            frameTimer = new System.Windows.Forms.Timer { Interval = 33 };
            frameTimer.Tick += FrameTimer_Tick;
            frameTimer.Start();

            // capture immediately at startup
            CaptureScreenToSourceBmp();
            StartPhase(0);
        }

        private void OnKeyDownHandler(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape) Close();
            if (e.KeyCode == Keys.Space) TogglePause();
            if (e.KeyCode == Keys.Oem2) StartPhase(currentPhase + 1); // "/" key
        }

        private void TogglePause()
        {
            paused = !paused;
            btnPause.Text = paused ? "Retomar (Space)" : "Pausar (Space)";
        }

        private void FrameTimer_Tick(object? sender, EventArgs e)
        {
            if (paused) return;

            // spawn floating texts occasionally
            if (rnd.NextDouble() < 0.04)
            {
                float x = rnd.Next(Math.Max(1, ClientSize.Width - 200));
                float y = ClientSize.Height + rnd.Next(5, 80);
                texts.Add(new PointF(x, y));
                textSpeeds.Add((float)(1.0 + rnd.NextDouble() * 2.0));
            }

            for (int i = texts.Count - 1; i >= 0; i--)
            {
                var p = texts[i];
                p.Y -= textSpeeds[i];
                texts[i] = p;
                if (p.Y < -50) { texts.RemoveAt(i); textSpeeds.RemoveAt(i); }
            }

            // per-phase updates
            if (currentPhase == 0) UpdateOffsets(-4, maxDropSpeed);
            if (currentPhase == 2) phase3Angle += 6f + 18f * GetPhaseProgress();
            if (currentPhase == 5) UpdateXMovers();

            Invalidate();
        }

        private void StartPhase(int index)
        {
            if (index >= phaseDurationsMs.Length)
            {
                FinalizeAndOpenTxt();
                return;
            }

            currentPhase = Math.Max(0, index);
            phaseStart = DateTime.Now;
            lblPhase.Text = $"Fase: {currentPhase}";

            switch (currentPhase)
            {
                case 0: columnWidth = 4; maxDropSpeed = 18; InitializeMeltOffsets(); break;
                case 1: columnWidth = 3; maxDropSpeed = 8; InitializeMeltOffsets(); break;
                case 2: columnWidth = 4; duplicateCount = 28; InitializeMeltOffsets(); break;
                case 3: columnWidth = 2; maxDropSpeed = 4; InitializeMeltOffsets(); break;
                case 4: columnWidth = 6; InitializeMeltOffsets(); break;
                case 5: InitRedX(); InitializeMeltOffsets(); break;
            }
        }

        private void InitializeMeltOffsets()
        {
            int cols = Math.Max(1, (int)Math.Ceiling((double)ClientSize.Width / Math.Max(1, columnWidth)));
            offsets = new int[cols];
            for (int i = 0; i < cols; i++) offsets[i] = 0;
        }

        private void UpdateOffsets(int minDelta, int maxDelta)
        {
            if (offsets == null || workingBmp == null) return;
            for (int i = 0; i < offsets.Length; i++)
            {
                int change = rnd.Next(minDelta, maxDelta + 1);
                offsets[i] = Math.Max(-workingBmp.Height, offsets[i] + change);
                if (rnd.NextDouble() < 0.003) offsets[i] = 0;
            }
        }

        private void InitRedX()
        {
            xPos.Clear(); xVel.Clear();
            for (int i = 0; i < 10; i++)
            {
                xPos.Add(new PointF(rnd.Next(ClientSize.Width), rnd.Next(ClientSize.Height)));
                xVel.Add(new PointF((float)(rnd.NextDouble() * 8 - 4), (float)(rnd.NextDouble() * 8 - 4)));
            }
        }

        private void UpdateXMovers()
        {
            for (int i = 0; i < xPos.Count; i++)
            {
                var p = xPos[i];
                var v = xVel[i];
                p.X += v.X; p.Y += v.Y;
                if (p.X < 0 || p.X > ClientSize.Width) v.X = -v.X;
                if (p.Y < 0 || p.Y > ClientSize.Height) v.Y = -v.Y;
                xPos[i] = p; xVel[i] = v;

                if (rnd.NextDouble() < 0.02 && xPos.Count < 200)
                {
                    xPos.Add(new PointF(p.X + rnd.Next(-20, 20), p.Y + rnd.Next(-20, 20)));
                    xVel.Add(new PointF((float)(rnd.NextDouble() * 8 - 4), (float)(rnd.NextDouble() * 8 - 4)));
                }
            }
        }

        // Capture primary screen into sourceBmp (safe: only reads pixels)
        private void CaptureScreenToSourceBmp()
        {
            try
            {
                Rectangle bounds = Screen.PrimaryScreen.Bounds;
                using Bitmap bmp = new(bounds.Width, bounds.Height, PixelFormat.Format32bppArgb);
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size);
                }

                sourceBmp?.Dispose();
                sourceBmp = new Bitmap(bmp);

                ResizeWorkingBmp();
                StartPhase(0);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Erro ao capturar a tela: " + ex.Message);
            }
        }

        private void ResizeWorkingBmp()
        {
            workingBmp?.Dispose();
            workingBmp = new Bitmap(ClientSize.Width, ClientSize.Height);
            using (Graphics g = Graphics.FromImage(workingBmp))
            {
                if (sourceBmp != null)
                {
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.DrawImage(sourceBmp, 0, 0, ClientSize.Width, ClientSize.Height);
                }
                else
                {
                    g.Clear(Color.Black);
                }
            }
        }

        private float GetPhaseProgress()
        {
            if (phaseStart == DateTime.MinValue) return 0f;
            double elapsed = (DateTime.Now - phaseStart).TotalMilliseconds;
            int dur = phaseDurationsMs[Math.Min(currentPhase, phaseDurationsMs.Length - 1)];
            return (float)Math.Min(1.0, elapsed / dur);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            ResizeWorkingBmp();
            InitializeMeltOffsets();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            if (workingBmp == null)
            {
                using var f = new Font("Segoe UI", 18, FontStyle.Bold);
                using var b = new SolidBrush(Color.White);
                g.Clear(Color.Black);
                g.DrawString("Capturando a tela automaticamente... ESC para fechar.", f, b, 30, 80);
                return;
            }

            // each phase visually replaces the previous
            switch (currentPhase)
            {
                case 0: DrawMelt(g); break;                  // 10s
                case 1: DrawZoomLag(g); break;               // 15s
                case 2: DrawRotatingDuplicates(g); break;    // 20s
                case 3: DrawStaticNoise(g); break;           // 25s
                case 4: DrawOldTvPixelation(g); break;       // 30s
                case 5: DrawRedSquareXs(g); break;           // 35s
                default: g.DrawImage(workingBmp, 0, 0, ClientSize.Width, ClientSize.Height); break;
            }

            // floating small texts
            using var tf = new Font("Segoe UI", 12, FontStyle.Bold);
            using var tb = new SolidBrush(Color.FromArgb(220, 255, 255, 255));
            foreach (var p in texts) g.DrawString("moggado pelo jodismiu", tf, tb, p);

            // auto-advance phase if finished
            if (GetPhaseProgress() >= 1.0f)
            {
                StartPhase(currentPhase + 1);
            }
        }

        // ===== Phase implementations =====

        // Phase 0: Melt (columns shifted vertically)
        private void DrawMelt(Graphics g)
        {
            if (sourceBmp == null || workingBmp == null || offsets == null)
            {
                g.DrawImage(workingBmp ?? new Bitmap(ClientSize.Width, ClientSize.Height), 0, 0, ClientSize.Width, ClientSize.Height);
                return;
            }

            using Bitmap src = new(sourceBmp, ClientSize.Width, ClientSize.Height);
            int cols = offsets.Length;
            for (int col = 0; col < cols; col++)
            {
                int sx = col * columnWidth;
                int sw = Math.Min(columnWidth, ClientSize.Width - sx);
                int dx = sx;
                int dy = offsets[col];

                Rectangle srcRect = new(sx, 0, sw, ClientSize.Height);
                Rectangle dstRect = new(dx, dy, sw, ClientSize.Height);
                if (dstRect.Top > ClientSize.Height) continue;

                int visibleY = Math.Max(0, dstRect.Top);
                int visibleHeight = Math.Min(ClientSize.Height - visibleY, ClientSize.Height);
                if (visibleHeight <= 0) continue;

                Rectangle srcVisible = new(srcRect.X, srcRect.Y + (visibleY - dstRect.Top), srcRect.Width, visibleHeight);
                Rectangle dstVisible = new(dstRect.X, visibleY, srcVisible.Width, srcVisible.Height);

                g.DrawImage(src, dstVisible, srcVisible, GraphicsUnit.Pixel);
            }

            // decorative splashes
            using var br = new SolidBrush(Color.FromArgb(60, 255, 220, 160));
            for (int i = 0; i < 30; i++)
            {
                int rx = rnd.Next(ClientSize.Width);
                int ry = rnd.Next(ClientSize.Height);
                int rw = rnd.Next(20, 120);
                int rh = rnd.Next(10, 60);
                g.FillEllipse(br, rx - rw / 2, ry - rh / 2, rw, rh);
            }
        }

        // Phase 1: Zoom / lag
        private void DrawZoomLag(Graphics g)
        {
            float p = GetPhaseProgress();
            float zoom = 1.0f + 0.6f * p;
            int w = ClientSize.Width, h = ClientSize.Height;
            float sw = w * zoom, sh = h * zoom;
            float ox = (w - sw) / 2f, oy = (h - sh) / 2f;
            g.DrawImage(workingBmp!, ox, oy, sw, sh);

            using var overlay = new SolidBrush(Color.FromArgb((int)(60 + 120 * p), 255, 255, 200));
            g.FillRectangle(overlay, 0, 0, w, h);
        }

        // Phase 2: Rotating duplicates
        private void DrawRotatingDuplicates(Graphics g)
        {
            float angle = phase3Angle;
            int thumbW = Math.Max(8, ClientSize.Width / 6);
            int thumbH = Math.Max(8, ClientSize.Height / 6);
            using Bitmap thumb = new(thumbW, thumbH);
            using (Graphics gt = Graphics.FromImage(thumb))
            {
                gt.DrawImage(workingBmp!, 0, 0, thumbW, thumbH);
            }

            float radius = Math.Min(ClientSize.Width, ClientSize.Height) * 0.35f;
            PointF center = new(ClientSize.Width / 2f, ClientSize.Height / 2f);
            for (int i = 0; i < duplicateCount; i++)
            {
                float a = angle + i * (360f / duplicateCount);
                double rad = a * Math.PI / 180.0;
                float x = center.X + radius * (float)Math.Cos(rad) - thumbW / 2f;
                float y = center.Y + radius * (float)Math.Sin(rad) - thumbH / 2f;

                float scale = 0.7f + 0.6f * (float)Math.Abs(Math.Sin((a + phase3Angle) * Math.PI / 180.0));
                int dw = (int)(thumbW * scale);
                int dh = (int)(thumbH * scale);
                Rectangle dest = new((int)x - dw / 2 + thumbW / 2, (int)y - dh / 2 + thumbH / 2, dw, dh);
                g.DrawImage(thumb, dest);
            }
        }

        // Phase 3: Static noise (TV)
        private void DrawStaticNoise(Graphics g)
        {
            g.DrawImage(workingBmp!, 0, 0, ClientSize.Width, ClientSize.Height);
            int w = ClientSize.Width, h = ClientSize.Height;
            using Bitmap noise = new(w, h, PixelFormat.Format24bppRgb);
            using (Graphics gn = Graphics.FromImage(noise))
            {
                for (int x = 0; x < w; x += 4)
                {
                    for (int y = 0; y < h; y += 4)
                    {
                        int v = rnd.Next(256);
                        using var br = new SolidBrush(Color.FromArgb(v, v, v));
                        gn.FillRectangle(br, x, y, 4, 4);
                    }
                }
            }
            using (TextureBrush tb = new(noise) { WrapMode = WrapMode.Tile })
            {
                g.FillRectangle(tb, 0, 0, w, h);
            }
        }

        // Phase 4: Old TV pixelation (blocks grow)
        private void DrawOldTvPixelation(Graphics g)
        {
            float p = GetPhaseProgress();
            int block = Math.Max(1, (int)Lerp(8, 40, p));
            int w = ClientSize.Width, h = ClientSize.Height;
            using Bitmap small = new(Math.Max(1, w / block), Math.Max(1, h / block));
            using (Graphics gs = Graphics.FromImage(small))
            {
                gs.InterpolationMode = InterpolationMode.NearestNeighbor;
                gs.DrawImage(workingBmp!, 0, 0, small.Width, small.Height);
            }
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.DrawImage(small, 0, 0, w, h);
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        }

        // Phase 5: Red square + X movers
        private void DrawRedSquareXs(Graphics g)
        {
            float p = GetPhaseProgress();
            g.DrawImage(workingBmp!, 0, 0, ClientSize.Width, ClientSize.Height);

            Rectangle fill = new((int)(ClientSize.Width * (0.5f - 0.5f * p)),
                                 (int)(ClientSize.Height * (0.5f - 0.5f * p)),
                                 (int)(ClientSize.Width * p),
                                 (int)(ClientSize.Height * p));
            using var br = new SolidBrush(Color.FromArgb(220, 200, 0, 0));
            g.FillRectangle(br, fill);

            using var pen = new Pen(Color.White, 3);
            using var brx = new SolidBrush(Color.Red);
            foreach (var pos in xPos)
            {
                int size = 28;
                Rectangle r = new((int)pos.X - size / 2, (int)pos.Y - size / 2, size, size);
                g.FillRectangle(brx, r);
                g.DrawLine(pen, r.Left + 4, r.Top + 4, r.Right - 4, r.Bottom - 4);
                g.DrawLine(pen, r.Left + 4, r.Bottom - 4, r.Right - 4, r.Top + 4);
            }
        }

        private static double Lerp(double a, double b, double t) => a + (b - a) * t;

        private void FinalizeAndOpenTxt()
        {
            frameTimer.Stop();
            workingBmp?.Dispose();
            workingBmp = new Bitmap(ClientSize.Width, ClientSize.Height);
            using (Graphics g = Graphics.FromImage(workingBmp))
                g.Clear(Color.Black);
            Invalidate();

            try
            {
                string temp = Path.Combine(Path.GetTempPath(), "moggado_final.txt");
                File.WriteAllText(temp, "MOGGADO pelo jodismiu\r\nFim da sequência.");
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("notepad.exe", temp) { UseShellExecute = true });
            }
            catch { /* ignore */ }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                frameTimer.Stop();
                frameTimer.Dispose();
                sourceBmp?.Dispose();
                workingBmp?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
