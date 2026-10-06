using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;

class EditHeroImage {
    struct PillDef {
        public float X, Y, W, H;
        public string Text;
        public PillDef(float x, float y, float w, float h, string t) { X = x; Y = y; W = w; H = h; Text = t; }
    }

    static void Main() {
        string heroPath = @"docs\hero.png";
        string markPath = @"brand\mark-512.png";
        string outPath = @"docs\hero.png";

        Console.WriteLine("Loading hero image: " + heroPath);
        using (Bitmap hero = new Bitmap(heroPath))
        using (Bitmap mark = new Bitmap(markPath)) {
            using (Graphics g = Graphics.FromImage(hero)) {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

                Color bg = Color.FromArgb(17, 15, 13);
                Color railBg = Color.FromArgb(15, 13, 11);

                // ========================================================
                // 1. UPDATE THE 3 WINDOW RAIL ICONS
                // Window 1 center: (1751, 246)
                // Window 2 center: (2599, 175)
                // Window 3 center: (1943, 716)
                // ========================================================
                int iconSize = 28;
                int clearSize = 46;
                Point[] railCenters = new Point[] {
                    new Point(1751, 246),
                    new Point(2599, 175),
                    new Point(1943, 716)
                };

                using (SolidBrush rb = new SolidBrush(railBg)) {
                    foreach (var pt in railCenters) {
                        g.FillRectangle(rb, pt.X - clearSize / 2, pt.Y - clearSize / 2, clearSize, clearSize);
                        g.DrawImage(mark, pt.X - iconSize / 2, pt.Y - iconSize / 2, iconSize, iconSize);
                    }
                }

                // ========================================================
                // 2. CLEAR LEFT BRANDING & TEXT AREA
                // Clear from X=120 to X=1680, Y=360 to Y=1450
                // ========================================================
                using (SolidBrush b = new SolidBrush(bg)) {
                    g.FillRectangle(b, 120, 360, 1560, 1090);
                }

                // ========================================================
                // 3. LOAD FONTS
                // ========================================================
                PrivateFontCollection pfc = new PrivateFontCollection();
                pfc.AddFontFile(@"fonts\IBMPlexSans-SemiBold.ttf");
                pfc.AddFontFile(@"fonts\IBMPlexSans-Regular.ttf");
                pfc.AddFontFile(@"fonts\IBMPlexMono-Regular.ttf");

                FontFamily sansSemiBold = null, sansRegular = null, monoRegular = null;
                foreach (var fam in pfc.Families) {
                    if (fam.Name.Contains("SemiBold")) sansSemiBold = fam;
                    else if (fam.Name.Contains("Mono")) monoRegular = fam;
                    else sansRegular = fam;
                }
                if (sansSemiBold == null) sansSemiBold = sansRegular;

                Font fontTitle = new Font(sansSemiBold, 104f, FontStyle.Bold, GraphicsUnit.Pixel);
                Font fontTagline = new Font(sansRegular, 58f, FontStyle.Regular, GraphicsUnit.Pixel);
                Font fontPill = new Font(monoRegular, 34f, FontStyle.Regular, GraphicsUnit.Pixel);
                Font fontBrandTag = new Font(sansSemiBold, 22f, FontStyle.Bold, GraphicsUnit.Pixel);
                Font fontLaptopList = new Font(monoRegular, 29f, FontStyle.Regular, GraphicsUnit.Pixel);

                // ========================================================
                // 4. DRAW BIG LOGO & TITLE
                // ========================================================
                int bigLogoX = 192;
                int bigLogoY = 411;
                int bigLogoW = 114;
                int bigLogoH = 114;
                g.DrawImage(mark, bigLogoX, bigLogoY, bigLogoW, bigLogoH);

                // Title: "Seal"
                using (SolidBrush textWhite = new SolidBrush(Color.FromArgb(245, 245, 245))) {
                    g.DrawString("Seal", fontTitle, textWhite, new PointF(340, 414));
                }

                // ========================================================
                // 5. DRAW TAGLINE
                // ========================================================
                using (SolidBrush textMain = new SolidBrush(Color.FromArgb(237, 234, 232)))
                using (SolidBrush textMuted = new SolidBrush(Color.FromArgb(144, 139, 135))) {
                    float tx = 208f;
                    float y1 = 655f;
                    float y2 = 735f;
                    float y3 = 815f;

                    g.DrawString("Fan curves, keyboard lighting and the", fontTagline, textMain, new PointF(tx, y1));
                    g.DrawString("OMEN key, without the vendor", fontTagline, textMain, new PointF(tx, y2));

                    // Third line: "software adware." with strike-through on "software"
                    string wordSoft = "software";
                    string wordAd = " adware.";
                    SizeF sizeSoft = g.MeasureString(wordSoft, fontTagline, new PointF(0, 0), StringFormat.GenericTypographic);

                    g.DrawString(wordSoft, fontTagline, textMuted, new PointF(tx, y3), StringFormat.GenericTypographic);
                    using (Pen penStrike = new Pen(Color.FromArgb(144, 139, 135), 4.5f)) {
                        float strikeY = y3 + sizeSoft.Height * 0.58f;
                        g.DrawLine(penStrike, tx, strikeY, tx + sizeSoft.Width + 4, strikeY);
                    }

                    g.DrawString(wordAd, fontTagline, textMain, new PointF(tx + sizeSoft.Width, y3), StringFormat.GenericTypographic);
                }

                // ========================================================
                // 6. DRAW TOP PILLS (AT Y = 1045)
                // ========================================================
                PillDef[] pills = new PillDef[] {
                    new PillDef(204f, 1045f, 370f, 96f, "Windows 10/11"),
                    new PillDef(600f, 1045f, 560f, 96f, "Every Supported Laptop"),
                    new PillDef(1186f, 1045f, 220f, 96f, "1.6 MB")
                };

                Color pillBgColor = Color.FromArgb(32, 28, 25);
                Color pillBorderColor = Color.FromArgb(44, 40, 37);
                using (SolidBrush pillBg = new SolidBrush(pillBgColor))
                using (Pen pillBorder = new Pen(pillBorderColor, 2f))
                using (SolidBrush pillText = new SolidBrush(Color.FromArgb(237, 234, 232))) {
                    foreach (var p in pills) {
                        using (GraphicsPath rpath = CreateRoundRect(p.X, p.Y, p.W, p.H, 18f)) {
                            g.FillPath(pillBg, rpath);
                            g.DrawPath(pillBorder, rpath);
                        }

                        SizeF sz = g.MeasureString(p.Text, fontPill, new PointF(0, 0), StringFormat.GenericTypographic);
                        float textX = p.X + (p.W - sz.Width) / 2f;
                        float textY = p.Y + (p.H - sz.Height) / 2f - 2f;
                        g.DrawString(p.Text, fontPill, pillText, new PointF(textX, textY), StringFormat.GenericTypographic);
                    }
                }

                // ========================================================
                // 7. DRAW SUPPORTED LAPTOPS (REPLACING GITHUB URL)
                // Every laptop model added in docs/laptops.md
                // ========================================================
                var laptopLines = new [] {
                    new { Tag = "HP",    Models = "OMEN Transcend (14/16) · OMEN 15/16/17 · OMEN MAX · Victus 15/16" },
                    new { Tag = "ASUS",  Models = "ROG Zephyrus (G14/15/16, M16) · Strix & SCAR · Flow · TUF Gaming" },
                    new { Tag = "Acer",  Models = "Predator Helios (16/18/Neo) · Predator Triton · Nitro 5/16/17/V" }
                };

                float lx = 208f;
                float startY = 1195f;
                float rowStep = 64f;
                float tagW = 104f;
                float tagH = 44f;

                Color tagBgColor = Color.FromArgb(40, 35, 31);
                Color tagBorderColor = Color.FromArgb(58, 52, 47);
                using (SolidBrush tagBg = new SolidBrush(tagBgColor))
                using (Pen tagBorder = new Pen(tagBorderColor, 1.5f))
                using (SolidBrush tagText = new SolidBrush(Color.FromArgb(240, 235, 230)))
                using (SolidBrush modelsBrush = new SolidBrush(Color.FromArgb(175, 170, 165))) {
                    for (int i = 0; i < laptopLines.Length; i++) {
                        float cy = startY + i * rowStep;
                        var item = laptopLines[i];

                        // Brand tag pill
                        using (GraphicsPath tpath = CreateRoundRect(lx, cy, tagW, tagH, 10f)) {
                            g.FillPath(tagBg, tpath);
                            g.DrawPath(tagBorder, tpath);
                        }

                        SizeF tsz = g.MeasureString(item.Tag, fontBrandTag, new PointF(0, 0), StringFormat.GenericTypographic);
                        float tx = lx + (tagW - tsz.Width) / 2f;
                        float ty = cy + (tagH - tsz.Height) / 2f - 1f;
                        g.DrawString(item.Tag, fontBrandTag, tagText, new PointF(tx, ty), StringFormat.GenericTypographic);

                        // Models line
                        float mx = lx + tagW + 24f;
                        float my = cy + (tagH - 29f) / 2f + 1f;
                        g.DrawString(item.Models, fontLaptopList, modelsBrush, new PointF(mx, my));
                    }
                }
            }

            // Save to docs\hero.png directly
            string tempOut = @"docs\hero_updated.png";
            hero.Save(tempOut, ImageFormat.Png);
            Console.WriteLine("Saved updated image to " + tempOut);
        }

        File.Copy(@"docs\hero_updated.png", @"docs\hero.png", true);
        File.Delete(@"docs\hero_updated.png");
        Console.WriteLine("Overwrote docs\\hero.png successfully!");
    }

    static GraphicsPath CreateRoundRect(float x, float y, float w, float h, float r) {
        GraphicsPath p = new GraphicsPath();
        float d = r * 2;
        p.AddArc(x, y, d, d, 180, 90);
        p.AddArc(x + w - d, y, d, d, 270, 90);
        p.AddArc(x + w - d, y + h - d, d, d, 0, 90);
        p.AddArc(x, y + h - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }
}
