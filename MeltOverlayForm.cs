using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;

namespace MoggadoSim
{
    public class SimulatedMeltForm : Form
    {
        private readonly int[] phaseDurationsMs = { 20000, 10000, 15000, 20000, 5000, 20000 };
        private int currentPhase = 0;
        private DateTime phaseStart;
        private System.Windows.Forms.Timer frameTimer;
        private Bitmap? sourceBmp;
        private Bitmap? workingBmp;
        private readonly Random rnd = new Random();

        private int columnWidth = 4;
        private int[]? offsets;
        private int maxDropSpeed = 18;
        private float phase3Angle = 0f;
        private int duplicateCount = 24;

        private readonly List<PointF> texts = new();
        private readonly List<float> textSpeeds = new();
        private readonly List<PointF> xPos = new();
        private readonly List<PointF> xVel = new();

        private readonly Button btnLoad;
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

            btnLoad = new Button { Text = "Load Image", Left = 10, Top = 10, AutoSize = true };
            btnSkip = new Button { Text = "Próximo", Left = 120, Top = 10, AutoSize = true };
            btnPause = new Button { Text = "Pausar", Left = 220, Top = 10, AutoSize = true };
            lblPhase = new Label { Text = "Fase: 0", Left = 320, Top = 15, AutoSize = true };

            Controls.AddRange(new Control[] { btnLoad, btnSkip, btnPause, lblPhase });

            btnLoad.Click += BtnLoad_Click;
            btnSkip.Click += (s, e) => StartPhase(currentPhase + 1);
            btnPause.Click += (s, e) => TogglePause();

            KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Escape) Close();
                if (e.KeyCode == Keys.Space) TogglePause();
                if (e.KeyCode == Keys.Oem2) StartPhase(currentPhase + 1);
            };

            frameTimer = new System.Windows.Forms.Timer { Interval = 33 };
            frameTimer.Tick += FrameTimer_Tick;
            frameTimer.Start();

            StartPhase(0);
        }

        private void BtnLoad_Click(object? sender, EventArgs e)
        {
            using var ofd = new OpenFileDialog { Filter = "Images|*.png;*.jpg;*.jpeg;*.bmp;*.gif|All files|*.*" };
            if (ofd.ShowDialog() != DialogResult.OK) return;
            using var img = (Bitmap)Image.FromFile(ofd.FileName);
            sourceBmp?.Dispose();
            sourceBmp = new Bitmap(img);
            ResizeWorkingBmp();
            StartPhase(0);
        }

        private void TogglePause()
        {
            paused = !paused;
            btnPause.Text = paused ? "Retomar" : "Pausar";
        }

        private void FrameTimer_Tick(object? sender, EventArgs e)
        {
            if (paused) return;

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

            if (currentPhase == 0) UpdateOffsets(-4, maxDropSpeed);
            if (currentPhase == 2) phase3Angle += 6f + 18f * GetPhaseProgress();
            if (currentPhase == 5) UpdateXMovers();

            Invalidate();
        }

        private void StartPhase(int index)
        {
            if (index >= phaseDurationsMs.Length) { FinalizeAndOpenTxt(); return; }
            currentPhase = index;
            phaseStart = DateTime.Now;
            lblPhase.Text = $"Fase: {currentPhase}";
            columnWidth = 4;
            maxDropSpeed = 18;
            InitializeMeltOffsets();
            if (currentPhase == 5) InitRedX();
        }

        private void InitializeMeltOffsets()
        {
            int cols = Math.Max(1, (int)Math.Ceiling((double)ClientSize.Width / columnWidth));
            offsets = new int[cols];
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
            for (int i = 0; i < 8; i++)
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
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            ResizeWorkingBmp();
            InitializeMeltOffsets();
        }

        private void ResizeWorkingBmp()
        {
            workingBmp?.Dispose();
            workingBmp = new Bitmap(ClientSize.Width, ClientSize.Height);
            using var g = Graphics.FromImage(workingBmp);
            if (sourceBmp != null)
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.DrawImage(sourceBmp, 0, 0, ClientSize.Width, ClientSize.Height);
            }
            else g.Clear(Color.Black);
        }

        private float GetPhaseProgress()
        {
            double elapsed = (DateTime.Now - phaseStart).TotalMilliseconds;
            return (float)Math.Min(1.0, elapsed / phaseDurationsMs[Math.Min(currentPhase, phaseDurationsMs.Length - 1)]);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            if (workingBmp == null) { g.Clear(Color.Black); return; }

            switch (currentPhase)
            {
                case 0: DrawMelt(g); break;
                case 1: DrawZoomLag(g); break;
                case 2: DrawRotatingDuplicates(g); break;
                case 3: DrawStaticNoise(g); break;
                case 4: DrawPixelate(g); break;
                case 5: DrawRedSquareXs(g); break;
                default: g.DrawImage(workingBmp, 0, 0); break;
            }

            using var f = new Font("Segoe UI", 12, FontStyle.Bold);
            using var b = new SolidBrush(Color.White);
            foreach (var p in texts) g.DrawString("moggado pelo jodismiu", f, b, p);

            if (GetPhaseProgress() >= 1.0f) StartPhase(currentPhase + 1);
        }

        private void DrawMelt(Graphics g)
        {
            if (sourceBmp == null || workingBmp == null || offsets == null) { g.DrawImage(workingBmp ?? new Bitmap(ClientSize.Width, ClientSize.Height), 0, 0, ClientSize.Width, ClientSize.Height); return; }
            using (Bitmap src = new Bitmap(sourceBmp, ClientSize.Width, ClientSize.Height))
            {
                int cols = offsets.Length;
                for (int col = 0; col < cols; col++)
                {
                    int sx = col * columnWidth;
                    int sw = Math.Min(columnWidth, ClientSize.Width - sx);
                    int dx = sx;
                    int dy = offsets[col];
                    Rectangle srcRect = new Rectangle(sx, 0, sw, ClientSize.Height);
                    Rectangle dstRect = new Rectangle(dx, dy, sw, ClientSize.Height);
                    int visibleY = Math.Max(0, dstRect.Top);
                    int visibleHeight = Math.Min(ClientSize.Height - visibleY, ClientSize.Height);
                    if (visibleHeight <= 0) continue;
                    Rectangle srcVisible = new Rectangle(srcRect.X, srcRect.Y + (visibleY - dstRect.Top), srcRect.Width, visibleHeight);
                    Rectangle dstVisible = new Rectangle(dstRect.X, visibleY, srcVisible.Width, srcVisible.Height);
                    g.DrawImage(src, dstVisible, srcVisible, GraphicsUnit.Pixel);
                }
            }
        }

        private void DrawZoomLag(Graphics g)
        {
            float p = GetPhaseProgress();
            float z = 1 + 0.6f * p;
            g.DrawImage(workingBmp!, (ClientSize.Width - ClientSize.Width * z) / 2, (ClientSize.Height - ClientSize.Height * z) / 2, ClientSize.Width * z, ClientSize.Height * z);
        }

        private void DrawRotatingDuplicates(Graphics g)
        {
            float angle = phase3Angle;
            int thumbW = Math.Max(8, ClientSize.Width / 6);
            int thumbH = Math.Max(8, ClientSize.Height / 6);
            using (Bitmap thumb = new Bitmap(thumbW, thumbH))
            using (Graphics gt = Graphics.FromImage(thumb))
            {
                gt.DrawImage(workingBmp!, 0, 0, thumbW, thumbH);
                float radius = Math.Min(ClientSize.Width, ClientSize.Height) * 0.35f;
                PointF center = new PointF(ClientSize.Width / 2f, ClientSize.Height / 2f);
                for (int i = 0; i < duplicateCount; i++)
                {
                    float a = angle + i * (360f / duplicateCount);
                    double rad = a * Math.PI / 180.0;
                    float x = center.X + radius * (float)Math.Cos(rad) - thumbW / 2f;
                    float y = center.Y + radius * (float)Math.Sin(rad) - thumbH / 2f;
                    g.DrawImage(thumb, x, y, thumbW, thumbH);
                }
            }
        }

        private void DrawStaticNoise(Graphics g)
        {
            g.DrawImage(workingBmp!, 0, 0, ClientSize.Width, ClientSize.Height);
            int w = ClientSize.Width, h = ClientSize.Height;
            using Bitmap noise = new Bitmap(w, h);
            using Graphics gn = Graphics.FromImage(noise);
            for (int x = 0; x < w; x += 4)
                for (int y = 0; y < h; y += 4)
                {
                    int v = rnd.Next(256);
                    using Brush br = new SolidBrush(Color.FromArgb(v, v, v))
                        gn.FillRectangle(br, x, y, 4, 4);
                }
            using TextureBrush tb = new(noise) { WrapMode = WrapMode.Tile };
            g.FillRectangle(tb, 0, 0, w, h);
        }

        private void DrawPixelate(Graphics g)
        {
            float p = GetPhaseProgress();
            int block = (int)(8 + (40 - 8) * p);
            int w = ClientSize.Width, h = ClientSize.Height;
            using (Bitmap small = new Bitmap(Math.Max(1, w / block), Math.Max(1, h / block)))
            using (Graphics gs = Graphics.FromImage(small))
            {
                gs.InterpolationMode = InterpolationMode.NearestNeighbor;
                gs.DrawImage(workingBmp!, 0, 0, small.Width, small.Height);
                g.InterpolationMode = InterpolationMode.NearestNeighbor;
                g.DrawImage(small, 0, 0, w, h);
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            }
        }

        private void DrawRedSquareXs(Graphics g)
        {
            float p = GetPhaseProgress();
            g.DrawImage(workingBmp!, 0, 0, ClientSize.Width, ClientSize.Height);
            Rectangle fill = new Rectangle(
                (int)(ClientSize.Width * (0.5f - 0.5f * p)),
                (int)(ClientSize.Height * (0.5f - 0.5f * p)),
                (int)(ClientSize.Width * p),
                (int)(ClientSize.Height * p));
            using (Brush br = new SolidBrush(Color.FromArgb(220, 200, 0, 0)))
                g.FillRectangle(br, fill);
            using (Pen pen = new Pen(Color.White, 3))
            using (Brush brx = new SolidBrush(Color.Red))
            {
                foreach (var pos in xPos)
                {
                    int size = 28;
                    Rectangle r = new Rectangle((int)pos.X - size / 2, (int)pos.Y - size / 2, size, size);
                    g.FillRectangle(brx, r);
                    g.DrawLine(pen, r.Left + 4, r.Top + 4, r.Right - 4, r.Bottom - 4);
                    g.DrawLine(pen, r.Left + 4, r.Bottom - 4, r.Right - 4, r.Top + 4);
                }
            }
        }

        private void FinalizeAndOpenTxt()
        {
            frameTimer.Stop();
            try
            {
                string temp = Path.Combine(Path.GetTempPath(), "moggado_final.txt");
                File.WriteAllText(temp, "MOGGADO pelo jodismiu\r\nFim da sequência.");
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("notepad.exe", temp) { UseShellExecute = true });
            }
            catch { }
        }
    }
}